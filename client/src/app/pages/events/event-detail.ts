import { Component, HostListener, computed, effect, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { LocalizedDatePipe } from '../../core/localized-date.pipe';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { combineLatest, map, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { originalUrl } from '../../core/media-url';
import { GalleryItemDto, RegistrationStatus } from '../../core/models';
import { SeoService } from '../../core/seo.service';
import { Reveal } from '../../shared/motion/reveal.directive';

type YesNo = 'yes' | 'no';

@Component({
  selector: 'app-event-detail',
  imports: [ReactiveFormsModule, TranslocoPipe, LocalizedDatePipe, RouterLink, Reveal, NgTemplateOutlet],
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

  /** "What to expect" items in the visitor's language (falls back to English), one per line. */
  readonly whatToExpect = computed(() => {
    const ev = this.event();
    if (!ev) return [];
    return this.lang.pick(ev, 'whatToExpect')
      .split('\n')
      .map(line => line.trim().replace(/^[-•*]\s*/, ''))
      .filter(Boolean);
  });

  /** The event's optional donation link, shown as a voluntary "support us" box under the form and after registering. */
  readonly donationUrl = computed(() => {
    const url = this.event()?.donationUrl?.trim();
    return url && /^https?:\/\//i.test(url) ? url : null;
  });

  readonly album = toSignal(
    combineLatest([toObservable(this.event), this.api.getGallery()]).pipe(
      map(([event, items]) => event ? items.filter(i => i.eventId === event.id) : [])
    ),
    { initialValue: [] as GalleryItemDto[] }
  );

  // --- Lightbox: image-only subset of the album, navigable by index ---

  readonly originalUrl = originalUrl;

  readonly lightboxImages = computed(() =>
    this.album().filter(item => item.mediaType !== 'video' && item.mediaType !== 'file'));

  readonly lightboxIndex = signal<number | null>(null);

  readonly lightbox = computed(() => {
    const list = this.lightboxImages();
    const index = this.lightboxIndex();
    return index !== null && index >= 0 && index < list.length ? list[index] : null;
  });

  openLightbox(item: GalleryItemDto) {
    const index = this.lightboxImages().findIndex(i => i.id === item.id);
    this.lightboxIndex.set(index === -1 ? null : index);
  }

  closeLightbox() {
    this.lightboxIndex.set(null);
  }

  step(delta: number) {
    const list = this.lightboxImages();
    const index = this.lightboxIndex();
    if (!list.length || index === null) return;
    this.lightboxIndex.set((index + delta + list.length) % list.length);
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (this.lightboxIndex() === null) return;
    const rtl = this.lang.current() === 'ar';
    if (event.key === 'Escape') this.closeLightbox();
    else if (event.key === 'ArrowRight') this.step(rtl ? -1 : 1);
    else if (event.key === 'ArrowLeft') this.step(rtl ? 1 : -1);
  }

  private touchStartX = 0;

  onTouchStart(event: TouchEvent) {
    this.touchStartX = event.touches[0].clientX;
  }

  onTouchEnd(event: TouchEvent) {
    const delta = event.changedTouches[0].clientX - this.touchStartX;
    if (Math.abs(delta) < 40) return;
    const rtl = this.lang.current() === 'ar';
    this.step(delta < 0 ? (rtl ? -1 : 1) : rtl ? 1 : -1);
  }

  readonly submitting = signal(false);
  readonly result = signal<RegistrationStatus | null>(null);
  /** For a personal thank-you after submitting ("Thank you, Sara!"). */
  readonly firstName = signal('');
  readonly errorKey = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    country: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.maxLength(50)]],
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
    bringingGuests: ['no' as YesNo],
    // Only asked (and validated) when bringingGuests is 'yes'; see bringingGuestsSync.
    extraGuests: [1],
    guestNames: [''],
    vegetarian: ['' as YesNo | ''],
    dietaryRestrictions: ['', Validators.maxLength(500)],
    wantsUpdates: ['' as YesNo | ''],
    // Honeypot: visually hidden, so only bots fill it in. The server quietly drops those.
    website: ['']
  });

  readonly bringingGuests = toSignal(this.form.controls.bringingGuests.valueChanges,
    { initialValue: this.form.controls.bringingGuests.value });

  private bringingGuestsSync = effect(() => {
    const { extraGuests, guestNames } = this.form.controls;
    if (this.bringingGuests() === 'yes') {
      extraGuests.setValidators([Validators.required, Validators.min(1), Validators.max(9)]);
      guestNames.setValidators([Validators.required, Validators.maxLength(500)]);
    } else {
      extraGuests.clearValidators();
      guestNames.clearValidators();
    }
    extraGuests.updateValueAndValidity();
    guestNames.updateValueAndValidity();
  });

  submit() {
    const event = this.event();
    if (!event || this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorKey.set(null);
    const value = this.form.getRawValue();
    const bringsGuests = value.bringingGuests === 'yes';
    this.api.register(event.slug, {
      fullName: value.fullName,
      email: value.email,
      country: value.country,
      phone: value.phone,
      reason: value.reason,
      guestsCount: bringsGuests ? 1 + value.extraGuests : 1,
      guestNames: bringsGuests ? value.guestNames : null,
      isVegetarian: value.vegetarian === '' ? null : value.vegetarian === 'yes',
      dietaryRestrictions: value.dietaryRestrictions,
      wantsUpdates: value.wantsUpdates === 'yes',
      website: value.website,
      language: this.lang.current()
    }).subscribe({
      next: response => {
        this.firstName.set(value.fullName.trim().split(/\s+/)[0]);
        this.result.set(response.status);
        this.submitting.set(false);
      },
      error: err => {
        this.errorKey.set(
          err.status === 409 ? 'events.form.duplicate'
            : err.status === 429 ? 'events.form.tooMany'
              : err.status === 400 ? 'events.form.checkFields'
                : 'events.form.genericError');
        this.submitting.set(false);
      }
    });
  }
}
