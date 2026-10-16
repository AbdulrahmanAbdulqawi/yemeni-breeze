import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { LocalizedDatePipe } from '../../core/localized-date.pipe';
import { ManagedRegistration } from '../../core/models';

type PageState = 'loading' | 'ready' | 'notFound' | 'error';

/**
 * Reached from the "Cancel your registration" link in emails. Opening the link only shows the
 * registration — cancelling takes a deliberate click, so mail scanners that prefetch links
 * can't cancel anyone.
 */
@Component({
  selector: 'app-manage-registration',
  imports: [TranslocoPipe, LocalizedDatePipe, RouterLink],
  template: `
    <section class="section-dark manage-head">
      <div class="container">
        <h1>{{ 'manage.title' | transloco }}</h1>
      </div>
    </section>

    <section class="section">
      <div class="container manage-wrap">
        <div class="card card-form manage-card">
          @switch (state()) {
            @case ('loading') {
              <p class="muted">{{ 'manage.loading' | transloco }}</p>
            }
            @case ('notFound') {
              <h2>{{ 'manage.notFoundTitle' | transloco }}</h2>
              <p>{{ 'manage.notFoundText' | transloco }}</p>
              <a routerLink="/events" class="btn btn-outline">{{ 'nav.events' | transloco }}</a>
            }
            @case ('error') {
              <p class="error-text">{{ 'manage.error' | transloco }}</p>
            }
            @case ('ready') {
              @if (registration(); as r) {
                @if (justCancelled()) {
                  <div class="result" animate.enter="yb-rise-in">
                    <span class="result-icon">🤍</span>
                    <h2>{{ 'manage.cancelledTitle' | transloco }}</h2>
                    <p>{{ 'manage.cancelledText' | transloco }}</p>
                  </div>
                }

                <h2 class="event-title">{{ title() }}</h2>
                <ul class="facts">
                  <li><span>{{ 'events.date' | transloco }}</span>{{ r.event.date | localizedDate: 'full' }}</li>
                  @if (r.event.startTime) {
                    <li><span>{{ 'events.time' | transloco }}</span><bdi dir="ltr">{{ r.event.startTime }}–{{ r.event.endTime }}</bdi></li>
                  }
                  @if (r.event.location) {
                    <li><span>{{ 'events.location' | transloco }}</span>{{ r.event.location }}</li>
                  }
                  <li><span>{{ 'manage.name' | transloco }}</span>{{ r.fullName }}</li>
                  <li><span>{{ 'manage.guests' | transloco }}</span>{{ r.guestsCount }}</li>
                  <li>
                    <span>{{ 'manage.status' | transloco }}</span>
                    <strong [class]="'status status-' + r.status.toLowerCase()">
                      {{ 'manage.status' + r.status | transloco }}
                    </strong>
                  </li>
                </ul>

                @if (r.canCancel) {
                  @if (!confirming()) {
                    <p class="hint">{{ 'manage.cancelHint' | transloco }}</p>
                    <div class="actions">
                      <button class="btn btn-outline danger" type="button" (click)="confirming.set(true)">
                        {{ 'manage.cancel' | transloco }}
                      </button>
                    </div>
                  } @else {
                    <div class="confirm" animate.enter="yb-rise-in">
                      <p><strong>{{ 'manage.confirmQuestion' | transloco }}</strong></p>
                      <p class="hint">{{ 'manage.confirmText' | transloco }}</p>
                      @if (cancelError()) {
                        <p class="error-text">{{ 'manage.error' | transloco }}</p>
                      }
                      <div class="actions">
                        <button class="btn btn-primary danger-fill" type="button" [disabled]="cancelling()" (click)="cancel()">
                          {{ (cancelling() ? 'manage.cancelling' : 'manage.confirmYes') | transloco }}
                        </button>
                        <button class="btn btn-outline" type="button" [disabled]="cancelling()" (click)="confirming.set(false)">
                          {{ 'manage.keep' | transloco }}
                        </button>
                      </div>
                    </div>
                  }
                } @else if (r.status === 'Cancelled' && !justCancelled()) {
                  <p class="hint">{{ 'manage.alreadyCancelled' | transloco }}</p>
                } @else if (r.status === 'Rejected') {
                  <p class="hint">{{ 'manage.rejected' | transloco }}</p>
                } @else if (r.status !== 'Cancelled') {
                  <p class="hint">{{ 'manage.pastEvent' | transloco }}</p>
                }

                <div class="actions">
                  <a [routerLink]="['/events', r.event.slug]" class="btn-text">{{ 'manage.viewEvent' | transloco }} →</a>
                </div>
              }
            }
          }
        </div>
      </div>
    </section>
  `,
  styles: `
    .manage-head {
      padding: 3rem 0;

      h1 {
        font-size: clamp(1.8rem, 4vw, 2.7rem);
        margin: 0;
      }
    }

    .manage-wrap {
      max-width: 640px;
    }

    .manage-card {
      border-start-start-radius: var(--yb-arch-radius);
      border-start-end-radius: var(--yb-arch-radius);
      padding-top: 3.5rem;
    }

    .event-title {
      text-align: center;
      margin-bottom: 1.2rem;
    }

    .facts {
      list-style: none;
      padding: 0;
      margin: 0 0 1.5rem;
      border-top: 1px solid var(--yb-outline);

      li {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        padding: 0.7rem 0;
        border-bottom: 1px solid var(--yb-outline);
      }

      span {
        color: var(--yb-maroon);
        font-weight: 700;
        font-size: 0.8rem;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        flex: none;
      }
    }

    .status-confirmed { color: var(--yb-green); }
    .status-waitlisted { color: #a8670f; }
    .status-cancelled,
    .status-rejected { color: var(--yb-red); }
    .status-pending { color: var(--yb-blue); }

    .hint,
    .muted {
      color: var(--yb-maroon);
      font-size: 0.95rem;
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.8rem;
      margin-top: 1rem;
    }

    .confirm {
      background: var(--yb-surface-low);
      border: 1px solid var(--yb-outline);
      border-radius: var(--yb-radius);
      padding: 1.2rem;
    }

    .danger {
      color: var(--yb-red);
      border-color: var(--yb-red);
    }

    .danger-fill,
    .danger-fill:hover {
      background: var(--yb-red);
      color: #fff;
    }

    .btn-text {
      font-weight: 700;
      text-decoration: none;
    }

    .result {
      text-align: center;
      padding-bottom: 1rem;

      .result-icon {
        font-size: 3rem;
      }
    }
  `
})
export class ManageRegistration {
  private api = inject(ApiService);
  private lang = inject(LanguageService);

  readonly token = input.required<string>();

  readonly state = signal<PageState>('loading');
  readonly registration = signal<ManagedRegistration | null>(null);
  readonly confirming = signal(false);
  readonly cancelling = signal(false);
  readonly cancelError = signal(false);
  readonly justCancelled = signal(false);

  readonly title = computed(() => {
    const ev = this.registration()?.event;
    if (!ev) return '';
    const lang = this.lang.current();
    return (lang === 'nl' && ev.titleNl) || (lang === 'ar' && ev.titleAr) || ev.titleEn;
  });

  constructor() {
    effect(() => this.load(this.token()));
  }

  private load(token: string) {
    this.state.set('loading');
    this.api.getManagedRegistration(token).subscribe({
      next: registration => {
        this.registration.set(registration);
        this.state.set('ready');
      },
      error: err => this.state.set(err.status === 404 || err.status === 400 ? 'notFound' : 'error')
    });
  }

  cancel() {
    this.cancelling.set(true);
    this.cancelError.set(false);
    this.api.cancelManagedRegistration(this.token()).subscribe({
      next: registration => {
        this.registration.set(registration);
        this.justCancelled.set(true);
        this.confirming.set(false);
        this.cancelling.set(false);
      },
      error: () => {
        this.cancelError.set(true);
        this.cancelling.set(false);
      }
    });
  }
}
