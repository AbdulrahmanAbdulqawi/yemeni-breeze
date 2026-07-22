import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { EventDto } from '../core/models';

@Component({
  selector: 'app-admin-events',
  imports: [RouterLink, TranslocoPipe, DatePipe],
  template: `
    <div class="admin-head">
      <h1>{{ 'admin.events.title' | transloco }}</h1>
      <a routerLink="/admin/events/new" class="btn btn-primary">{{ 'admin.events.new' | transloco }}</a>
    </div>

    <div class="table-wrap">
      <table class="yb-table">
        <thead>
          <tr>
            <th>{{ 'admin.events.titleEn' | transloco }}</th>
            <th>{{ 'admin.events.date' | transloco }}</th>
            <th>{{ 'admin.events.status' | transloco }}</th>
            <th>{{ 'admin.events.capacity' | transloco }}</th>
            <th>{{ 'admin.events.confirmed' | transloco }}</th>
            <th>{{ 'admin.events.waitlisted' | transloco }}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (event of events(); track event.id) {
            <tr>
              <td>{{ event.titleEn }}</td>
              <td>{{ event.date | date: 'mediumDate' }}</td>
              <td>{{ event.status }}</td>
              <td>{{ event.capacity }}</td>
              <td>{{ event.confirmedCount }}</td>
              <td>{{ event.waitlistedCount }}</td>
              <td class="actions">
                <a [routerLink]="['/admin/events', event.id, 'registrations']" class="btn-link">
                  {{ 'admin.events.registrations' | transloco }}
                </a>
                <a [routerLink]="['/admin/events', event.id]" class="btn-link">
                  {{ 'admin.events.edit' | transloco }}
                </a>
                <button class="btn-link danger" (click)="remove(event)">
                  {{ 'admin.events.delete' | transloco }}
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: `
    .admin-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.4rem;
    }

    .actions {
      white-space: nowrap;
      display: flex;
      gap: 0.7rem;
    }

    .btn-link {
      background: none;
      border: none;
      color: var(--yb-maroon);
      font-weight: 600;
      cursor: pointer;
      text-decoration: underline;
      padding: 0;
      font-size: 0.95rem;

      &.danger {
        color: var(--yb-red);
      }
    }
  `
})
export class AdminEvents {
  private api = inject(ApiService);
  private transloco = inject(TranslocoService);

  readonly events = signal<EventDto[]>([]);

  constructor() {
    this.load();
  }

  load() {
    this.api.adminGetEvents().subscribe(events => this.events.set(events));
  }

  remove(event: EventDto) {
    if (!confirm(this.transloco.translate('admin.events.confirmDelete'))) return;
    this.api.adminDeleteEvent(event.id).subscribe(() => this.load());
  }
}
