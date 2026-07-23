import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';
import { GalleryCacheService } from '../../core/gallery-cache.service';
import { LanguageService } from '../../core/language.service';
import { originalUrl } from '../../core/media-url';
import { GalleryItemDto, MediaFolderDto } from '../../core/models';

/**
 * Repeating bento rhythm: wide, tall, square, panoramic, then a balanced pair.
 * Index into this by position so a dynamic gallery still reads as a composed layout.
 */
const LAYOUT = ['wide', 'tall', 'square', 'pano', 'half', 'half'] as const;

const PAGE_SIZE = 50;

@Component({
  selector: 'app-gallery-page',
  imports: [TranslocoPipe, CmsPipe],
  templateUrl: './gallery-page.html',
  styleUrl: './gallery-page.scss'
})
export class GalleryPage {
  private api = inject(ApiService);
  private cache = inject(GalleryCacheService);
  readonly lang = inject(LanguageService);

  readonly folders = toSignal(this.api.getFolders(), { initialValue: [] as MediaFolderDto[] });

  /** null = show everything, otherwise a folder id. */
  readonly activeFolder = signal<number | null>(null);

  /** Accumulated items for the active filter — grows as "Load more" fetches pages. */
  readonly items = signal<GalleryItemDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly hasMore = computed(() => this.items().length < this.total());

  /** Only non-empty folders are worth offering as a filter. ItemCount from /api/folders
   *  is a full per-folder count, independent of pagination. */
  readonly filters = computed(() => this.folders().filter(f => f.itemCount > 0));

  readonly visible = computed(() =>
    this.items().map((item, index) => ({ item, shape: LAYOUT[index % LAYOUT.length] })));

  constructor() {
    this.loadFolder(null);
  }

  select(folderId: number | null) {
    if (folderId === this.activeFolder()) return;
    this.activeFolder.set(folderId);
    this.closeLightbox();
    this.loadFolder(folderId);
  }

  /** Loads the first page of a folder from cache if present, else from the API. */
  private loadFolder(folderId: number | null) {
    const cached = this.cache.get(folderId);
    if (cached) {
      this.items.set(cached.items);
      this.total.set(cached.total);
      return;
    }
    this.items.set([]);
    this.total.set(0);
    this.fetchPage(folderId, 0);
  }

  loadMore() {
    if (this.loading() || !this.hasMore()) return;
    this.fetchPage(this.activeFolder(), this.items().length);
  }

  private fetchPage(folderId: number | null, skip: number) {
    this.loading.set(true);
    this.api.getGalleryPage({ folderId, skip, take: PAGE_SIZE }).subscribe({
      next: page => {
        // Guard against a stale response landing after the user switched filters.
        if (folderId !== this.activeFolder()) {
          this.loading.set(false);
          return;
        }
        const merged = skip === 0 ? page.items : [...this.items(), ...page.items];
        this.items.set(merged);
        this.total.set(page.total);
        this.cache.set(folderId, { items: merged, total: page.total });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
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
