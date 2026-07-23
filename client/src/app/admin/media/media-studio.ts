import { Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { GalleryItemDto, MediaFolderDto, MediaType } from '../../core/models';
import { ConfirmService } from '../ui/confirm.service';
import { ToastService } from '../ui/toast.service';
import { PageHeader } from '../ui/page-header';
import { EmptyState, Spinner } from '../ui/state-views';
import { BrandingCard } from './branding-card';
import { CaptionEdit, MediaGrid } from './media-grid';
import { FolderRail, FolderSelection } from './folder-rail';
import { UploadPanel } from './upload-panel';

@Component({
  selector: 'app-media-studio',
  imports: [
    TranslocoPipe, PageHeader, EmptyState, Spinner,
    BrandingCard, FolderRail, UploadPanel, MediaGrid
  ],
  template: `
    <app-page-header
      [heading]="'admin.media.title' | transloco"
      [subtitle]="'admin.media.subtitle' | transloco" />

    <app-branding-card />

    <div class="studio">
      <aside class="studio-rail">
        <app-folder-rail
          [folders]="folders()"
          [selected]="selection()"
          [totalCount]="items().length"
          [unfiledCount]="unfiledCount()"
          (select)="selection.set($event)"
          (createFolder)="createFolder($event)"
          (renameFolder)="renameFolder($event)"
          (deleteFolder)="deleteFolder($event)" />
      </aside>

      <div class="studio-main">
        <app-upload-panel
          [targetName]="selectedFolderName() ?? ('admin.media.unfiled' | transloco)"
          (uploaded)="onUploaded($event)"
          (batchFinished)="onBatchFinished($event)" />

        @if (loading()) {
          <app-spinner />
        } @else if (visibleItems().length) {
          <app-media-grid
            [items]="visibleItems()"
            [folders]="folders()"
            (bulkMove)="bulkMove($event)"
            (bulkDelete)="bulkDelete($event)"
            (bulkCaptions)="bulkCaptions($event)"
            (deleteOne)="deleteOne($event)" />
        } @else {
          <app-empty-state [message]="'admin.media.empty' | transloco" />
        }
      </div>
    </div>
  `,
  styles: `
    .studio {
      display: grid;
      grid-template-columns: 210px minmax(0, 1fr);
      gap: 1.4rem;
      align-items: start;
    }

    .studio-rail {
      position: sticky;
      top: 1rem;
    }

    .studio-main {
      display: flex;
      flex-direction: column;
      gap: 1.1rem;
      min-width: 0;
    }

    @media (max-width: 780px) {
      .studio {
        grid-template-columns: 1fr;
      }

      .studio-rail {
        position: static;
      }
    }
  `
})
export class MediaStudio {
  private api = inject(ApiService);
  private toasts = inject(ToastService);
  private confirm = inject(ConfirmService);
  private transloco = inject(TranslocoService);

  readonly items = signal<GalleryItemDto[]>([]);
  readonly folders = signal<MediaFolderDto[]>([]);
  readonly selection = signal<FolderSelection>(null);
  readonly loading = signal(true);

  readonly unfiledCount = computed(() => this.items().filter(i => i.folderId === null).length);

  readonly visibleItems = computed(() => {
    const selection = this.selection();
    if (selection === null) return this.items();
    if (selection === 'unfiled') return this.items().filter(i => i.folderId === null);
    return this.items().filter(i => i.folderId === selection);
  });

  /** Name of the folder uploads land in, or null when that is "unfiled". */
  readonly selectedFolderName = computed(() => {
    const selection = this.selection();
    if (typeof selection !== 'number') return null;
    return this.folders().find(f => f.id === selection)?.name ?? null;
  });

  constructor() {
    this.load();
  }

  private load() {
    this.loading.set(true);
    forkJoin({ items: this.api.getGallery(), folders: this.api.getFolders() }).subscribe({
      next: ({ items, folders }) => {
        this.items.set(items);
        this.folders.set(folders);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  /** Each finished upload is registered as a gallery item in the selected folder. */
  onUploaded(result: { url: string; thumbUrl: string | null; mediaType: MediaType }) {
    const selection = this.selection();
    const folderId = typeof selection === 'number' ? selection : null;

    this.api.adminAddGalleryItem({
      eventId: this.folders().find(f => f.id === folderId)?.eventId ?? null,
      folderId,
      imageUrl: result.url,
      thumbUrl: result.thumbUrl,
      mediaType: result.mediaType,
      captionEn: '', captionNl: '', captionAr: '',
      sortOrder: this.items().length
    }).subscribe({
      next: item => this.items.update(list => [item, ...list]),
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  onBatchFinished({ ok, failed }: { ok: number; failed: number }) {
    if (failed > 0) this.toasts.error(this.transloco.translate('admin.media.uploadFailed'));
    if (ok > 0) this.refreshFolders();
  }

  createFolder(name: string) {
    this.api.adminCreateFolder({ name, eventId: null, sortOrder: this.folders().length }).subscribe({
      next: folder => {
        this.folders.update(list => [...list, folder]);
        this.toasts.success(this.transloco.translate('admin.media.folderCreated'));
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  renameFolder({ folder, name }: { folder: MediaFolderDto; name: string }) {
    this.api.adminUpdateFolder(folder.id, { name, eventId: folder.eventId, sortOrder: folder.sortOrder })
      .subscribe({
        next: updated => {
          this.folders.update(list => list.map(f => (f.id === updated.id ? updated : f)));
          this.toasts.success(this.transloco.translate('admin.common.saved'));
        },
        error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
      });
  }

  async deleteFolder(folder: MediaFolderDto) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.media.confirmDeleteFolder'),
      confirmLabel: this.transloco.translate('admin.media.deleteFolder'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (!ok) return;

    this.api.adminDeleteFolder(folder.id).subscribe({
      next: () => {
        if (this.selection() === folder.id) this.selection.set(null);
        this.toasts.success(this.transloco.translate('admin.media.folderDeleted'));
        this.load();
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  bulkMove({ ids, folderId }: { ids: number[]; folderId: number | null }) {
    this.api.adminBulkMove(ids, folderId).subscribe({
      next: () => {
        this.items.update(list =>
          list.map(i => (ids.includes(i.id) ? { ...i, folderId } : i)));
        this.refreshFolders();
        this.toasts.success(this.transloco.translate('admin.media.moved'));
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  async bulkDelete(ids: number[]) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.media.confirmBulkDelete'),
      confirmLabel: this.transloco.translate('admin.media.delete'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (!ok) return;

    this.api.adminBulkDelete(ids).subscribe({
      next: () => {
        this.items.update(list => list.filter(i => !ids.includes(i.id)));
        this.refreshFolders();
        this.toasts.success(this.transloco.translate('admin.media.deleted'));
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  bulkCaptions({ ids, captions }: { ids: number[]; captions: CaptionEdit }) {
    this.api.adminBulkUpdateCaptions(ids, captions).subscribe({
      next: updated => {
        const byId = new Map(updated.map(i => [i.id, i]));
        this.items.update(list => list.map(i => byId.get(i.id) ?? i));
        this.toasts.success(this.transloco.translate('admin.media.captionsUpdated'));
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  async deleteOne(item: GalleryItemDto) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.media.confirmDeleteOne'),
      confirmLabel: this.transloco.translate('admin.media.delete'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (!ok) return;

    this.api.adminDeleteGalleryItem(item.id).subscribe({
      next: () => {
        this.items.update(list => list.filter(i => i.id !== item.id));
        this.refreshFolders();
        this.toasts.success(this.transloco.translate('admin.media.deleted'));
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  private refreshFolders() {
    this.api.getFolders().subscribe(folders => this.folders.set(folders));
  }
}
