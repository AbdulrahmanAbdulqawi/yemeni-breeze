import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { EventDto, EventStatus } from '../core/models';
import { ConfirmService } from './ui/confirm.service';
import { ToastService } from './ui/toast.service';
import { PageHeader } from './ui/page-header';
import { EmptyState, Spinner } from './ui/state-views';

type SortKey = 'titleEn' | 'date' | 'status' | 'capacity' | 'confirmedCount' | 'waitlistedCount';

@Component({
  selector: 'app-admin-events',
  imports: [RouterLink, TranslocoPipe, DatePipe, FormsModule, PageHeader, EmptyState, Spinner],
  template: `
    <app-page-header [heading]="'admin.events.title' | transloco">
      <a routerLink="/admin/events/new" class="btn btn-primary">{{ 'admin.events.new' | transloco }}</a>
    </app-page-header>

    <div class="ad-toolbar">
      <input
        class="ad-search"
        type="search"
        [ngModel]="query()"
        (ngModelChange)="query.set($event)"
        [placeholder]="'admin.common.search' | transloco" />
      <select class="ad-select" [ngModel]="statusFilter()" (ngModelChange)="statusFilter.set($event)">
        <option value="">{{ 'admin.common.filterAll' | transloco }}</option>
        <option value="Draft">Draft</option>
        <option value="Published">Published</option>
        <option value="Past">Past</option>
      </select>
    </div>

    @if (loading()) {
      <app-spinner />
    } @else if (visible().length) {
      <div class="table-wrap">
        <table class="yb-table">
          <thead>
            <tr>
              <th><button class="ad-sort" (click)="sortBy('titleEn')">{{ 'admin.events.titleEn' | transloco }}{{ arrow('titleEn') }}</button></th>
              <th><button class="ad-sort" (click)="sortBy('date')">{{ 'admin.events.date' | transloco }}{{ arrow('date') }}</button></th>
              <th><button class="ad-sort" (click)="sortBy('status')">{{ 'admin.events.status' | transloco }}{{ arrow('status') }}</button></th>
              <th><button class="ad-sort" (click)="sortBy('capacity')">{{ 'admin.events.capacity' | transloco }}{{ arrow('capacity') }}</button></th>
              <th><button class="ad-sort" (click)="sortBy('confirmedCount')">{{ 'admin.events.confirmed' | transloco }}{{ arrow('confirmedCount') }}</button></th>
              <th><button class="ad-sort" (click)="sortBy('waitlistedCount')">{{ 'admin.events.waitlisted' | transloco }}{{ arrow('waitlistedCount') }}</button></th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (event of visible(); track event.id) {
              <tr>
                <td class="title-cell">{{ event.titleEn }}</td>
                <td>{{ event.date | date: 'mediumDate' }}</td>
                <td><span class="badge" [class]="'badge ' + statusClass(event.status)">{{ event.status }}</span></td>
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
    } @else {
      <app-empty-state [message]="'admin.common.noResults' | transloco" />
    }
  `,
  styles: `
    .title-cell {
      font-weight: 600;
    }

    .actions {
      white-space: nowrap;
      display: flex;
      gap: 0.7rem;
    }

    .badge-draft {
      background: #eee7d8;
      color: #7c6a5c;
    }

    .badge-published {
      background: #e2f4e5;
      color: #1d7c2e;
    }

    .badge-past {
      background: #ece9f7;
      color: #4a4185;
    }
  `
})
export class AdminEvents {
  private api = inject(ApiService);
  private confirm = inject(ConfirmService);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);

  readonly events = signal<EventDto[]>([]);
  readonly loading = signal(true);
  readonly sortKey = signal<SortKey>('date');
  readonly sortAsc = signal(false);

  readonly query = signal('');
  readonly statusFilter = signal('');

  readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const status = this.statusFilter();
    const key = this.sortKey();
    const dir = this.sortAsc() ? 1 : -1;

    return this.events()
      .filter(e => !status || e.status === status)
      .filter(e => !q || `${e.titleEn} ${e.titleNl} ${e.titleAr} ${e.location}`.toLowerCase().includes(q))
      .slice()
      .sort((a, b) => {
        const av = a[key];
        const bv = b[key];
        if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
        return String(av).localeCompare(String(bv)) * dir;
      });
  });

  constructor() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.adminGetEvents().subscribe({
      next: events => {
        this.events.set(events);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  sortBy(key: SortKey) {
    if (this.sortKey() === key) this.sortAsc.update(v => !v);
    else {
      this.sortKey.set(key);
      this.sortAsc.set(true);
    }
  }

  arrow(key: SortKey) {
    if (this.sortKey() !== key) return '';
    return this.sortAsc() ? ' ↑' : ' ↓';
  }

  statusClass(status: EventStatus) {
    return `badge-${status.toLowerCase()}`;
  }

  async remove(event: EventDto) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.events.confirmDelete'),
      confirmLabel: this.transloco.translate('admin.events.delete'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (!ok) return;

    this.api.adminDeleteEvent(event.id).subscribe({
      next: () => {
        this.toasts.success(this.transloco.translate('admin.media.deleted'));
        this.load();
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }
}
