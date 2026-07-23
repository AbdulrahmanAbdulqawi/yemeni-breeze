import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { RegistrationDto, RegistrationStatus } from '../core/models';
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
        <option value="Confirmed">Confirmed</option>
        <option value="Waitlisted">Waitlisted</option>
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
              <th>{{ 'admin.registrations.guests' | transloco }}</th>
              <th>{{ 'admin.registrations.status' | transloco }}</th>
              <th>{{ 'admin.checkin.checkedIn' | transloco }}</th>
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
                <td>{{ registration.guestsCount }}</td>
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
                <td class="note-cell" [title]="registration.note">{{ registration.note }}</td>
                <td>{{ registration.createdAt | date: 'short' }}</td>
                <td class="actions">
                  @if (registration.status !== 'Confirmed') {
                    <button class="btn-link" (click)="setStatus(registration, 'Confirmed')">
                      {{ 'admin.registrations.promote' | transloco }}
                    </button>
                  }
                  @if (registration.status !== 'Cancelled') {
                    <button class="btn-link danger" (click)="setStatus(registration, 'Cancelled')">
                      {{ 'admin.registrations.cancel' | transloco }}
                    </button>
                  }
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

    .checked-ratio {
      font-size: 0.9rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .note-cell {
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .actions {
      white-space: nowrap;
      display: flex;
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

  readonly eventId = input.required<string>();
  readonly registrations = signal<RegistrationDto[]>([]);
  readonly loading = signal(true);
  readonly query = signal('');
  readonly statusFilter = signal('');

  readonly confirmedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Confirmed')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  readonly waitlistedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Waitlisted')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  readonly confirmedRows = computed(() =>
    this.registrations().filter(r => r.status === 'Confirmed').length);

  readonly checkedInCount = computed(() =>
    this.registrations().filter(r => r.checkedInAt).length);

  readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const status = this.statusFilter();
    return this.registrations()
      .filter(r => !status || r.status === status)
      .filter(r => !q || `${r.fullName} ${r.email} ${r.phone ?? ''}`.toLowerCase().includes(q));
  });

  constructor() {
    effect(() => this.load(+this.eventId()));
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
