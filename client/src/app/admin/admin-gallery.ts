import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { GalleryItemDto } from '../core/models';

@Component({
  selector: 'app-admin-gallery',
  imports: [ReactiveFormsModule, TranslocoPipe],
  template: `
    <div class="admin-head">
      <h1>{{ 'admin.gallery.title' | transloco }}</h1>
    </div>

    <form class="card add-form" [formGroup]="form" (ngSubmit)="add()">
      <div class="field">
        <label>{{ 'admin.gallery.add' | transloco }}</label>
        <input type="file" accept="image/*" (change)="upload($event)" />
        @if (form.controls.imageUrl.value) {
          <img [src]="form.controls.imageUrl.value" class="preview" alt="" />
        }
      </div>
      <div class="grid-3">
        <div class="field">
          <label for="captionEn">{{ 'admin.gallery.captionEn' | transloco }}</label>
          <input id="captionEn" formControlName="captionEn" />
        </div>
        <div class="field">
          <label for="captionNl">{{ 'admin.gallery.captionNl' | transloco }}</label>
          <input id="captionNl" formControlName="captionNl" />
        </div>
        <div class="field">
          <label for="captionAr">{{ 'admin.gallery.captionAr' | transloco }}</label>
          <input id="captionAr" formControlName="captionAr" dir="rtl" />
        </div>
      </div>
      <div class="field small">
        <label for="sortOrder">{{ 'admin.gallery.sortOrder' | transloco }}</label>
        <input id="sortOrder" formControlName="sortOrder" type="number" />
      </div>
      <button class="btn btn-primary" type="submit" [disabled]="form.invalid || saving()">
        {{ 'admin.gallery.add' | transloco }}
      </button>
    </form>

    <div class="items-grid">
      @for (item of items(); track item.id) {
        <div class="card item-card">
          <img [src]="item.imageUrl" alt="" />
          <div class="item-body">
            <p>{{ item.captionEn }}</p>
            <button class="btn-link danger" (click)="remove(item)">
              {{ 'admin.gallery.delete' | transloco }}
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .add-form {
      padding: 1.6rem;
      margin-bottom: 2rem;
      max-width: 860px;
    }

    .grid-3 {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
    }

    .field.small {
      max-width: 160px;
    }

    .preview {
      max-height: 130px;
      width: auto;
      border-radius: 8px;
      margin-top: 0.5rem;
    }

    .items-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 1.2rem;
    }

    .item-card img {
      width: 100%;
      height: 160px;
      object-fit: cover;
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
  private fb = inject(FormBuilder);
  private transloco = inject(TranslocoService);

  readonly items = signal<GalleryItemDto[]>([]);
  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    imageUrl: ['', Validators.required],
    captionEn: [''],
    captionNl: [''],
    captionAr: [''],
    sortOrder: [0]
  });

  constructor() {
    this.load();
  }

  load() {
    this.api.getGallery().subscribe(items => this.items.set(items));
  }

  upload(input: Event) {
    const file = (input.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.api.adminUpload(file).subscribe(result =>
      this.form.controls.imageUrl.setValue(result.url));
  }

  add() {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.api.adminAddGalleryItem({ eventId: null, ...this.form.getRawValue() }).subscribe({
      next: () => {
        this.form.reset({ imageUrl: '', captionEn: '', captionNl: '', captionAr: '', sortOrder: 0 });
        this.saving.set(false);
        this.load();
      },
      error: () => this.saving.set(false)
    });
  }

  remove(item: GalleryItemDto) {
    if (!confirm(this.transloco.translate('admin.gallery.confirmDelete'))) return;
    this.api.adminDeleteGalleryItem(item.id).subscribe(() => this.load());
  }
}
