import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { MediaFolderDto } from '../../core/models';

/** `null` = all media, `'unfiled'` = items with no folder, number = a folder id. */
export type FolderSelection = null | 'unfiled' | number;

@Component({
  selector: 'app-folder-rail',
  imports: [FormsModule, TranslocoPipe],
  template: `
    <nav class="rail">
      <p class="rail-label">{{ 'admin.media.folders' | transloco }}</p>

      <button type="button" class="rail-item" [class.active]="selected() === null" (click)="select.emit(null)">
        <span class="rail-name">{{ 'admin.media.allMedia' | transloco }}</span>
        <span class="rail-count">{{ totalCount() }}</span>
      </button>

      <button type="button" class="rail-item" [class.active]="selected() === 'unfiled'" (click)="select.emit('unfiled')">
        <span class="rail-name">{{ 'admin.media.unfiled' | transloco }}</span>
        <span class="rail-count">{{ unfiledCount() }}</span>
      </button>

      <hr />

      @for (folder of folders(); track folder.id) {
        @if (editingId() === folder.id) {
          <form class="rail-edit" (ngSubmit)="commitRename(folder)">
            <input [(ngModel)]="draftName" name="name" autofocus />
            <button type="submit" class="btn-link">{{ 'admin.media.save' | transloco }}</button>
            <button type="button" class="btn-link" (click)="editingId.set(null)">
              {{ 'admin.media.cancel' | transloco }}
            </button>
          </form>
        } @else {
          <div class="rail-row" [class.active]="selected() === folder.id">
            <button type="button" class="rail-item" (click)="select.emit(folder.id)">
              @if (folder.eventId !== null) {
                <span class="event-dot" title="Event folder"></span>
              }
              <span class="rail-name">{{ folder.name }}</span>
              <span class="rail-count">{{ folder.itemCount }}</span>
            </button>
            <div class="rail-actions">
              <button type="button" (click)="startRename(folder)" [title]="'admin.media.renameFolder' | transloco">✎</button>
              <button type="button" (click)="deleteFolder.emit(folder)" [title]="'admin.media.deleteFolder' | transloco">🗑</button>
            </div>
          </div>
        }
      }

      @if (creating()) {
        <form class="rail-edit" (ngSubmit)="commitCreate()">
          <input
            [(ngModel)]="draftName"
            name="newName"
            [placeholder]="'admin.media.folderName' | transloco"
            autofocus />
          <button type="submit" class="btn-link">{{ 'admin.media.create' | transloco }}</button>
          <button type="button" class="btn-link" (click)="creating.set(false)">
            {{ 'admin.media.cancel' | transloco }}
          </button>
        </form>
      } @else {
        <button type="button" class="rail-new" (click)="startCreate()">
          + {{ 'admin.media.newFolder' | transloco }}
        </button>
      }
    </nav>
  `,
  styles: `
    .rail {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
    }

    .rail-label {
      margin: 0 0 0.4rem;
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: var(--ad-muted, #7c6a5c);
      font-weight: 700;
    }

    hr {
      border: none;
      border-top: 1px solid var(--ad-border, #e4dcc9);
      margin: 0.5rem 0;
    }

    .rail-row {
      display: flex;
      align-items: center;
      border-radius: 8px;

      &.active,
      &:hover {
        background: #fff;
      }

      &:hover .rail-actions {
        opacity: 1;
      }
    }

    .rail-item {
      flex: 1;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      background: none;
      border: none;
      font: inherit;
      text-align: start;
      color: inherit;
      cursor: pointer;
      padding: 0.45rem 0.6rem;
      border-radius: 8px;
      min-width: 0;

      &.active {
        background: #fff;
        font-weight: 700;
      }

      &:hover {
        background: #fff;
      }
    }

    .rail-name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .rail-count {
      margin-inline-start: auto;
      font-size: 0.78rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .event-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--yb-orange);
      flex: none;
    }

    .rail-actions {
      display: flex;
      gap: 0.1rem;
      opacity: 0;
      transition: opacity 0.12s;
      padding-inline-end: 0.3rem;

      button {
        background: none;
        border: none;
        cursor: pointer;
        font-size: 0.85rem;
        padding: 0.2rem;
        opacity: 0.7;

        &:hover {
          opacity: 1;
        }
      }
    }

    .rail-edit {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      align-items: center;
      padding: 0.35rem 0.2rem;

      input {
        flex: 1 1 100%;
        font: inherit;
        padding: 0.35rem 0.5rem;
        border: 1.5px solid var(--yb-blue);
        border-radius: 6px;
      }
    }

    .rail-new {
      background: none;
      border: 1.5px dashed var(--ad-border-strong, #d3c7ac);
      border-radius: 8px;
      padding: 0.45rem;
      margin-top: 0.5rem;
      cursor: pointer;
      font: inherit;
      font-size: 0.88rem;
      color: var(--ad-muted, #7c6a5c);

      &:hover {
        border-color: var(--yb-maroon);
        color: var(--yb-maroon);
      }
    }
  `
})
export class FolderRail {
  readonly folders = input.required<MediaFolderDto[]>();
  readonly selected = input.required<FolderSelection>();
  readonly totalCount = input.required<number>();
  readonly unfiledCount = input.required<number>();

  readonly select = output<FolderSelection>();
  readonly createFolder = output<string>();
  readonly renameFolder = output<{ folder: MediaFolderDto; name: string }>();
  readonly deleteFolder = output<MediaFolderDto>();

  readonly editingId = signal<number | null>(null);
  readonly creating = signal(false);
  draftName = '';

  startCreate() {
    this.draftName = '';
    this.editingId.set(null);
    this.creating.set(true);
  }

  commitCreate() {
    const name = this.draftName.trim();
    if (name) this.createFolder.emit(name);
    this.creating.set(false);
    this.draftName = '';
  }

  startRename(folder: MediaFolderDto) {
    this.creating.set(false);
    this.draftName = folder.name;
    this.editingId.set(folder.id);
  }

  commitRename(folder: MediaFolderDto) {
    const name = this.draftName.trim();
    if (name && name !== folder.name) this.renameFolder.emit({ folder, name });
    this.editingId.set(null);
  }
}
