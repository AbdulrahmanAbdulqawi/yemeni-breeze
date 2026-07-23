import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';
import { LanguageService } from '../../core/language.service';
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
}
