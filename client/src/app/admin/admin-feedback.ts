import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { EventDto, FeedbackAudience, FeedbackRow, FeedbackSummary } from '../core/models';
import { FEEDBACK_QUESTIONS } from '../pages/feedback/feedback-options';
import { ConfirmService } from './ui/confirm.service';
import { ToastService } from './ui/toast.service';
import { PageHeader } from './ui/page-header';
import { EmptyState, Spinner } from './ui/state-views';

type RowFilter = 'all' | 'join' | 'unfinished';

/** One event's feedback: send the email, see who answered and what, export it all. */
@Component({
  selector: 'app-admin-feedback',
  imports: [TranslocoPipe, DatePipe, RouterLink, FormsModule, PageHeader, EmptyState, Spinner],
  template: `
    <app-page-header [heading]="('admin.feedback.title' | transloco) + (event() ? ' · ' + event()!.titleEn : '')">
      <a [routerLink]="['/admin/events', eventId(), 'registrations']" class="btn btn-outline">←</a>
      <button class="btn btn-primary" [disabled]="!summary()?.responses" (click)="exportCsv()">
        {{ 'admin.feedback.exportCsv' | transloco }}
      </button>
    </app-page-header>

    @if (loading()) {
      <app-spinner />
    } @else if (summary(); as s) {
      <div class="stats">
        <div class="stat">
          <span class="num">{{ s.emailsSent }}</span>
          <span class="lbl">{{ 'admin.feedback.statSent' | transloco }}</span>
          @if (s.emailsPending) {
            <span class="sub">{{ 'admin.feedback.statPending' | transloco: { n: s.emailsPending } }}</span>
          }
        </div>
        <div class="stat">
          <span class="num">{{ s.responses }}</span>
          <span class="lbl">{{ 'admin.feedback.statResponses' | transloco }}</span>
        </div>
        <div class="stat">
          <span class="num">{{ s.completed }}</span>
          <span class="lbl">{{ 'admin.feedback.statCompleted' | transloco }}</span>
        </div>
        <div class="stat highlight">
          <span class="num">{{ s.wantToJoin }}</span>
          <span class="lbl">{{ 'admin.feedback.statJoin' | transloco }}</span>
        </div>
      </div>

      <section class="card send-card">
        <h2>{{ 'admin.feedback.sendTitle' | transloco }}</h2>
        <p class="muted">{{ 'admin.feedback.sendText' | transloco }}</p>
        <div class="send-row">
          <label class="radio">
            <input type="radio" name="audience" value="checkedIn" [ngModel]="audience()" (ngModelChange)="audience.set($event)" />
            {{ 'admin.feedback.audienceCheckedIn' | transloco: { n: s.checkedIn } }}
          </label>
          <label class="radio">
            <input type="radio" name="audience" value="confirmed" [ngModel]="audience()" (ngModelChange)="audience.set($event)" />
            {{ 'admin.feedback.audienceConfirmed' | transloco: { n: s.confirmed } }}
          </label>
          <button class="btn btn-primary" [disabled]="sending()" (click)="send()">
            {{ (sending() ? 'admin.feedback.sending' : 'admin.feedback.send') | transloco }}
          </button>
        </div>
      </section>

      @if (s.rows.length) {
        <div class="ad-toolbar">
          <input class="ad-search" type="search" [ngModel]="query()" (ngModelChange)="query.set($event)"
                 [placeholder]="'admin.common.search' | transloco" />
          <select class="ad-select" [ngModel]="filter()" (ngModelChange)="filter.set($event)">
            <option value="all">{{ 'admin.feedback.filterAll' | transloco }}</option>
            <option value="join">{{ 'admin.feedback.filterJoin' | transloco }}</option>
            <option value="unfinished">{{ 'admin.feedback.filterUnfinished' | transloco }}</option>
          </select>
        </div>

        @for (row of visible(); track row.id) {
          <details class="card answer" [open]="visible().length <= 3">
            <summary>
              <span class="who">
                <strong>{{ row.fullName }}</strong>
                <span class="muted">{{ row.email }}{{ row.phone ? ' · ' + row.phone : '' }}</span>
              </span>
              <span class="tags">
                @if (row.answers.joinCommunity; as join) {
                  <span class="badge" [class]="'badge ' + (join === 'yes' ? 'badge-confirmed' : join === 'maybe' ? 'badge-waitlisted' : 'badge-cancelled')">
                    {{ 'feedback.options.join.' + join | transloco }}
                  </span>
                }
                @if (row.answers.metExpectations; as met) {
                  <span class="badge badge-neutral">{{ 'feedback.options.met.' + met | transloco }}</span>
                }
                <span class="badge" [class]="'badge ' + (row.isComplete ? 'badge-confirmed' : 'badge-pending')">
                  {{ (row.isComplete ? 'admin.feedback.finished' : 'admin.feedback.inProgress') | transloco }}
                </span>
                <span class="muted when">{{ row.updatedAt | date: 'short' }}</span>
              </span>
            </summary>
            @if (!hasAnswers(row)) {
              <p class="muted nothing">{{ 'admin.feedback.nothingYet' | transloco }}</p>
            }
            <dl>
              @for (q of questions; track q.n) {
                @if (answerOf(row, q); as answer) {
                  <div class="qa">
                    <dt>{{ q.n }}. {{ 'feedback.q' + q.n | transloco }}</dt>
                    <dd>
                      @if (answer.codes.length) {
                        <span class="chips">
                          @for (code of answer.codes; track code) {
                            <span class="chip">{{ 'feedback.options.' + q.options + '.' + code | transloco }}</span>
                          }
                        </span>
                      }
                      @if (answer.text) {
                        <span class="text">{{ answer.text }}</span>
                      }
                    </dd>
                  </div>
                }
              }
            </dl>
          </details>
        } @empty {
          <app-empty-state [message]="'admin.feedback.noMatch' | transloco" />
        }
      } @else {
        <app-empty-state [message]="'admin.feedback.empty' | transloco" />
      }
    }
  `,
  styles: `
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 0.8rem;
      margin-bottom: 1rem;
    }

    .stat {
      background: var(--ad-surface, #fff);
      border: 1px solid var(--ad-border, #e4dcc9);
      border-radius: var(--ad-radius, 10px);
      padding: 0.9rem 1rem;
      display: flex;
      flex-direction: column;

      .num {
        font-size: 1.7rem;
        font-weight: 700;
        line-height: 1.1;
      }

      .lbl {
        color: var(--ad-muted, #7c6a5c);
        font-size: 0.85rem;
      }

      .sub {
        font-size: 0.78rem;
        color: #a8670f;
      }

      &.highlight {
        background: var(--yb-brown);
        border-color: var(--yb-brown);

        .num {
          color: var(--yb-gold);
        }

        .lbl {
          color: var(--yb-cream);
        }
      }
    }

    .send-card {
      padding: 1.1rem 1.2rem;
      margin-bottom: 1.4rem;

      h2 {
        font-size: 1.1rem;
        margin: 0 0 0.3rem;
      }

      .muted {
        margin: 0 0 0.8rem;
      }
    }

    .send-row {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.6rem 1.2rem;

      .btn {
        margin-inline-start: auto;
      }
    }

    .radio {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      cursor: pointer;

      input {
        accent-color: var(--yb-maroon);
      }
    }

    .muted {
      color: var(--ad-muted, #7c6a5c);
      font-size: 0.88rem;
    }

    .answer {
      margin-bottom: 0.7rem;
      padding: 0;

      summary {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.5rem 1rem;
        padding: 0.85rem 1.1rem;
        cursor: pointer;
        list-style: none;

        &::-webkit-details-marker {
          display: none;
        }
      }

      &[open] summary {
        border-bottom: 1px solid var(--ad-border, #e4dcc9);
      }

      dl {
        margin: 0;
        padding: 0.4rem 1.1rem 0.9rem;
      }
    }

    .nothing {
      margin: 0;
      padding: 0.8rem 1.1rem 0;
    }

    .who {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .tags {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.4rem;
    }

    .badge-neutral {
      background: var(--yb-surface);
      color: var(--yb-brown);
    }

    .when {
      font-size: 0.8rem;
    }

    .qa {
      padding: 0.6rem 0;
      border-bottom: 1px dashed var(--ad-border, #e4dcc9);

      &:last-child {
        border-bottom: none;
      }

      dt {
        font-size: 0.85rem;
        color: var(--ad-muted, #7c6a5c);
        margin-bottom: 0.25rem;
      }

      dd {
        margin: 0;
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
      }

      .text {
        white-space: pre-line;
      }
    }

    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }

    .chip {
      font-size: 0.85rem;
      padding: 0.15rem 0.65rem;
      border-radius: 999px;
      background: var(--yb-surface);
      border: 1px solid var(--yb-outline);
    }
  `
})
export class AdminFeedback {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);
  private confirm = inject(ConfirmService);

  readonly eventId = input.required<string>();
  readonly event = signal<EventDto | null>(null);
  readonly summary = signal<FeedbackSummary | null>(null);
  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly audience = signal<FeedbackAudience>('checkedIn');
  readonly query = signal('');
  readonly filter = signal<RowFilter>('all');

  readonly questions = FEEDBACK_QUESTIONS;

  readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const filter = this.filter();
    return (this.summary()?.rows ?? [])
      .filter(r => filter === 'all'
        || (filter === 'join' && r.answers.joinCommunity === 'yes')
        || (filter === 'unfinished' && !r.isComplete))
      .filter(r => !q || `${r.fullName} ${r.email}`.toLowerCase().includes(q));
  });

  constructor() {
    effect(() => {
      const id = +this.eventId();
      untracked(() => {
        this.load(id);
        this.api.adminGetEvents().subscribe(events => this.event.set(events.find(e => e.id === id) ?? null));
      });
    });
  }

  load(eventId: number) {
    this.api.adminGetFeedback(eventId).subscribe({
      next: summary => {
        this.summary.set(summary);
        this.loading.set(false);
        // Nobody checked in (no door scanning that night)? Then "everyone confirmed" is the useful default.
        if (!summary.checkedIn && this.audience() === 'checkedIn') this.audience.set('confirmed');
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  /** The answer to one question as option codes and/or free text, or null when they skipped it. */
  answerOf(row: FeedbackRow, q: (typeof FEEDBACK_QUESTIONS)[number]) {
    const value = row.answers[q.field];
    const codes = Array.isArray(value) ? value : q.options && value ? [value] : [];
    const text = (q.other ? row.answers[q.other] : !q.options ? (value as string | null) : null) ?? '';
    return codes.length || text ? { codes, text } : null;
  }

  hasAnswers(row: FeedbackRow) {
    return this.questions.some(q => this.answerOf(row, q));
  }

  async send() {
    const audience = this.audience();
    const group = this.transloco.translate(
      audience === 'checkedIn' ? 'admin.feedback.groupCheckedIn' : 'admin.feedback.groupConfirmed');
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.feedback.confirmSend', { group }),
      confirmLabel: this.transloco.translate('admin.feedback.send'),
      cancelLabel: this.transloco.translate('admin.media.cancel')
    });
    if (!ok) return;

    this.sending.set(true);
    this.api.adminSendFeedbackEmails(+this.eventId(), audience).subscribe({
      next: ({ queued }) => {
        this.sending.set(false);
        if (queued) this.toasts.success(this.transloco.translate('admin.feedback.queued', { n: queued }));
        else this.toasts.success(this.transloco.translate('admin.feedback.noneQueued'));
        this.load(+this.eventId());
        // The dispatcher sends within seconds; refresh so "emails sent" catches up.
        if (queued) setTimeout(() => this.load(+this.eventId()), 4000);
      },
      error: () => {
        this.sending.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  exportCsv() {
    this.http.get(this.api.feedbackCsvUrl(+this.eventId()), { responseType: 'blob' })
      .subscribe(blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `feedback-event-${this.eventId()}.csv`;
        link.click();
        URL.revokeObjectURL(url);
      });
  }
}
