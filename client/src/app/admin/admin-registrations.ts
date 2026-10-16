import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { EmailStatus, EventDto, RegistrationDto, RegistrationStatus } from '../core/models';
import { ConfirmService } from './ui/confirm.service';
import { ToastService } from './ui/toast.service';
import { PageHeader } from './ui/page-header';
import { EmptyState, Spinner } from './ui/state-views';

@Component({
  selector: 'app-admin-registrations',
  imports: [TranslocoPipe, DatePipe, RouterLink, FormsModule, PageHeader, EmptyState, Spinner],
  template: `
    <app-page-header [heading]="'admin.registrations.title' | transloco">
      <a routerLink="/admin/events" class="btn btn-outline">←</a>
      <a [routerLink]="['/admin/events', eventId(), 'checkin']" class="btn btn-primary">
        {{ 'admin.checkin.title' | transloco }}
      </a>
      <button class="btn btn-primary" (click)="exportCsv()">
        {{ 'admin.registrations.exportCsv' | transloco }}
      </button>
    </app-page-header>

    <div class="stats">
      @if (event(); as ev) {
        <span class="seats" [class.over]="confirmedSeats() > ev.capacity">
          {{ 'admin.registrations.seatsTaken' | transloco: { taken: confirmedSeats(), capacity: ev.capacity } }}
        </span>
      }
      @if (pendingRows()) {
        <button class="badge badge-pending" (click)="statusFilter.set('Pending')">
          {{ pendingRows() }} {{ 'admin.registrations.pendingReview' | transloco }}
        </button>
      }
      <span class="badge badge-confirmed">{{ confirmedSeats() }} ✓</span>
      <span class="badge badge-waitlisted">{{ waitlistedSeats() }} ⏳</span>
      <span class="checked-ratio">
        {{ 'admin.checkin.checkedIn' | transloco }}: <strong>{{ checkedInCount() }}</strong> / {{ confirmedRows() }}
      </span>
    </div>

    <div class="ad-toolbar">
      <input
        class="ad-search"
        type="search"
        [ngModel]="query()"
        (ngModelChange)="query.set($event)"
        [placeholder]="'admin.common.search' | transloco" />
      <select class="ad-select" [ngModel]="statusFilter()" (ngModelChange)="statusFilter.set($event)">
        <option value="">{{ 'admin.common.filterAll' | transloco }}</option>
        <option value="Pending">Pending</option>
        <option value="Confirmed">Confirmed</option>
        <option value="Waitlisted">Waitlisted</option>
        <option value="Rejected">Rejected</option>
        <option value="Cancelled">Cancelled</option>
      </select>
    </div>

    @if (loading()) {
      <app-spinner />
    } @else if (visible().length) {
      <div class="table-wrap">
        <table class="yb-table sticky">
          <thead>
            <tr>
              <th>{{ 'admin.registrations.name' | transloco }}</th>
              <th>{{ 'admin.registrations.email' | transloco }}</th>
              <th>{{ 'admin.registrations.phone' | transloco }}</th>
              <th>{{ 'admin.registrations.country' | transloco }}</th>
              <th>{{ 'admin.registrations.guests' | transloco }}</th>
              <th>{{ 'admin.registrations.guestNames' | transloco }}</th>
              <th>{{ 'admin.registrations.reason' | transloco }}</th>
              <th>{{ 'admin.registrations.vegetarian' | transloco }}</th>
              <th>{{ 'admin.registrations.dietary' | transloco }}</th>
              <th>{{ 'admin.registrations.updates' | transloco }}</th>
              <th>{{ 'admin.registrations.status' | transloco }}</th>
              <th>{{ 'admin.checkin.checkedIn' | transloco }}</th>
              <th>{{ 'admin.registrations.emailStatus' | transloco }}</th>
              <th>{{ 'admin.registrations.note' | transloco }}</th>
              <th>{{ 'admin.registrations.registeredAt' | transloco }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (registration of visible(); track registration.id) {
              <tr>
                <td>{{ registration.fullName }}</td>
                <td>{{ registration.email }}</td>
                <td>{{ registration.phone }}</td>
                <td>{{ registration.country }}</td>
                <td>{{ registration.guestsCount }}</td>
                <td class="note-cell" [title]="registration.guestNames ?? ''">{{ registration.guestNames }}</td>
                <td class="note-cell" [title]="registration.reason ?? ''">{{ registration.reason }}</td>
                <td>
                  @if (registration.isVegetarian !== null) {
                    {{ (registration.isVegetarian ? 'admin.registrations.yes' : 'admin.registrations.no') | transloco }}
                  }
                </td>
                <td class="note-cell" [title]="registration.dietaryRestrictions ?? ''">{{ registration.dietaryRestrictions }}</td>
                <td>
                  @if (registration.wantsUpdates) {
                    {{ 'admin.registrations.yes' | transloco }}
                  }
                </td>
                <td>
                  <span class="badge" [class]="'badge badge-' + registration.status.toLowerCase()">
                    {{ registration.status }}
                  </span>
                </td>
                <td>
                  @if (registration.checkedInAt) {
                    <span class="badge badge-confirmed">✓ {{ registration.checkedInAt | date: 'HH:mm' }}</span>
                  }
                </td>
                <td>
                  @if (registration.lastEmailStatus; as emailStatus) {
                    <span
                      [class]="'badge email-badge ' + emailBadge(emailStatus)"
                      [title]="registration.lastEmailError || (registration.lastEmailKind + ' · ' + (registration.lastEmailAt | date: 'short'))">
                      {{ 'admin.registrations.email' + emailStatus | transloco }}
                    </span>
                  }
                </td>
                <td class="note-cell" [title]="registration.note">{{ registration.note }}</td>
                <td>{{ registration.createdAt | date: 'short' }}</td>
                <td class="actions">
                  <div class="actions-inner">
                  @if (registration.status === 'Pending') {
                    <button class="btn-link accept" (click)="accept(registration)">
                      {{ 'admin.registrations.accept' | transloco }}
                    </button>
                    <button class="btn-link danger" (click)="reject(registration)">
                      {{ 'admin.registrations.reject' | transloco }}
                    </button>
                  } @else if (registration.status !== 'Confirmed') {
                    <button class="btn-link" (click)="accept(registration)">
                      {{ (registration.status === 'Rejected' ? 'admin.registrations.accept' : 'admin.registrations.promote') | transloco }}
                    </button>
                  }
                  @if (registration.status === 'Confirmed' || registration.status === 'Waitlisted' || registration.status === 'Rejected') {
                    <button class="btn-link" [disabled]="resending() === registration.id" (click)="resendEmail(registration)">
                      {{ 'admin.registrations.resendEmail' | transloco }}
                    </button>
                  }
                  @if (registration.status === 'Confirmed' || registration.status === 'Waitlisted') {
                    <button class="btn-link danger" (click)="setStatus(registration, 'Cancelled')">
                      {{ 'admin.registrations.cancel' | transloco }}
                    </button>
                  }
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <app-empty-state [message]="'admin.registrations.empty' | transloco" />
    }
  `,
  styles: `
    .stats {
      display: flex;
      align-items: center;
      gap: 0.8rem;
      margin-bottom: 1rem;
      flex-wrap: wrap;
    }

    .seats {
      font-weight: 700;

      &.over {
        color: var(--yb-red);
      }
    }

    button.badge {
      border: none;
      cursor: pointer;
      font: inherit;
      font-size: 0.85rem;
      font-weight: 700;
    }

    .btn-link.accept {
      font-weight: 700;
      color: #1d7c2e;
    }

    .checked-ratio {
      font-size: 0.9rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .email-badge {
      white-space: nowrap;
    }

    .note-cell {
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* The cell stays a table-cell (flex on a <td> breaks the row); the buttons flex inside it. */
    .actions {
      white-space: nowrap;
    }

    .actions-inner {
      display: flex;
      align-items: center;
      gap: 0.6rem;
    }

    table.sticky thead th {
      position: sticky;
      top: 0;
      z-index: 1;
    }
  `
})
export class AdminRegistrations {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);
  private confirm = inject(ConfirmService);

  readonly eventId = input.required<string>();
  readonly event = signal<EventDto | null>(null);
  readonly registrations = signal<RegistrationDto[]>([]);
  readonly loading = signal(true);
  readonly query = signal('');
  readonly statusFilter = signal('');
  readonly resending = signal<number | null>(null);

  readonly confirmedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Confirmed')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  readonly waitlistedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Waitlisted')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  readonly pendingRows = computed(() =>
    this.registrations().filter(r => r.status === 'Pending').length);

  readonly confirmedRows = computed(() =>
    this.registrations().filter(r => r.status === 'Confirmed').length);

  readonly checkedInCount = computed(() =>
    this.registrations().filter(r => r.checkedInAt).length);

  readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const status = this.statusFilter();
    return this.registrations()
      .filter(r => !status || r.status === status)
      .filter(r => !q || `${r.fullName} ${r.email} ${r.phone ?? ''} ${r.country ?? ''} ${r.guestNames ?? ''}`
        .toLowerCase().includes(q));
  });

  constructor() {
    effect(() => {
      const id = +this.eventId();
      this.load(id);
      this.api.adminGetEvents().subscribe(events => this.event.set(events.find(e => e.id === id) ?? null));
    });
  }

  /** Confirms and sends the ticket. Warns first when that would go over the event's capacity. */
  async accept(registration: RegistrationDto) {
    const capacity = this.event()?.capacity;
    const after = this.confirmedSeats() + registration.guestsCount;
    if (capacity !== undefined && after > capacity) {
      const ok = await this.confirm.ask({
        message: this.transloco.translate('admin.registrations.overCapacity',
          { name: registration.fullName, after, capacity }),
        confirmLabel: this.transloco.translate('admin.registrations.acceptAnyway'),
        cancelLabel: this.transloco.translate('admin.media.cancel')
      });
      if (!ok) return;
    }
    this.setStatus(registration, 'Confirmed');
  }

  async reject(registration: RegistrationDto) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.registrations.confirmReject', { name: registration.fullName }),
      confirmLabel: this.transloco.translate('admin.registrations.reject'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (ok) this.setStatus(registration, 'Rejected');
  }

  load(eventId: number) {
    this.loading.set(true);
    this.api.adminGetRegistrations(eventId).subscribe({
      next: rows => {
        this.registrations.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  setStatus(registration: RegistrationDto, status: RegistrationStatus) {
    this.api.adminSetRegistrationStatus(+this.eventId(), registration.id, status).subscribe({
      next: () => {
        this.toasts.success(this.transloco.translate('admin.common.saved'));
        this.load(+this.eventId());
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  emailBadge(status: EmailStatus) {
    return status === 'Sent' ? 'badge-confirmed'
      : status === 'Failed' ? 'badge-cancelled'
        : 'badge-waitlisted';
  }

  resendEmail(registration: RegistrationDto) {
    this.resending.set(registration.id);
    this.api.adminResendRegistrationEmail(+this.eventId(), registration.id).subscribe({
      next: () => {
        this.resending.set(null);
        this.toasts.success(this.transloco.translate('admin.registrations.emailQueued'));
        // The dispatcher sends within seconds; refresh so the status column catches up.
        setTimeout(() => this.load(+this.eventId()), 3000);
      },
      error: () => {
        this.resending.set(null);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  exportCsv() {
    this.http.get(this.api.registrationsCsvUrl(+this.eventId()), { responseType: 'blob' })
      .subscribe(blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `registrations-event-${this.eventId()}.csv`;
        link.click();
        URL.revokeObjectURL(url);
      });
  }
}
