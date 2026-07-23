import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocalizedDatePipe } from '../core/localized-date.pipe';
import { LanguageService } from '../core/language.service';
import { EventDto } from '../core/models';

/**
 * The two event-card treatments used across Home and the Events page:
 * `upcoming` (the featured next event, with meta rows and a Register CTA)
 * and `past` (photo + date badge, with a link into that event's album).
 */
@Component({
  selector: 'app-event-card',
  imports: [RouterLink, TranslocoPipe, LocalizedDatePipe],
  template: `
    <article class="ec card" [class.ec-past]="variant() === 'past'">
      <div class="ec-media">
        @if (event().imageUrl) {
          <img [src]="event().imageUrl" [alt]="lang.pick(event(), 'title')" />
        } @else {
          <div class="ec-media-placeholder">
            <img src="/assets/logo.png" alt="" />
          </div>
        }

        @if (variant() === 'past') {
          <span class="ec-date-badge">{{ event().date | localizedDate: 'medium' }}</span>
        }
      </div>

      <div class="ec-body">
        @if (variant() === 'upcoming') {
          <div class="ec-badges">
            <span class="ec-badge">{{ 'events.featured' | transloco }}</span>
            @if (event().spotsLeft > 0 && event().spotsLeft <= 20) {
              <span class="ec-badge-note">{{ 'events.limitedAvailability' | transloco }}</span>
            }
          </div>
        }

        <h3>{{ lang.pick(event(), 'title') }}</h3>

        @if (variant() === 'upcoming') {
          <ul class="ec-meta">
            <li>
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <rect x="3" y="5" width="18" height="16" rx="2" />
                <path d="M3 10h18M8 3v4M16 3v4" />
              </svg>
              {{ event().date | localizedDate: 'long' }}
            </li>
            <li>
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3 3" />
              </svg>
              {{ event().startTime }}–{{ event().endTime }}
            </li>
            <li>
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 21s7-6.1 7-11.5A7 7 0 0 0 5 9.5C5 14.9 12 21 12 21Z" />
                <circle cx="12" cy="9.5" r="2.3" />
              </svg>
              {{ event().location }}
            </li>
          </ul>

          <div class="ec-footer">
            @if (event().isRegistrationOpen) {
              <div class="ec-spots">
                @if (event().spotsLeft > 0) {
                  <strong>{{ event().spotsLeft }} {{ 'events.spotsLeft' | transloco }}</strong>
                } @else {
                  <strong class="waitlist">{{ 'events.waitlistOpen' | transloco }}</strong>
                }
                <span>{{ 'events.reservationRequired' | transloco }}</span>
              </div>
            } @else {
              <div class="ec-spots">
                <strong class="closed">{{ 'events.registrationClosed' | transloco }}</strong>
              </div>
            }
            <a [routerLink]="['/events', event().slug]" class="btn btn-primary ec-cta">
              @if (event().isRegistrationOpen && event().spotsLeft > 0) {
                <span class="cta-swap">
                  <span class="cta-default">{{ 'events.register' | transloco }}</span>
                  <span class="cta-hover">{{ 'events.form.submit' | transloco }}</span>
                </span>
              } @else {
                {{ (event().isRegistrationOpen ? 'events.joinWaitlist' : 'events.details') | transloco }}
              }
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M5 12h14M13 6l6 6-6 6" />
              </svg>
            </a>
          </div>
        } @else {
          <p class="ec-location">{{ event().location }}</p>
          <p class="ec-desc">{{ lang.pick(event(), 'description') }}</p>
          <a [routerLink]="['/events', event().slug]" class="ec-gallery-link">
            {{ 'events.viewGallery' | transloco }}
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </a>
        }
      </div>
    </article>
  `,
  styles: `
    .ec {
      display: flex;
      flex-direction: column;
      margin: 0;
      overflow: hidden;
    }

    .ec-media {
      position: relative;
      line-height: 0;

      img {
        width: 100%;
        height: 220px;
        object-fit: cover;
        display: block;
      }
    }

    .ec-media-placeholder {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 220px;
      background: var(--yb-brown);

      img {
        width: auto;
        height: 110px;
      }
    }

    .ec-past .ec-media img,
    .ec-past .ec-media-placeholder {
      filter: saturate(0.55);
    }

    .ec-date-badge {
      position: absolute;
      inset-block-start: 0.8rem;
      inset-inline-start: 0.8rem;
      background: rgb(49 15 2 / 0.85);
      color: var(--yb-cream);
      font-size: 0.72rem;
      font-weight: 700;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      padding: 0.3rem 0.7rem;
      border-radius: 999px;
    }

    .ec-body {
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      flex: 1;

      h3 {
        margin: 0;
      }
    }

    .ec-badges {
      display: flex;
      align-items: center;
      gap: 0.7rem;
      flex-wrap: wrap;
    }

    .ec-badge {
      background: var(--yb-gold);
      color: var(--yb-brown);
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      padding: 0.25rem 0.65rem;
      border-radius: 999px;
    }

    .ec-badge-note {
      font-size: 0.85rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .ec-meta {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.45rem;

      li {
        display: flex;
        align-items: center;
        gap: 0.55rem;
        color: var(--yb-brown-deep);
        font-weight: 600;
        font-size: 0.94rem;
      }

      svg {
        width: 17px;
        height: 17px;
        flex: none;
        fill: none;
        stroke: var(--yb-maroon);
        stroke-width: 1.8;
        stroke-linecap: round;
        stroke-linejoin: round;
      }
    }

    .ec-footer {
      margin-top: auto;
      padding-top: 0.8rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
    }

    .ec-spots {
      display: flex;
      flex-direction: column;
      line-height: 1.3;

      strong {
        color: var(--yb-green);
        font-size: 0.95rem;

        &.waitlist {
          color: var(--yb-orange);
        }

        &.closed {
          color: var(--yb-maroon);
          opacity: 0.75;
        }
      }

      span {
        font-size: 0.8rem;
        color: var(--ad-muted, #7c6a5c);
      }
    }

    .ec-cta {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;

      /* "Register" previews the wording of the form it leads to on hover,
         swapping to the exact label the submit button itself uses.
         Both labels sit in the same grid cell so the wider one ("Confirm
         registration") sets the box size and nothing reflows on hover. */
      .cta-swap {
        position: relative;
        display: inline-grid;

        span {
          grid-area: 1 / 1;
          transition: opacity 0.15s ease;
          white-space: nowrap;
        }

        .cta-hover {
          opacity: 0;
        }
      }

      /* Selectors repeat the .cta-swap ancestor so specificity matches (and
         beats) the base ".cta-swap .cta-hover { opacity: 0 }" rule above —
         Angular's per-component [_ngcontent] attribute is appended to every
         class in a rule, so a shorter hover selector here would otherwise
         lose the specificity tie and never show the hover text. */
      &:hover .cta-swap .cta-default {
        opacity: 0;
      }

      &:hover .cta-swap .cta-hover {
        opacity: 1;
      }

      svg {
        width: 16px;
        height: 16px;
        fill: none;
        stroke: currentColor;
        stroke-width: 2;
        stroke-linecap: round;
        stroke-linejoin: round;
        transition: transform 0.15s ease;
      }

      &:hover svg {
        transform: translateX(3px);
      }

      /* :host-context (not "[dir='rtl'] &") because dir="rtl" lives on <html>,
         outside this component — see gallery-page.scss for the full note. */
      :host-context([dir='rtl']) &:hover svg {
        transform: translateX(-3px) scaleX(-1);
      }

      :host-context([dir='rtl']) & svg {
        transform: scaleX(-1);
      }
    }

    .ec-location {
      margin: 0;
      color: var(--yb-gold-dim, var(--yb-orange));
      font-weight: 700;
      font-size: 0.88rem;
    }

    .ec-desc {
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      overflow: hidden;
      margin: 0;
      color: var(--ad-muted, #52443f);
    }

    .ec-gallery-link {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      margin-top: auto;
      padding-top: 0.4rem;
      font-weight: 700;
      color: var(--yb-maroon);
      text-decoration: none;

      svg {
        width: 15px;
        height: 15px;
        fill: none;
        stroke: currentColor;
        stroke-width: 2;
        stroke-linecap: round;
        stroke-linejoin: round;
      }

      &:hover {
        color: var(--yb-red);
      }

      :host-context([dir='rtl']) & svg {
        transform: scaleX(-1);
      }
    }
  `
})
export class EventCard {
  readonly event = input.required<EventDto>();
  readonly variant = input<'upcoming' | 'past'>('upcoming');

  readonly lang = inject(LanguageService);
}
