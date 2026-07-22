import { Component, effect, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { LocalizedDatePipe } from '../../core/localized-date.pipe';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { combineLatest, map, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { GalleryItemDto, RegistrationStatus } from '../../core/models';
import { SeoService } from '../../core/seo.service';

@Component({
  selector: 'app-event-detail',
  imports: [ReactiveFormsModule, TranslocoPipe, LocalizedDatePipe, RouterLink],
  templateUrl: './event-detail.html',
  styleUrl: './event-detail.scss'
})
export class EventDetail {
  private api = inject(ApiService);
  private fb = inject(FormBuilder);
  readonly lang = inject(LanguageService);

  readonly slug = input.required<string>();

  private seo = inject(SeoService);

  private seoSync = effect(() => {
    const ev = this.event();
    this.lang.current(); // re-run on language switch
    if (ev) this.seo.setTags(this.lang.pick(ev, 'title'), this.lang.pick(ev, 'description').slice(0, 160));
  });

  readonly event = toSignal(
    toObservable(this.slug).pipe(switchMap(slug => this.api.getEvent(slug))),
    { initialValue: null }
  );

  readonly album = toSignal(
    combineLatest([toObservable(this.event), this.api.getGallery()]).pipe(
      map(([event, items]) => event ? items.filter(i => i.eventId === event.id) : [])
    ),
    { initialValue: [] as GalleryItemDto[] }
  );

  readonly lightbox = signal<GalleryItemDto | null>(null);

  readonly submitting = signal(false);
  readonly result = signal<RegistrationStatus | null>(null);
  readonly errorKey = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    guestsCount: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
    note: ['']
  });

  submit() {
    const event = this.event();
    if (!event || this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorKey.set(null);
    this.api.register(event.slug, { ...this.form.getRawValue(), language: this.lang.current() }).subscribe({
      next: response => {
        this.result.set(response.status);
        this.submitting.set(false);
      },
      error: err => {
        this.errorKey.set(err.status === 409 ? 'events.form.duplicate' : 'events.form.genericError');
        this.submitting.set(false);
      }
    });
  }
}
