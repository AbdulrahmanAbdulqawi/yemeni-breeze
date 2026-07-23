import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { GalleryItemDto, MediaFolderDto } from '../../core/models';

export interface CaptionEdit {
  captionEn?: string | null;
  captionNl?: string | null;
  captionAr?: string | null;
}

@Component({
  selector: 'app-media-grid',
  imports: [FormsModule, TranslocoPipe],
  template: `
    @if (selectedIds().size > 0) {
      <div class="bulk-bar">
        <strong>{{ selectedIds().size }}</strong>
        <span>{{ 'admin.media.selected' | transloco }}</span>

        <select class="ad-select" [(ngModel)]="moveTarget" (ngModelChange)="onMove($event)">
          <option [ngValue]="undefined">{{ 'admin.media.moveTo' | transloco }}…</option>
          <option [ngValue]="null">{{ 'admin.media.unfiled' | transloco }}</option>
          @for (folder of folders(); track folder.id) {
            <option [ngValue]="folder.id">{{ folder.name }}</option>
          }
        </select>

        <button type="button" class="btn-link" (click)="captionsOpen.set(!captionsOpen())">
          {{ 'admin.media.editCaptions' | transloco }}
        </button>
        <button type="button" class="btn-link danger" (click)="requestBulkDelete()">
          {{ 'admin.media.bulkDelete' | transloco }}
        </button>
        <button type="button" class="btn-link" (click)="clearSelection()">
          {{ 'admin.media.clearSelection' | transloco }}
        </button>
      </div>

      @if (captionsOpen()) {
        <form class="caption-form card" (ngSubmit)="applyCaptions()">
          <div class="caption-fields">
            <input [(ngModel)]="captionEn" name="captionEn" placeholder="Caption (English)" />
            <input [(ngModel)]="captionNl" name="captionNl" placeholder="Bijschrift (Nederlands)" />
            <input [(ngModel)]="captionAr" name="captionAr" placeholder="التعليق (عربي)" dir="rtl" />
          </div>
          <button type="submit" class="btn btn-primary">
            {{ 'admin.media.applyCaptions' | transloco }}
          </button>
        </form>
      }
    }

    <div class="grid">
      @for (item of items(); track item.id) {
        <figure class="tile card" [class.selected]="selectedIds().has(item.id)">
          <button
            type="button"
            class="tile-media"
            (click)="toggle(item, $event)"
            [attr.aria-pressed]="selectedIds().has(item.id)">
            @switch (item.mediaType) {
              @case ('video') {
                <span class="badge-kind">▶</span>
                <img class="placeholder" src="/assets/logo.png" alt="" />
              }
              @case ('file') {
                <span class="badge-kind">📄</span>
                <img class="placeholder" src="/assets/logo.png" alt="" />
              }
              @default {
                <img [src]="item.thumbUrl || item.imageUrl" alt="" loading="lazy" />
              }
            }
            <span class="check" aria-hidden="true">✓</span>
          </button>
          <figcaption>
            <span class="tile-caption">{{ item.captionEn || '—' }}</span>
            <span class="tile-actions">
              <a [href]="item.imageUrl" target="_blank" rel="noopener">{{ 'admin.media.openFile' | transloco }}</a>
              <button type="button" class="btn-link danger" (click)="deleteOne.emit(item)">
                {{ 'admin.media.delete' | transloco }}
              </button>
            </span>
          </figcaption>
        </figure>
      }
    </div>
  `,
  styles: `
    .bulk-bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.7rem;
      background: var(--yb-brown);
      color: var(--yb-cream);
      padding: 0.6rem 0.9rem;
      border-radius: var(--ad-radius, 10px);
      margin-bottom: 0.9rem;
      font-size: 0.9rem;

      .btn-link {
        color: var(--yb-yellow);

        &.danger {
          color: #ffb4a8;
        }
      }
    }

    .caption-form {
      background: #fff;
      padding: 0.9rem;
      margin-bottom: 0.9rem;
      display: flex;
      flex-wrap: wrap;
      gap: 0.6rem;
      align-items: center;
    }

    .caption-fields {
      display: flex;
      flex: 1;
      flex-wrap: wrap;
      gap: 0.5rem;

      input {
        flex: 1 1 180px;
        font: inherit;
        padding: 0.45rem 0.6rem;
        border: 1.5px solid var(--ad-border-strong, #d3c7ac);
        border-radius: 8px;
      }
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
      gap: 0.9rem;
    }

    .tile {
      background: #fff;
      margin: 0;
      overflow: hidden;
      transition: box-shadow 0.12s, border-color 0.12s;

      &.selected {
        border-color: var(--yb-blue);
        box-shadow: 0 0 0 2px var(--yb-blue);

        .check {
          opacity: 1;
          background: var(--yb-blue);
        }
      }
    }

    .tile-media {
      position: relative;
      display: block;
      width: 100%;
      padding: 0;
      border: none;
      background: #f0ece2;
      cursor: pointer;
      line-height: 0;

      img {
        width: 100%;
        height: 130px;
        object-fit: cover;
        display: block;
      }

      .placeholder {
        object-fit: contain;
        padding: 1.4rem;
        opacity: 0.5;
      }
    }

    .badge-kind {
      position: absolute;
      inset-block-start: 0.4rem;
      inset-inline-start: 0.4rem;
      background: rgba(0, 0, 0, 0.6);
      color: #fff;
      border-radius: 6px;
      font-size: 0.8rem;
      padding: 0.05rem 0.4rem;
      line-height: 1.5;
    }

    .check {
      position: absolute;
      inset-block-start: 0.4rem;
      inset-inline-end: 0.4rem;
      width: 20px;
      height: 20px;
      border-radius: 50%;
      background: rgba(0, 0, 0, 0.35);
      color: #fff;
      font-size: 0.72rem;
      line-height: 20px;
      text-align: center;
      opacity: 0;
      transition: opacity 0.12s;
    }

    .tile-media:hover .check {
      opacity: 0.75;
    }

    figcaption {
      padding: 0.5rem 0.6rem 0.6rem;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      font-size: 0.82rem;
    }

    .tile-caption {
      color: var(--ad-muted, #7c6a5c);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .tile-actions {
      display: flex;
      justify-content: space-between;
      gap: 0.5rem;

      a {
        font-size: 0.82rem;
      }

      .btn-link {
        font-size: 0.82rem;
      }
    }
  `
})
export class MediaGrid {
  readonly items = input.required<GalleryItemDto[]>();
  readonly folders = input.required<MediaFolderDto[]>();

  readonly bulkMove = output<{ ids: number[]; folderId: number | null }>();
  readonly bulkDelete = output<number[]>();
  readonly bulkCaptions = output<{ ids: number[]; captions: CaptionEdit }>();
  readonly deleteOne = output<GalleryItemDto>();

  readonly selectedIds = signal<Set<number>>(new Set());
  readonly captionsOpen = signal(false);

  moveTarget: number | null | undefined = undefined;
  captionEn = '';
  captionNl = '';
  captionAr = '';

  private lastClickedId: number | null = null;

  toggle(item: GalleryItemDto, event: MouseEvent) {
    const ids = new Set(this.selectedIds());

    if (event.shiftKey && this.lastClickedId !== null) {
      const list = this.items();
      const from = list.findIndex(i => i.id === this.lastClickedId);
      const to = list.findIndex(i => i.id === item.id);
      if (from !== -1 && to !== -1) {
        for (let i = Math.min(from, to); i <= Math.max(from, to); i++) ids.add(list[i].id);
        this.selectedIds.set(ids);
        return;
      }
    }

    if (ids.has(item.id)) ids.delete(item.id);
    else ids.add(item.id);
    this.lastClickedId = item.id;
    this.selectedIds.set(ids);
  }

  requestBulkDelete() {
    this.bulkDelete.emit([...this.selectedIds()]);
  }

  clearSelection() {
    this.selectedIds.set(new Set());
    this.captionsOpen.set(false);
    this.lastClickedId = null;
  }

  onMove(folderId: number | null | undefined) {
    if (folderId === undefined) return;
    this.bulkMove.emit({ ids: [...this.selectedIds()], folderId });
    this.moveTarget = undefined;
    this.clearSelection();
  }

  applyCaptions() {
    const captions: CaptionEdit = {};
    if (this.captionEn.trim()) captions.captionEn = this.captionEn.trim();
    if (this.captionNl.trim()) captions.captionNl = this.captionNl.trim();
    if (this.captionAr.trim()) captions.captionAr = this.captionAr.trim();
    if (Object.keys(captions).length === 0) return;

    this.bulkCaptions.emit({ ids: [...this.selectedIds()], captions });
    this.captionEn = this.captionNl = this.captionAr = '';
    this.clearSelection();
  }
}
