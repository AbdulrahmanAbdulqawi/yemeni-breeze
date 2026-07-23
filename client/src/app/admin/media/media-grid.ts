import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { Lang } from '../../core/language.service';
import { originalUrl } from '../../core/media-url';
import { GalleryItemDto, MediaFolderDto } from '../../core/models';
import { LangSelect } from '../ui/lang-select';

export interface CaptionEdit {
  captionEn?: string | null;
  captionNl?: string | null;
  captionAr?: string | null;
}

@Component({
  selector: 'app-media-grid',
  imports: [FormsModule, TranslocoPipe, LangSelect],
  template: `
    @if (items().length) {
      <div class="grid-toolbar" [class.active]="selectedIds().size > 0">
        <span class="toolbar-count">
          @if (selectedIds().size) {
            <strong>{{ selectedIds().size }}</strong> / {{ items().length }}
            {{ 'admin.media.selected' | transloco }}
          } @else {
            {{ items().length }} {{ 'admin.media.items' | transloco }}
          }
        </span>

        @if (selectedIds().size < items().length) {
          <button
            type="button"
            class="icon-btn"
            (click)="selectAll()"
            [title]="'admin.media.selectAll' | transloco"
            [attr.aria-label]="'admin.media.selectAll' | transloco">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <rect x="3" y="3" width="18" height="18" rx="4" />
              <path d="m8 12 3 3 6-6" />
            </svg>
          </button>
        }

        @if (selectedIds().size > 0) {
          <span class="icon-select">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="select-icon">
              <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7Z" />
            </svg>
            <select
              class="ad-select"
              [(ngModel)]="moveTarget"
              (ngModelChange)="onMove($event)"
              [title]="'admin.media.moveTo' | transloco"
              [attr.aria-label]="'admin.media.moveTo' | transloco">
              <option [ngValue]="undefined">{{ 'admin.media.moveTo' | transloco }}…</option>
              <option [ngValue]="null">{{ 'admin.media.unfiled' | transloco }}</option>
              @for (folder of folders(); track folder.id) {
                <option [ngValue]="folder.id">{{ folder.name }}</option>
              }
            </select>
          </span>

          <button
            type="button"
            class="icon-btn"
            (click)="captionsOpen.set(!captionsOpen())"
            [title]="'admin.media.editCaptions' | transloco"
            [attr.aria-label]="'admin.media.editCaptions' | transloco">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 20h9" />
              <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L8 18l-4 1 1-4Z" />
            </svg>
          </button>
          <button
            type="button"
            class="icon-btn danger"
            (click)="requestBulkDelete()"
            [title]="'admin.media.bulkDelete' | transloco"
            [attr.aria-label]="'admin.media.bulkDelete' | transloco">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M3 6h18" />
              <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
              <path d="m19 6-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
              <path d="M10 11v6M14 11v6" />
            </svg>
          </button>
          <button
            type="button"
            class="icon-btn"
            (click)="clearSelection()"
            [title]="'admin.media.clearSelection' | transloco"
            [attr.aria-label]="'admin.media.clearSelection' | transloco">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M18 6 6 18" />
              <path d="M6 6l12 12" />
            </svg>
          </button>
        }
      </div>

      @if (captionsOpen()) {
        <form class="caption-form card" (ngSubmit)="applyCaptions()">
          <app-lang-select [(active)]="captionLang" [label]="'admin.events.translation' | transloco" />
          <div class="caption-fields">
            @switch (captionLang()) {
              @case ('nl') {
                <input [(ngModel)]="captionNl" name="captionNl" placeholder="Bijschrift" />
              }
              @case ('ar') {
                <input [(ngModel)]="captionAr" name="captionAr" placeholder="التعليق" dir="rtl" />
              }
              @default {
                <input [(ngModel)]="captionEn" name="captionEn" placeholder="Caption" />
              }
            }
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
              @if (item.mediaType === 'image') {
                <a [href]="originalUrl(item.imageUrl)" download>{{ 'admin.media.downloadOriginal' | transloco }}</a>
              }
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
    .grid-toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.7rem;
      background: var(--ad-surface, #fff);
      border: 1px solid var(--ad-border, #e4dcc9);
      color: var(--ad-text, #3d2415);
      padding: 0.55rem 0.9rem;
      border-radius: var(--ad-radius, 10px);
      margin-bottom: 0.9rem;
      font-size: 0.9rem;
      transition: background 0.15s ease, color 0.15s ease;

      .btn-link {
        color: var(--yb-maroon);

        &.danger {
          color: var(--yb-red);
        }
      }

      /* Once something is selected, the bar carries the same weight as
         other destructive/bulk-action surfaces in the studio. */
      &.active {
        background: var(--yb-brown);
        border-color: var(--yb-brown);
        color: var(--yb-cream);

        .btn-link {
          color: var(--yb-yellow);

          &.danger {
            color: #ffb4a8;
          }
        }

        .icon-btn {
          color: var(--yb-cream);

          &:hover {
            background: rgb(253 249 234 / 0.15);
          }

          &.danger {
            color: #ffb4a8;
          }
        }

        .icon-select {
          color: var(--yb-cream);
        }
      }
    }

    .toolbar-count {
      font-weight: 600;
    }

    /* Icon-only actions — labelled for assistive tech and mouse users via
       title/aria-label rather than visible text, to keep the bar compact. */
    .icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 30px;
      height: 30px;
      padding: 0;
      border: none;
      border-radius: 7px;
      background: none;
      color: var(--yb-maroon);
      cursor: pointer;

      svg {
        width: 17px;
        height: 17px;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.9;
        stroke-linecap: round;
        stroke-linejoin: round;
      }

      &:hover {
        background: color-mix(in srgb, currentColor 12%, transparent);
      }

      &.danger {
        color: var(--yb-red);
      }
    }

    .icon-select {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      color: var(--yb-maroon);

      .select-icon {
        width: 16px;
        height: 16px;
        flex: none;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
        opacity: 0.8;
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
      flex-wrap: wrap;
      align-items: center;
      gap: 0.4rem 0.6rem;

      a {
        font-size: 0.82rem;
      }

      .btn-link {
        font-size: 0.82rem;
        margin-inline-start: auto;
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
  readonly captionLang = signal<Lang>('en');

  moveTarget: number | null | undefined = undefined;
  captionEn = '';
  captionNl = '';
  captionAr = '';

  private lastClickedId: number | null = null;

  readonly originalUrl = originalUrl;

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

  selectAll() {
    this.selectedIds.set(new Set(this.items().map(i => i.id)));
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
