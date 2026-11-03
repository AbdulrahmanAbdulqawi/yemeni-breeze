import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { LanguageService } from '../core/language.service';
import { LocalizedDatePipe } from '../core/localized-date.pipe';
import { LocalizedText, Volunteer, VolunteerAnswer, VolunteerFormField } from '../core/models';
import { FEEDBACK_OPTIONS } from '../pages/feedback/feedback-options';
import { AdminLayout } from './admin-layout';
import { PageHeader } from './ui/page-header';
import { EmptyState, Spinner } from './ui/state-views';
import { ToastService } from './ui/toast.service';

type SourceFilter = 'all' | 'website' | 'yes' | 'maybe';
type Badge = { label: string; css: string };

const KNOWN_ROLES: readonly string[] = FEEDBACK_OPTIONS.contributions;

/**
 * Everyone who wants to volunteer: people who signed up on the website's Volunteer page and
 * guests who said yes or maybe in an event's feedback. Each person shows once, matched by email.
 */
@Component({
  selector: 'app-admin-volunteers',
  imports: [FormsModule, RouterLink, TranslocoPipe, LocalizedDatePipe, PageHeader, EmptyState, Spinner],
  template: `
    <app-page-header [heading]="'admin.volunteers.title' | transloco"
                     [subtitle]="'admin.volunteers.intro' | transloco">
      <button class="btn btn-outline" [disabled]="!people().length" (click)="exportCsv()">
        {{ 'admin.feedback.exportCsv' | transloco }}
      </button>
    </app-page-header>

    <nav class="ad-tabs" [attr.aria-label]="'admin.volunteers.sections' | transloco">
      <a class="on" aria-current="page">{{ 'admin.volunteers.tabPeople' | transloco }}</a>
      <a routerLink="/admin/volunteers/form">{{ 'admin.volunteers.tabForm' | transloco }}</a>
    </nav>

    @if (loading()) {
      <app-spinner />
    } @else if (!people().length) {
      <app-empty-state [message]="'admin.volunteers.empty' | transloco" />
    } @else {
      <div class="stats">
        <div class="stat">
          <span class="num">{{ people().length }}</span>
          <span class="lbl">{{ 'admin.volunteers.statTotal' | transloco }}</span>
        </div>
        <div class="stat">
          <span class="num web">{{ countWebsite() }}</span>
          <span class="lbl">{{ 'admin.volunteers.statWebsite' | transloco }}</span>
        </div>
        <div class="stat">
          <span class="num yes">{{ countFeedback() }}</span>
          <span class="lbl">{{ 'admin.volunteers.statFeedback' | transloco }}</span>
        </div>
        <div class="stat">
          <span class="num">{{ countNew() }}</span>
          <span class="lbl">{{ 'admin.volunteers.statNew' | transloco }}</span>
        </div>
      </div>

      <div class="ad-toolbar">
        <input class="ad-search" type="search" [ngModel]="query()" (ngModelChange)="query.set($event)"
               [placeholder]="'admin.volunteers.searchPlaceholder' | transloco"
               [attr.aria-label]="'admin.common.search' | transloco" />
        <div class="segmented" role="group" [attr.aria-label]="'admin.volunteers.sourceLabel' | transloco">
          @for (option of sourceOptions; track option.value) {
            <button type="button" [class.on]="source() === option.value" [attr.aria-pressed]="source() === option.value"
                    (click)="source.set(option.value)">
              {{ option.label | transloco }}
            </button>
          }
        </div>
      </div>

      <div class="roles" role="group" [attr.aria-label]="'admin.volunteers.rolesLabel' | transloco">
        <span class="roles-label">{{ 'admin.volunteers.rolesLabel' | transloco }}</span>
        <button type="button" class="role" [class.on]="role() === 'all'" [attr.aria-pressed]="role() === 'all'"
                (click)="role.set('all')">
          {{ 'admin.volunteers.all' | transloco }} <span class="n">{{ people().length }}</span>
        </button>
        @for (code of roleCodes(); track code) {
          <button type="button" class="role" [class.on]="role() === code" [attr.aria-pressed]="role() === code"
                  (click)="role.set(code)">
            {{ shortRole(code) }} <span class="n">{{ roleCount(code) }}</span>
          </button>
        }
      </div>

      <div class="layout">
        <section class="card list">
          @if (visible().length) {
            <div class="table-wrap">
              <table class="yb-table">
                <thead>
                  <tr>
                    <th>{{ 'admin.volunteers.colName' | transloco }}</th>
                    <th>{{ 'admin.volunteers.colSource' | transloco }}</th>
                    <th>{{ 'admin.volunteers.colRoles' | transloco }}</th>
                    <th>{{ 'admin.volunteers.colWhere' | transloco }}</th>
                    <th>{{ 'admin.volunteers.colStatus' | transloco }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (person of visible(); track person.email) {
                    <tr [class.selected]="person.email === selectedEmail()">
                      <td>
                        <button type="button" class="who" (click)="select(person)">
                          <span class="avatar" aria-hidden="true">{{ initials(person.fullName) }}</span>
                          <span class="who-text">
                            <span class="name">{{ person.fullName }}</span>
                            <span class="muted">{{ person.email }}</span>
                          </span>
                        </button>
                      </td>
                      <td>
                        <span class="badges">
                          @for (b of badges(person); track b.label) {
                            <span class="badge" [class]="b.css">{{ b.label | transloco }}</span>
                          }
                        </span>
                      </td>
                      <td>
                        <span class="chips">
                          @for (code of person.contributions.slice(0, 2); track code) {
                            <span class="chip">{{ shortRole(code) }}</span>
                          }
                          @if (person.contributions.length > 2) {
                            <span class="more">+{{ person.contributions.length - 2 }}</span>
                          }
                        </span>
                      </td>
                      <td class="event">
                        @if (person.signup && (!person.feedback || person.signup.signedUpAt >= person.feedback.repliedAt)) {
                          <span class="event-title">{{ 'admin.volunteers.volunteerPage' | transloco }}</span>
                          <span class="muted">{{ 'admin.volunteers.signedUp' | transloco: { date: (person.signup.signedUpAt | localizedDate: 'medium') } }}</span>
                        } @else if (person.feedback; as fb) {
                          <span class="event-title">{{ eventTitle(fb.event) }}</span>
                          <span class="muted">{{ 'admin.volunteers.replied' | transloco: { date: (fb.repliedAt | localizedDate: 'medium') } }}</span>
                        }
                      </td>
                      <td>
                        <span class="status" [class.contacted]="person.contacted">
                          <span class="dot" aria-hidden="true"></span>
                          {{ (person.contacted ? 'admin.volunteers.statusContacted' : 'admin.volunteers.statusNew') | transloco }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="muted no-match">{{ 'admin.volunteers.noMatch' | transloco }}</p>
          }
        </section>

        @if (selected(); as person) {
          <aside #details class="card details" [attr.aria-label]="'admin.volunteers.details' | transloco">
            <div class="d-head">
              <span class="avatar big" aria-hidden="true">{{ initials(person.fullName) }}</span>
              <div class="d-title">
                <h2>{{ person.fullName }}</h2>
                <div class="d-badges">
                  @for (b of badges(person); track b.label) {
                    <span class="badge" [class]="b.css">{{ b.label | transloco }}</span>
                  }
                  <span class="status pill" [class.contacted]="person.contacted">
                    <span class="dot" aria-hidden="true"></span>
                    {{ (person.contacted ? 'admin.volunteers.statusContacted' : 'admin.volunteers.statusNew') | transloco }}
                  </span>
                </div>
              </div>
              <button type="button" class="close" (click)="selectedEmail.set(null)"
                      [attr.aria-label]="'admin.volunteers.close' | transloco">
                <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
                  <path d="M6 6l12 12M18 6 6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round" fill="none" />
                </svg>
              </button>
            </div>

            <div class="d-section">
              <h3>{{ 'admin.volunteers.contact' | transloco }}</h3>
              <dl class="facts">
                <dt>{{ 'admin.volunteers.email' | transloco }}</dt>
                <dd><a [href]="'mailto:' + person.email">{{ person.email }}</a></dd>
                @if (person.phone) {
                  <dt>{{ 'admin.volunteers.phone' | transloco }}</dt>
                  <dd><a [href]="'tel:' + phoneLink(person.phone)" dir="ltr">{{ person.phone }}</a></dd>
                }
                <dt>{{ 'admin.volunteers.language' | transloco }}</dt>
                <dd>{{ 'admin.volunteers.languages.' + person.language | transloco }}</dd>
                @if (person.signup; as s) {
                  <dt>{{ 'admin.volunteers.signedUpLabel' | transloco }}</dt>
                  <dd>{{ s.signedUpAt | localizedDate: 'long' }} · {{ 'admin.volunteers.volunteerPage' | transloco }}</dd>
                }
                @if (person.feedback; as fb) {
                  <dt>{{ 'admin.volunteers.event' | transloco }}</dt>
                  <dd>{{ eventTitle(fb.event) }} · {{ fb.repliedAt | localizedDate: 'long' }}</dd>
                  @if (fb.eventsCount > 1) {
                    <dt>{{ 'admin.volunteers.eventsCount' | transloco }}</dt>
                    <dd>{{ fb.eventsCount }}</dd>
                  }
                }
              </dl>
            </div>

            <div class="d-section">
              <h3>{{ 'admin.volunteers.wantsTitle' | transloco }}</h3>
              @if (fullRoles(person).length) {
                <ul class="wants">
                  @for (code of fullRoles(person); track code) {
                    <li>
                      <span class="tick" aria-hidden="true">
                        <svg viewBox="0 0 24 24" width="12" height="12">
                          <path d="m5 12 5 5 9-10" stroke="currentColor" stroke-width="3" stroke-linecap="round"
                                stroke-linejoin="round" fill="none" />
                        </svg>
                      </span>
                      {{ fullRole(code) }}
                    </li>
                  }
                </ul>
              }
              @if (person.contributionsOther) {
                <p class="other"><strong>{{ 'admin.volunteers.other' | transloco }}</strong> {{ person.contributionsOther }}</p>
              } @else if (!fullRoles(person).length) {
                <p class="muted">{{ 'admin.volunteers.nothingPicked' | transloco }}</p>
              }
            </div>

            @if (shortAnswers(person); as answers) {
              @if (answers.length) {
                <div class="d-section">
                  <h3>{{ 'admin.volunteers.moreAnswers' | transloco }}</h3>
                  @for (a of answers; track a.fieldId) {
                    <div class="said">
                      <span class="said-label">{{ text(a.question) }}</span>
                      <p class="answer">{{ answerText(a) }}</p>
                    </div>
                  }
                </div>
              }
            }

            <div class="d-section">
              <h3>{{ 'admin.volunteers.ownWords' | transloco }}</h3>
              @for (a of longAnswers(person); track a.fieldId) {
                <div class="said">
                  <span class="said-label">{{ text(a.question) }}</span>
                  <p>{{ a.value }}</p>
                </div>
              }
              @if (person.feedback?.ideas; as ideas) {
                <div class="said">
                  <span class="said-label">{{ 'admin.volunteers.ideas' | transloco }}</span>
                  <p>{{ ideas }}</p>
                </div>
              }
              @if (person.feedback?.anythingElse; as anything) {
                <div class="said">
                  <span class="said-label">{{ 'admin.volunteers.anythingElse' | transloco }}</span>
                  <p>{{ anything }}</p>
                </div>
              }
              @if (!longAnswers(person).length && !person.feedback?.ideas && !person.feedback?.anythingElse) {
                <p class="muted">{{ 'admin.volunteers.noWords' | transloco }}</p>
              }
            </div>

            <div class="d-actions">
              <a class="btn btn-primary" [href]="'mailto:' + person.email">
                {{ 'admin.volunteers.emailThem' | transloco: { name: firstName(person.fullName) } }}
              </a>
              <button type="button" class="btn btn-outline" [disabled]="saving()" (click)="toggleContacted(person)">
                {{ (person.contacted ? 'admin.volunteers.markNew' : 'admin.volunteers.markContacted') | transloco }}
              </button>
              @if (person.feedback; as fb) {
                <a class="see-all" [routerLink]="['/admin/events', fb.event.id, 'feedback']">
                  {{ 'admin.volunteers.seeFeedback' | transloco }}
                </a>
              }
            </div>
          </aside>
        }
      </div>
    }
  `,
  styles: `
    .num.yes {
      color: #1d7c2e;
    }

    .num.web {
      color: #2b2bb0;
    }

    .badges {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.3rem;
    }

    .answer {
      font-weight: 600;
    }

    .roles {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.45rem;
      margin-bottom: 1.1rem;
    }

    .roles-label {
      font-size: 0.85rem;
      font-weight: 700;
      color: var(--ad-muted);
      margin-inline-end: 0.2rem;
    }

    .role {
      font: inherit;
      font-size: 0.86rem;
      font-weight: 600;
      padding: 0.35rem 0.8rem;
      border-radius: 999px;
      border: 1px solid var(--ad-border-strong);
      background: var(--ad-surface);
      color: var(--ad-text);
      cursor: pointer;

      .n {
        opacity: 0.7;
      }

      &.on {
        background: var(--yb-maroon);
        border-color: var(--yb-maroon);
        color: var(--yb-cream);
      }
    }

    .layout {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 1.1rem;
    }

    .list {
      flex: 999 1 560px;
      min-width: 0;
      padding: 0;
      overflow: hidden;

      table.yb-table {
        border: none;
        min-width: 620px;
      }

      td {
        vertical-align: top;
      }

      tr.selected td {
        background: var(--yb-surface-low);
      }

      tr.selected td:first-child {
        box-shadow: inset 3px 0 0 var(--yb-maroon);
      }
    }

    :host-context([dir='rtl']) .list tr.selected td:first-child {
      box-shadow: inset -3px 0 0 var(--yb-maroon);
    }

    .no-match {
      text-align: center;
      padding: 2rem 1rem;
      margin: 0;
    }

    .who {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      background: none;
      border: none;
      padding: 0;
      font: inherit;
      color: inherit;
      text-align: start;
      cursor: pointer;

      &:hover .name {
        color: var(--yb-maroon);
      }
    }

    .who-text {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .name {
      font-weight: 700;
      text-decoration: underline;
      text-decoration-color: var(--ad-border-strong);
      text-underline-offset: 3px;
    }

    .avatar {
      width: 34px;
      height: 34px;
      border-radius: 50%;
      background: var(--yb-surface);
      color: var(--yb-maroon);
      font-weight: 800;
      font-size: 0.8rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: none;

      &.big {
        width: 48px;
        height: 48px;
        font-size: 1.05rem;
        background: var(--yb-maroon);
        color: var(--yb-cream);
      }
    }

    .badge {
      font-size: 0.8rem;
      padding: 0.15rem 0.7rem;
      white-space: nowrap;
    }

    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }

    .chip {
      font-size: 0.8rem;
      font-weight: 600;
      padding: 0.15rem 0.6rem;
      border-radius: 999px;
      background: var(--ad-bg);
      border: 1px solid var(--ad-border);
      white-space: nowrap;
    }

    .more {
      font-size: 0.8rem;
      font-weight: 700;
      color: var(--ad-muted);
      padding: 0.15rem 0.2rem;
    }

    .event {
      display: flex;
      flex-direction: column;
      font-size: 0.86rem;
    }

    .event-title {
      font-weight: 600;
    }

    .status {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.82rem;
      font-weight: 700;
      color: #8a5508;
      white-space: nowrap;

      .dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--yb-orange);
      }

      &.contacted {
        color: #1d6a2a;

        .dot {
          background: var(--yb-green);
        }
      }

      &.pill {
        background: var(--ad-bg);
        border-radius: 999px;
        padding: 0.15rem 0.7rem;
      }
    }

    .details {
      flex: 1 1 340px;
      min-width: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      scroll-margin-top: 70px;
    }

    .d-head {
      display: flex;
      gap: 0.85rem;
      align-items: flex-start;
      padding: 1.1rem 1.2rem;
      border-bottom: 1px solid var(--ad-border);
    }

    .d-title {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 0.4rem;

      h2 {
        margin: 0;
        font-size: 1.1rem;
      }
    }

    .d-badges {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }

    .close {
      width: 36px;
      height: 36px;
      flex: none;
      border-radius: 8px;
      border: 1px solid var(--ad-border);
      background: var(--ad-surface);
      color: var(--ad-text);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;

      &:hover {
        border-color: var(--yb-maroon);
      }
    }

    .d-section {
      padding: 1rem 1.2rem;
      border-bottom: 1px solid var(--ad-border);
      display: flex;
      flex-direction: column;
      gap: 0.6rem;

      h3 {
        margin: 0;
        font-size: 0.75rem;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--ad-muted);
      }

      p {
        margin: 0;
      }
    }

    .facts {
      display: grid;
      grid-template-columns: 7rem minmax(0, 1fr);
      gap: 0.45rem 0.8rem;
      margin: 0;
      font-size: 0.9rem;

      dt {
        color: var(--ad-muted);
      }

      dd {
        margin: 0;
        font-weight: 600;
        overflow-wrap: anywhere;
      }

      a {
        color: var(--yb-maroon);
      }
    }

    .wants {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;

      li {
        display: flex;
        align-items: flex-start;
        gap: 0.6rem;
        font-weight: 600;
      }
    }

    .tick {
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: #e2f4e5;
      color: #1d7c2e;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: none;
    }

    .other {
      padding: 0.6rem 0.75rem;
      background: var(--yb-surface-low);
      border-radius: 8px;
      font-size: 0.9rem;
    }

    .said {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;

      p {
        white-space: pre-line;
        line-height: 1.5;
      }
    }

    .said-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: var(--ad-muted);
    }

    .d-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.6rem;
      padding: 1rem 1.2rem;

      .btn {
        text-decoration: none;
      }
    }

    .see-all {
      margin-inline-start: auto;
      font-size: 0.88rem;
      font-weight: 600;
      color: var(--yb-maroon);
    }
  `
})
export class AdminVolunteers {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);
  private lang = inject(LanguageService);
  private layout = inject(AdminLayout, { optional: true });

  private readonly detailsRef = viewChild<ElementRef<HTMLElement>>('details');

  readonly people = signal<Volunteer[]>([]);
  /** The "help with" question, for the labels of choices the admin added in the form editor. */
  private readonly helpField = signal<VolunteerFormField | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly query = signal('');
  readonly source = signal<SourceFilter>('all');
  readonly role = signal<string>('all');
  readonly selectedEmail = signal<string | null>(null);

  readonly sourceOptions: { value: SourceFilter; label: string }[] = [
    { value: 'all', label: 'admin.volunteers.sourceAll' },
    { value: 'website', label: 'admin.volunteers.sourceWebsite' },
    { value: 'yes', label: 'admin.volunteers.sourceYes' },
    { value: 'maybe', label: 'admin.volunteers.sourceMaybe' }
  ];

  /** The usual roles, then any choice added in the form editor or found in someone's answers. */
  readonly roleCodes = computed(() => {
    const codes = [...KNOWN_ROLES];
    for (const o of this.helpField()?.options ?? []) if (!codes.includes(o.id)) codes.push(o.id);
    for (const p of this.people()) for (const c of p.contributions) if (!codes.includes(c)) codes.push(c);
    return codes.filter(c => KNOWN_ROLES.includes(c) || this.roleCount(c) > 0);
  });

  readonly countWebsite = computed(() => this.people().filter(p => p.signup).length);
  readonly countFeedback = computed(() => this.people().filter(p => p.feedback).length);
  readonly countNew = computed(() => this.people().filter(p => !p.contacted).length);

  readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const source = this.source();
    const role = this.role();
    return this.people()
      .filter(p => source === 'all' || (source === 'website' ? !!p.signup : p.feedback?.interest === source))
      .filter(p => role === 'all' || p.contributions.includes(role))
      .filter(p => !q || `${p.fullName} ${p.email} ${p.phone ?? ''}`.toLowerCase().includes(q));
  });

  readonly selected = computed(() => this.people().find(p => p.email === this.selectedEmail()) ?? null);

  constructor() {
    this.api.adminGetVolunteers().subscribe({
      next: people => {
        this.people.set(people);
        this.loading.set(false);
        // On a wide screen the details sit beside the list, so open the first person straight away.
        if (people.length && window.matchMedia('(min-width: 1100px)').matches) this.selectedEmail.set(people[0].email);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
    this.api.adminGetVolunteerForm().subscribe({
      next: form => this.helpField.set(form.fields.find(f => f.id === 'help') ?? null),
      error: () => this.helpField.set(null)
    });
  }

  text(t: LocalizedText) {
    const l = this.lang.current();
    return (l === 'ar' ? t.ar : l === 'nl' ? t.nl : t.en) || t.en;
  }

  badges(person: Volunteer): Badge[] {
    const list: Badge[] = [];
    if (person.signup) list.push({ label: 'admin.volunteers.badgeWebsite', css: 'badge-pending' });
    if (person.feedback?.interest === 'yes') list.push({ label: 'admin.volunteers.badgeYes', css: 'badge-confirmed' });
    if (person.feedback?.interest === 'maybe') list.push({ label: 'admin.volunteers.badgeMaybe', css: 'badge-waitlisted' });
    return list;
  }

  roleCount(code: string) {
    return this.people().filter(p => p.contributions.includes(code)).length;
  }

  private customRole(code: string) {
    const option = this.helpField()?.options.find(o => o.id === code);
    return option ? this.text(option.label) : code;
  }

  shortRole(code: string) {
    return KNOWN_ROLES.includes(code) ? this.transloco.translate(`admin.volunteers.roles.${code}`) : this.customRole(code);
  }

  fullRole(code: string) {
    return KNOWN_ROLES.includes(code)
      ? this.transloco.translate(`feedback.options.contributions.${code}`)
      : this.customRole(code);
  }

  /** The chosen tasks, without "other" (its own text is shown instead). */
  fullRoles(person: Volunteer) {
    return person.contributions.filter(c => c !== 'other');
  }

  shortAnswers(person: Volunteer) {
    return (person.signup?.answers ?? []).filter(a => a.type !== 'long');
  }

  longAnswers(person: Volunteer) {
    return (person.signup?.answers ?? []).filter(a => a.type === 'long' && a.value);
  }

  answerText(a: VolunteerAnswer) {
    if (a.choices?.length) return a.choices.map(c => this.text(c)).join(', ');
    if (a.type === 'yesno') return this.transloco.translate(a.value === 'yes' ? 'volunteer.yes' : 'volunteer.no');
    if (a.type === 'date' && a.value) {
      return new Date(a.value + 'T00:00:00').toLocaleDateString(this.lang.current(), { dateStyle: 'long' });
    }
    return a.value ?? '';
  }

  select(person: Volunteer) {
    this.selectedEmail.set(person.email);
    // On a phone the details are below the list: bring them into view.
    if (!window.matchMedia('(min-width: 1100px)').matches) {
      setTimeout(() => this.detailsRef()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    }
  }

  eventTitle(e: { titleEn: string; titleNl: string; titleAr: string }) {
    const current = this.lang.current();
    return (current === 'ar' ? e.titleAr : current === 'nl' ? e.titleNl : e.titleEn) || e.titleEn;
  }

  initials(name: string) {
    return name.trim().split(/\s+/).slice(0, 2).map(w => w[0] ?? '').join('').toUpperCase();
  }

  firstName(name: string) {
    return name.trim().split(/\s+/)[0] ?? name;
  }

  phoneLink(phone: string) {
    return phone.replace(/[^\d+]/g, '');
  }

  toggleContacted(person: Volunteer) {
    const contacted = !person.contacted;
    this.saving.set(true);
    this.api.adminSetVolunteerContacted(person.email, contacted).subscribe({
      next: () => {
        this.saving.set(false);
        this.people.update(list => list.map(p => (p.email === person.email ? { ...p, contacted } : p)));
        this.layout?.setNewVolunteers(this.countNew());
        this.toasts.success(this.transloco.translate(
          contacted ? 'admin.volunteers.markedContacted' : 'admin.volunteers.markedNew', { name: person.fullName }));
      },
      error: () => {
        this.saving.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  exportCsv() {
    this.http.get(this.api.volunteersCsvUrl(), { responseType: 'blob' }).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'volunteers.csv';
      link.click();
      URL.revokeObjectURL(url);
    });
  }
}
