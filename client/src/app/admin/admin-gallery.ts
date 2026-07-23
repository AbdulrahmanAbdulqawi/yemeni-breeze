import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable, forkJoin, switchMap } from 'rxjs';
import { ApiService } from '../core/api.service';
import { EventDto, GalleryItemDto, MediaType, SiteSettings } from '../core/models';

@Component({
  selector: 'app-admin-gallery',
  imports: [FormsModule, TranslocoPipe],
  template: `
    <div class="admin-head">
      <h1>{{ 'admin.gallery.title' | transloco }}</h1>
    </div>

    <!-- Branding images -->
    <div class="card branding-card">
      <h3>{{ 'admin.gallery.branding' | transloco }}</h3>
      <div class="branding-grid">
        <div class="branding-slot">
          <label>{{ 'admin.gallery.heroImage' | transloco }}</label>
          @if (settings()['heroImageUrl']; as url) {
            <img [src]="url" alt="" />
            <button type="button" class="btn-link danger" (click)="removeBranding('heroImageUrl')">
              {{ 'admin.gallery.removeImage' | transloco }}
            </button>
          }
          <input type="file" accept="image/*" (change)="uploadBranding($event, 'heroImageUrl')" />
        </div>
        <div class="branding-slot">
          <label>{{ 'admin.gallery.aboutImage' | transloco }}</label>
          @if (settings()['aboutImageUrl']; as url) {
            <img [src]="url" alt="" />
            <button type="button" class="btn-link danger" (click)="removeBranding('aboutImageUrl')">
              {{ 'admin.gallery.removeImage' | transloco }}
            </button>
          }
          <input type="file" accept="image/*" (change)="uploadBranding($event, 'aboutImageUrl')" />
        </div>
      </div>
    </div>

    <!-- Multi-upload drop zone -->
    <div
      class="dropzone"
      [class.dragover]="dragging()"
      (dragover)="$event.preventDefault(); dragging.set(true)"
      (dragleave)="dragging.set(false)"
      (drop)="onDrop($event)">
      <p class="dz-title">{{ 'admin.gallery.dropTitle' | transloco }}</p>
      <p>{{ 'admin.gallery.dropOr' | transloco }}</p>
      <input type="file" accept="image/*,video/*,application/pdf" multiple (change)="onPick($event)" />
      <div class="dz-options">
        <label>
          {{ 'admin.gallery.attachToEvent' | transloco }}
          <select [(ngModel)]="selectedEventId">
            <option [ngValue]="null">—</option>
            @for (event of events(); track event.id) {
              <option [ngValue]="event.id">{{ event.titleEn }}</option>
            }
          </select>
        </label>
      </div>
      @if (uploading()) {
        <p class="dz-progress">{{ 'admin.gallery.uploading' | transloco }} ({{ uploadCount() }})</p>
      }
    </div>

    <div class="items-grid">
      @for (item of items(); track item.id) {
        <div class="card item-card">
          @switch (item.mediaType) {
            @case ('video') {
              <video [src]="item.imageUrl" controls preload="metadata"></video>
            }
            @case ('file') {
              <a class="file-tile" [href]="item.imageUrl" target="_blank" rel="noopener">📄 {{ 'admin.gallery.openFile' | transloco }}</a>
            }
            @default {
              <img [src]="item.thumbUrl || item.imageUrl" alt="" />
            }
          }
          <div class="item-body">
            <p>
              {{ item.captionEn }}
              @if (eventTitle(item.eventId); as title) {
                <span class="event-tag">{{ title }}</span>
              }
            </p>
            <button class="btn-link danger" (click)="remove(item)">
              {{ 'admin.gallery.delete' | transloco }}
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .branding-card {
      padding: 1.4rem 1.6rem;
      margin-bottom: 1.6rem;
      max-width: 860px;

      h3 {
        margin-bottom: 0.8rem;
      }
    }

    .branding-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 1.2rem;
    }

    .branding-slot {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;

      label {
        font-weight: 600;
        color: var(--yb-brown);
      }

      img {
        max-height: 110px;
        width: auto;
        border-radius: 8px;
        object-fit: cover;
      }
    }

    .dropzone {
      border: 2.5px dashed var(--yb-blue);
      border-radius: var(--yb-radius);
      background: #fff;
      padding: 2rem;
      text-align: center;
      margin-bottom: 2rem;
      max-width: 860px;
      transition: background 0.15s ease;

      &.dragover {
        background: #eef;
        border-style: solid;
      }

      .dz-title {
        font-family: var(--yb-font-heading);
        font-size: 1.15rem;
        color: var(--yb-maroon);
        margin: 0 0 0.3rem;
      }

      .dz-options {
        margin-top: 1rem;

        select {
          margin-inline-start: 0.6rem;
          padding: 0.35rem 0.6rem;
          border-radius: 8px;
          border: 1.5px solid #d9c9a8;
        }
      }

      .dz-progress {
        color: var(--yb-blue);
        font-weight: 700;
      }
    }

    .items-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 1.2rem;
    }

    .item-card img,
    .item-card video {
      width: 100%;
      height: 160px;
      object-fit: cover;
      display: block;
      background: #000;
    }

    .file-tile {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 160px;
      background: var(--yb-cream);
      color: var(--yb-brown);
      font-weight: 700;
      text-decoration: none;
      gap: 0.4rem;
    }

    .item-body {
      padding: 0.8rem 1rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.6rem;

      p {
        margin: 0;
      }
    }

    .event-tag {
      display: inline-block;
      background: var(--yb-cream);
      color: var(--yb-brown);
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 700;
      padding: 0.05rem 0.6rem;
      margin-inline-start: 0.4rem;
    }

    .btn-link {
      background: none;
      border: none;
      font-weight: 600;
      cursor: pointer;
      text-decoration: underline;
      padding: 0;

      &.danger {
        color: var(--yb-red);
      }
    }
  `
})
export class AdminGallery {
  private api = inject(ApiService);
  private transloco = inject(TranslocoService);

  readonly items = signal<GalleryItemDto[]>([]);
  readonly events = signal<EventDto[]>([]);
  readonly settings = signal<SiteSettings>({});
  readonly dragging = signal(false);
  readonly uploading = signal(false);
  readonly uploadCount = signal(0);
  selectedEventId: number | null = null;

  constructor() {
    this.load();
    this.api.adminGetEvents().subscribe(events => this.events.set(events));
    this.api.getSettings().subscribe(settings => this.settings.set(settings));
  }

  load() {
    this.api.getGallery().subscribe(items => this.items.set(items));
  }

  eventTitle(eventId: number | null): string | null {
    if (eventId === null) return null;
    return this.events().find(e => e.id === eventId)?.titleEn ?? null;
  }

  onDrop(drop: DragEvent) {
    drop.preventDefault();
    this.dragging.set(false);
    const files = Array.from(drop.dataTransfer?.files ?? []);
    if (files.length) this.uploadFiles(files);
  }

  onPick(change: Event) {
    const input = change.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (files.length) this.uploadFiles(files);
    input.value = '';
  }

  private uploadFiles(files: File[]) {
    this.uploading.set(true);
    this.uploadCount.set(files.length);
    const base = this.items().length;
    const images = files.filter(f => f.type.startsWith('image/'));
    const others = files.filter(f => !f.type.startsWith('image/'));

    const tasks: Observable<GalleryItemDto>[] = [];

    // Images are batch-resized to WebP, then registered as gallery items
    if (images.length) {
      tasks.push(this.api.adminUploadBatch(images).pipe(
        switchMap(results => forkJoin(results.map((r, i) =>
          this.api.adminAddGalleryItem(this.newItem(r.url, r.thumbUrl, 'image', base + i)))))
      ) as unknown as Observable<GalleryItemDto>);
    }

    // Videos / files are stored as-is
    others.forEach((file, i) => {
      tasks.push(this.api.adminUploadFile(file).pipe(
        switchMap(res => this.api.adminAddGalleryItem(
          this.newItem(res.url, null, res.kind, base + images.length + i)))
      ));
    });

    if (!tasks.length) {
      this.uploading.set(false);
      return;
    }
    forkJoin(tasks).subscribe({
      next: () => {
        this.uploading.set(false);
        this.load();
      },
      error: () => this.uploading.set(false)
    });
  }

  private newItem(imageUrl: string, thumbUrl: string | null, mediaType: MediaType, sortOrder: number) {
    return {
      eventId: this.selectedEventId,
      imageUrl,
      thumbUrl,
      mediaType,
      captionEn: '', captionNl: '', captionAr: '',
      sortOrder
    };
  }

  uploadBranding(change: Event, key: 'heroImageUrl' | 'aboutImageUrl') {
    const file = (change.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.api.adminUpload(file).subscribe(result =>
      this.api.adminUpdateSettings({ [key]: result.url })
        .subscribe(settings => this.settings.set(settings)));
  }

  removeBranding(key: 'heroImageUrl' | 'aboutImageUrl') {
    this.api.adminUpdateSettings({ [key]: '' })
      .subscribe(settings => this.settings.set(settings));
  }

  remove(item: GalleryItemDto) {
    if (!confirm(this.transloco.translate('admin.gallery.confirmDelete'))) return;
    this.api.adminDeleteGalleryItem(item.id).subscribe(() => this.load());
  }
}
