import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { ContactMessageDto } from '../core/models';

@Component({
  selector: 'app-admin-messages',
  imports: [TranslocoPipe, DatePipe],
  template: `
    <h1>{{ 'admin.messages.title' | transloco }}</h1>

    @if (messages().length) {
      <div class="messages-list">
        @for (message of messages(); track message.id) {
          <div class="card message-card" [class.unread]="!message.isRead">
            <div class="message-head">
              <strong>{{ message.name }}</strong>
              <a [href]="'mailto:' + message.email">{{ message.email }}</a>
              <span class="date">{{ message.createdAt | date: 'medium' }}</span>
            </div>
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
          </div>
        }
      </div>
    } @else {
      <p>{{ 'admin.messages.empty' | transloco }}</p>
    }
  `,
  styles: `
    .messages-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      max-width: 800px;
    }

    .message-card {
      padding: 1.2rem 1.5rem;

      &.unread {
        border-inline-start: 5px solid var(--yb-orange);
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
        font-size: 0.88rem;
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
export class AdminMessages {
  private api = inject(ApiService);

  readonly messages = signal<ContactMessageDto[]>([]);

  constructor() {
    this.load();
  }

  load() {
    this.api.adminGetMessages().subscribe(messages => this.messages.set(messages));
  }

  markRead(message: ContactMessageDto) {
    this.api.adminMarkMessageRead(message.id).subscribe(() => this.load());
  }

  remove(message: ContactMessageDto) {
    this.api.adminDeleteMessage(message.id).subscribe(() => this.load());
  }
}
