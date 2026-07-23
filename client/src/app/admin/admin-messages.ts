import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { ContactMessageDto } from '../core/models';
import { ConfirmService } from './ui/confirm.service';
import { ToastService } from './ui/toast.service';
import { PageHeader } from './ui/page-header';
import { EmptyState, Spinner } from './ui/state-views';

@Component({
  selector: 'app-admin-messages',
  imports: [TranslocoPipe, DatePipe, FormsModule, PageHeader, EmptyState, Spinner],
  template: `
    <app-page-header [heading]="'admin.messages.title' | transloco" [subtitle]="subtitle()" />

    <div class="ad-toolbar">
      <input
        class="ad-search"
        type="search"
        [ngModel]="query()"
        (ngModelChange)="query.set($event)"
        [placeholder]="'admin.common.search' | transloco" />
      <select class="ad-select" [ngModel]="readFilter()" (ngModelChange)="readFilter.set($event)">
        <option value="">{{ 'admin.common.filterAll' | transloco }}</option>
        <option value="unread">{{ 'admin.messages.unreadOnly' | transloco }}</option>
        <option value="read">{{ 'admin.messages.readOnly' | transloco }}</option>
      </select>
    </div>

    @if (loading()) {
      <app-spinner />
    } @else if (visible().length) {
      <div class="messages-list">
        @for (message of visible(); track message.id) {
          <article class="card message-card" [class.unread]="!message.isRead">
            <header class="message-head">
              <strong>{{ message.name }}</strong>
              <a [href]="'mailto:' + message.email">{{ message.email }}</a>
              <span class="date">{{ message.createdAt | date: 'medium' }}</span>
            </header>
            @if (message.subject) {
              <p class="subject">{{ message.subject }}</p>
            }
            <p class="body">{{ message.message }}</p>
            <div class="message-actions">
              @if (!message.isRead) {
                <button class="btn-link" (click)="markRead(message)">
                  {{ 'admin.messages.markRead' | transloco }}
                </button>
              }
              <button class="btn-link danger" (click)="remove(message)">
                {{ 'admin.messages.delete' | transloco }}
              </button>
            </div>
          </article>
        }
      </div>
    } @else {
      <app-empty-state [message]="'admin.messages.empty' | transloco" />
    }
  `,
  styles: `
    .messages-list {
      display: flex;
      flex-direction: column;
      gap: 0.9rem;
      max-width: 820px;
    }

    .message-card {
      padding: 1.1rem 1.3rem;
      background: #fff;

      &.unread {
        border-inline-start: 4px solid var(--yb-orange);
      }
    }

    .message-head {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
      align-items: baseline;

      .date {
        margin-inline-start: auto;
        opacity: 0.65;
        font-size: 0.85rem;
      }
    }

    .subject {
      font-weight: 700;
      margin: 0.5rem 0 0.2rem;
    }

    .body {
      white-space: pre-line;
      margin: 0.4rem 0 0.8rem;
    }

    .message-actions {
      display: flex;
      gap: 0.8rem;
    }
  `
})
export class AdminMessages {
  private api = inject(ApiService);
  private confirm = inject(ConfirmService);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);

  readonly messages = signal<ContactMessageDto[]>([]);
  readonly loading = signal(true);
  readonly query = signal('');
  readonly readFilter = signal('');

  readonly unreadCount = computed(() => this.messages().filter(m => !m.isRead).length);
  readonly subtitle = computed(() => `${this.unreadCount()} / ${this.messages().length}`);

  readonly visible = computed(() => {
    const q = this.query().trim().toLowerCase();
    const filter = this.readFilter();
    return this.messages()
      .filter(m => !filter || (filter === 'unread' ? !m.isRead : m.isRead))
      .filter(m => !q || `${m.name} ${m.email} ${m.subject} ${m.message}`.toLowerCase().includes(q))
      .slice()
      // unread first, then newest
      .sort((a, b) =>
        Number(a.isRead) - Number(b.isRead) ||
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  });

  constructor() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.adminGetMessages().subscribe({
      next: messages => {
        this.messages.set(messages);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  markRead(message: ContactMessageDto) {
    this.api.adminMarkMessageRead(message.id).subscribe({
      next: () => this.load(),
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }

  async remove(message: ContactMessageDto) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.messages.confirmDelete'),
      confirmLabel: this.transloco.translate('admin.messages.delete'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (!ok) return;

    this.api.adminDeleteMessage(message.id).subscribe({
      next: () => {
        this.toasts.success(this.transloco.translate('admin.media.deleted'));
        this.load();
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }
}
