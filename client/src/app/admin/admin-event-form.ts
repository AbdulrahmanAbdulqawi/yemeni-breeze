import { Component, effect, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { EventInput, EventStatus } from '../core/models';

@Component({
  selector: 'app-admin-event-form',
  imports: [ReactiveFormsModule, TranslocoPipe],
  template: `
    <h1>{{ (isNew() ? 'admin.events.new' : 'admin.events.edit') | transloco }}</h1>

    <form class="card event-form" [formGroup]="form" (ngSubmit)="save()">
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

      <div class="grid-3">
        <div class="field">
          <label for="titleEn">{{ 'admin.events.titleEn' | transloco }}</label>
          <input id="titleEn" formControlName="titleEn" />
        </div>
        <div class="field">
          <label for="titleNl">{{ 'admin.events.titleNl' | transloco }}</label>
          <input id="titleNl" formControlName="titleNl" />
        </div>
        <div class="field">
          <label for="titleAr">{{ 'admin.events.titleAr' | transloco }}</label>
          <input id="titleAr" formControlName="titleAr" dir="rtl" />
        </div>
      </div>

      <div class="field">
        <label for="descriptionEn">{{ 'admin.events.descriptionEn' | transloco }}</label>
        <textarea id="descriptionEn" formControlName="descriptionEn" rows="4"></textarea>
      </div>
      <div class="field">
        <label for="descriptionNl">{{ 'admin.events.descriptionNl' | transloco }}</label>
        <textarea id="descriptionNl" formControlName="descriptionNl" rows="4"></textarea>
      </div>
      <div class="field">
        <label for="descriptionAr">{{ 'admin.events.descriptionAr' | transloco }}</label>
        <textarea id="descriptionAr" formControlName="descriptionAr" rows="4" dir="rtl"></textarea>
      </div>

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

      <div class="field">
        <label>{{ 'admin.events.image' | transloco }}</label>
        @if (form.controls.imageUrl.value) {
          <img [src]="form.controls.imageUrl.value" class="image-preview" alt="" />
        }
        <input type="file" accept="image/*" (change)="upload($event)" />
        @if (uploading()) {
          <span>…</span>
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
      padding: 2rem;
      max-width: 900px;
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

  readonly id = input<string>();
  readonly isNew = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);

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
    const file = (input.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.api.adminUpload(file).subscribe({
      next: result => {
        this.form.controls.imageUrl.setValue(result.url);
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false)
    });
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
