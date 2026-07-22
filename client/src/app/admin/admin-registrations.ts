import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { RegistrationDto, RegistrationStatus } from '../core/models';

@Component({
  selector: 'app-admin-registrations',
  imports: [TranslocoPipe, DatePipe, RouterLink],
  template: `
    <div class="admin-head">
      <h1>{{ 'admin.registrations.title' | transloco }}</h1>
      <div class="head-actions">
        <a routerLink="/admin/events" class="btn btn-outline">←</a>
        <button class="btn btn-primary" (click)="exportCsv()">
          {{ 'admin.registrations.exportCsv' | transloco }}
        </button>
      </div>
    </div>

    <p class="summary">
      <span class="badge badge-confirmed">{{ confirmedSeats() }} ✓</span>
      <span class="badge badge-waitlisted">{{ waitlistedSeats() }} ⏳</span>
    </p>

    @if (registrations().length) {
      <div class="table-wrap">
        <table class="yb-table">
          <thead>
            <tr>
              <th>{{ 'admin.registrations.name' | transloco }}</th>
              <th>{{ 'admin.registrations.email' | transloco }}</th>
              <th>{{ 'admin.registrations.phone' | transloco }}</th>
              <th>{{ 'admin.registrations.guests' | transloco }}</th>
              <th>{{ 'admin.registrations.status' | transloco }}</th>
              <th>{{ 'admin.registrations.note' | transloco }}</th>
              <th>{{ 'admin.registrations.registeredAt' | transloco }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (registration of registrations(); track registration.id) {
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
                <td class="note-cell">{{ registration.note }}</td>
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
      <p>{{ 'admin.registrations.empty' | transloco }}</p>
    }
  `,
  styles: `
    .admin-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
    }

    .head-actions {
      display: flex;
      gap: 0.7rem;
    }

    .summary {
      display: flex;
      gap: 0.7rem;
    }

    .note-cell {
      max-width: 220px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .actions {
      white-space: nowrap;
      display: flex;
      gap: 0.6rem;
    }

    .btn-link {
      background: none;
      border: none;
      color: var(--yb-maroon);
      font-weight: 600;
      cursor: pointer;
      text-decoration: underline;
      padding: 0;

      &.danger {
        color: var(--yb-red);
      }
    }
  `
})
export class AdminRegistrations {
  private api = inject(ApiService);
  private http = inject(HttpClient);

  readonly eventId = input.required<string>();
  readonly registrations = signal<RegistrationDto[]>([]);

  readonly confirmedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Confirmed')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  readonly waitlistedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Waitlisted')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  constructor() {
    effect(() => this.load(+this.eventId()));
  }

  load(eventId: number) {
    this.api.adminGetRegistrations(eventId).subscribe(rows => this.registrations.set(rows));
  }

  setStatus(registration: RegistrationDto, status: RegistrationStatus) {
    this.api.adminSetRegistrationStatus(+this.eventId(), registration.id, status)
      .subscribe(() => this.load(+this.eventId()));
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
