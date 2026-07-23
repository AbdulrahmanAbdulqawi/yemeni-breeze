import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';
import { LanguageService } from '../../core/language.service';
import { originalUrl } from '../../core/media-url';
import { GalleryItemDto, MediaFolderDto } from '../../core/models';

/**
 * Repeating bento rhythm: wide, tall, square, panoramic, then a balanced pair.
 * Index into this by position so a dynamic gallery still reads as a composed layout.
 */
const LAYOUT = ['wide', 'tall', 'square', 'pano', 'half', 'half'] as const;

@Component({
  selector: 'app-gallery-page',
  imports: [TranslocoPipe, CmsPipe],
  templateUrl: './gallery-page.html',
  styleUrl: './gallery-page.scss'
})
export class GalleryPage {
  private api = inject(ApiService);
  readonly lang = inject(LanguageService);

  readonly items = toSignal(this.api.getGallery(), { initialValue: [] as GalleryItemDto[] });
  readonly folders = toSignal(this.api.getFolders(), { initialValue: [] as MediaFolderDto[] });

  /** null = show everything, otherwise a folder id. */
  readonly activeFolder = signal<number | null>(null);

  /** Only folders that actually hold something are worth offering as a filter. */
  readonly filters = computed(() => {
    const used = new Set(this.items().map(i => i.folderId).filter(id => id !== null));
    return this.folders().filter(f => used.has(f.id));
  });

  readonly visible = computed(() => {
    const folderId = this.activeFolder();
    const items = folderId === null
      ? this.items()
      : this.items().filter(i => i.folderId === folderId);

    return items.map((item, index) => ({ item, shape: LAYOUT[index % LAYOUT.length] }));
  });

  select(folderId: number | null) {
    this.activeFolder.set(folderId);
  }

  // --- Lightbox: image-only subset of the currently visible tiles, navigable by index ---

  readonly originalUrl = originalUrl;

  readonly lightboxImages = computed(() =>
    this.visible()
      .map(entry => entry.item)
      .filter(item => item.mediaType !== 'video' && item.mediaType !== 'file'));

  readonly lightboxIndex = signal<number | null>(null);

  readonly lightboxItem = computed(() => {
    const list = this.lightboxImages();
    const index = this.lightboxIndex();
    return index !== null && index >= 0 && index < list.length ? list[index] : null;
  });

  openLightbox(item: GalleryItemDto) {
    const index = this.lightboxImages().findIndex(i => i.id === item.id);
    this.lightboxIndex.set(index === -1 ? null : index);
  }

  closeLightbox() {
    this.lightboxIndex.set(null);
  }

  step(delta: number) {
    const list = this.lightboxImages();
    const index = this.lightboxIndex();
    if (!list.length || index === null) return;
    this.lightboxIndex.set((index + delta + list.length) % list.length);
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (this.lightboxIndex() === null) return;
    const rtl = this.lang.current() === 'ar';
    if (event.key === 'Escape') this.closeLightbox();
    else if (event.key === 'ArrowRight') this.step(rtl ? -1 : 1);
    else if (event.key === 'ArrowLeft') this.step(rtl ? 1 : -1);
  }

  private touchStartX = 0;

  onTouchStart(event: TouchEvent) {
    this.touchStartX = event.touches[0].clientX;
  }

  onTouchEnd(event: TouchEvent) {
    const delta = event.changedTouches[0].clientX - this.touchStartX;
    if (Math.abs(delta) < 40) return;
    const rtl = this.lang.current() === 'ar';
    this.step(delta < 0 ? (rtl ? -1 : 1) : rtl ? 1 : -1);
  }
}
