import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { Lang } from '../core/language.service';
import { EventInput, EventStatus } from '../core/models';
import { ToastService } from './ui/toast.service';
import { PageHeader } from './ui/page-header';
import { LangTabs } from './ui/lang-tabs';

@Component({
  selector: 'app-admin-event-form',
  imports: [ReactiveFormsModule, TranslocoPipe, PageHeader, LangTabs],
  template: `
    <app-page-header [heading]="(isNew() ? 'admin.events.new' : 'admin.events.edit') | transloco" />

    <form class="card event-form" [formGroup]="form" (ngSubmit)="save()">
      <h2 class="form-section">{{ 'admin.events.sectionDetails' | transloco }}</h2>
      <div class="grid-2">
        <div class="field">
          <label for="slug">{{ 'admin.events.slug' | transloco }}</label>
          <input id="slug" formControlName="slug" placeholder="my-event-name" />
        </div>
        <div class="field">
          <label for="location">{{ 'admin.events.location' | transloco }}</label>
          <input id="location" formControlName="location" />
        </div>
      </div>

      <div class="lang-block">
        <div class="lang-block-head">
          <app-lang-tabs [(active)]="contentLang" />
          @if (missingLangs().length) {
            <span class="lang-missing">
              {{ 'admin.events.emptyIn' | transloco }}: {{ missingLangs().join(', ') }}
            </span>
          }
        </div>

        @switch (contentLang()) {
          @case ('nl') {
            <div class="field">
              <label for="titleNl">{{ 'admin.events.title' | transloco }}</label>
              <input id="titleNl" formControlName="titleNl" />
            </div>
            <div class="field">
              <label for="descriptionNl">{{ 'admin.events.description' | transloco }}</label>
              <textarea id="descriptionNl" formControlName="descriptionNl" rows="5"></textarea>
            </div>
          }
          @case ('ar') {
            <div class="field">
              <label for="titleAr">{{ 'admin.events.title' | transloco }}</label>
              <input id="titleAr" formControlName="titleAr" dir="rtl" />
            </div>
            <div class="field">
              <label for="descriptionAr">{{ 'admin.events.description' | transloco }}</label>
              <textarea id="descriptionAr" formControlName="descriptionAr" rows="5" dir="rtl"></textarea>
            </div>
          }
          @default {
            <div class="field">
              <label for="titleEn">{{ 'admin.events.title' | transloco }}</label>
              <input id="titleEn" formControlName="titleEn" />
            </div>
            <div class="field">
              <label for="descriptionEn">{{ 'admin.events.description' | transloco }}</label>
              <textarea id="descriptionEn" formControlName="descriptionEn" rows="5"></textarea>
            </div>
          }
        }
      </div>

      <h2 class="form-section">{{ 'admin.events.sectionSchedule' | transloco }}</h2>
      <div class="grid-4">
        <div class="field">
          <label for="date">{{ 'admin.events.date' | transloco }}</label>
          <input id="date" formControlName="date" type="date" />
        </div>
        <div class="field">
          <label for="startTime">{{ 'admin.events.startTime' | transloco }}</label>
          <input id="startTime" formControlName="startTime" type="time" />
        </div>
        <div class="field">
          <label for="endTime">{{ 'admin.events.endTime' | transloco }}</label>
          <input id="endTime" formControlName="endTime" type="time" />
        </div>
        <div class="field">
          <label for="capacity">{{ 'admin.events.capacity' | transloco }}</label>
          <input id="capacity" formControlName="capacity" type="number" min="1" />
        </div>
      </div>

      <h2 class="form-section">{{ 'admin.events.sectionPublishing' | transloco }}</h2>
      <div class="grid-2">
        <div class="field">
          <label for="status">{{ 'admin.events.status' | transloco }}</label>
          <select id="status" formControlName="status">
            <option value="Draft">Draft</option>
            <option value="Published">Published</option>
            <option value="Past">Past</option>
          </select>
        </div>
        <div class="field checkbox-field">
          <label>
            <input type="checkbox" formControlName="isRegistrationOpen" />
            {{ 'admin.events.registrationOpen' | transloco }}
          </label>
        </div>
      </div>

      <h2 class="form-section">{{ 'admin.events.sectionMedia' | transloco }}</h2>
      <div class="field cover-field">
        <label>{{ 'admin.events.image' | transloco }}</label>
        @if (form.controls.imageUrl.value; as cover) {
          <img [src]="cover" class="image-preview" alt="" />
          <button type="button" class="btn-link danger" (click)="clearImage()">
            {{ 'admin.gallery.removeImage' | transloco }}
          </button>
        }
        <input type="file" accept="image/*" (change)="upload($event)" [disabled]="uploading()" />
        @if (uploading()) {
          <span class="uploading-note">{{ 'admin.media.uploading' | transloco }}…</span>
        }
      </div>

      @if (error(); as message) {
        <p class="error-text">{{ message }}</p>
      }

      <div class="form-actions">
        <button class="btn btn-primary" type="submit" [disabled]="saving()">
          {{ 'admin.events.save' | transloco }}
        </button>
        <button class="btn btn-outline" type="button" (click)="cancel()">
          {{ 'admin.events.cancel' | transloco }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .event-form {
      padding: 1.6rem 1.8rem 1.8rem;
      max-width: 900px;
      background: #fff;
    }

    .form-section {
      margin: 1.6rem 0 0.9rem;
      padding-bottom: 0.4rem;
      border-bottom: 1px solid var(--ad-border, #e4dcc9);
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.07em;
      color: var(--ad-muted, #7c6a5c);

      &:first-child {
        margin-top: 0;
      }
    }

    .cover-field {
      align-items: flex-start;
    }

    .lang-block {
      border: 1px solid var(--ad-border, #e4dcc9);
      border-radius: var(--ad-radius, 10px);
      padding: 0.9rem 1rem 0.2rem;
      background: #fdfcf9;
      margin-bottom: 1rem;
    }

    .lang-block-head {
      display: flex;
      align-items: center;
      gap: 0.8rem;
      flex-wrap: wrap;
      margin-bottom: 0.9rem;
    }

    .lang-missing {
      font-size: 0.8rem;
      color: var(--yb-orange);
      font-weight: 600;
    }

    .uploading-note {
      font-size: 0.85rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .grid-2,
    .grid-3,
    .grid-4 {
      display: grid;
      gap: 1rem;
    }

    .grid-2 {
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    }

    .grid-3 {
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    }

    .grid-4 {
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
    }

    .checkbox-field {
      justify-content: end;

      label {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
    }

    .image-preview {
      max-height: 150px;
      width: auto;
      border-radius: 8px;
      margin-bottom: 0.5rem;
    }

    .form-actions {
      display: flex;
      gap: 1rem;
      margin-top: 1rem;
    }
  `
})
export class AdminEventForm {
  private api = inject(ApiService);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);

  readonly id = input<string>();
  readonly isNew = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly contentLang = signal<Lang>('en');

  /** Languages whose title is still blank — surfaced so a translation isn't missed. */
  readonly missingLangs = computed(() => {
    const value = this.formValue();
    if (!value) return [];
    return [
      ['English', value.titleEn],
      ['Nederlands', value.titleNl],
      ['العربية', value.titleAr]
    ].filter(([, title]) => !String(title ?? '').trim()).map(([label]) => label as string);
  });

  readonly form = this.fb.nonNullable.group({
    slug: ['', Validators.required],
    titleEn: ['', Validators.required],
    titleNl: [''],
    titleAr: [''],
    descriptionEn: [''],
    descriptionNl: [''],
    descriptionAr: [''],
    date: ['', Validators.required],
    startTime: [''],
    endTime: [''],
    location: [''],
    capacity: [100, [Validators.required, Validators.min(1)]],
    imageUrl: [null as string | null],
    status: ['Draft' as EventStatus],
    isRegistrationOpen: [false]
  });

  /** Live form value, so the "still empty in…" hint tracks typing. */
  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue()
  });

  constructor() {
    effect(() => {
      const id = this.id();
      if (id && id !== 'new') {
        this.isNew.set(false);
        this.api.adminGetEvents().subscribe(events => {
          const event = events.find(e => e.id === +id);
          if (event) this.form.patchValue(event);
        });
      }
    });
  }

  upload(input: Event) {
    const target = input.target as HTMLInputElement;
    const file = target.files?.[0];
    target.value = '';
    if (!file) return;

    this.uploading.set(true);
    this.api.adminUpload(file).subscribe({
      next: result => {
        this.form.controls.imageUrl.setValue(result.url);
        this.uploading.set(false);
      },
      error: () => {
        this.uploading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  clearImage() {
    this.form.controls.imageUrl.setValue(null);
  }

  save() {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue() as EventInput;
    const request = this.isNew()
      ? this.api.adminCreateEvent(value)
      : this.api.adminUpdateEvent(+this.id()!, value);
    request.subscribe({
      next: () => this.router.navigate(['/admin/events']),
      error: err => {
        this.error.set(err.error?.message ?? 'Error');
        this.saving.set(false);
      }
    });
  }

  cancel() {
    this.router.navigate(['/admin/events']);
  }
}
