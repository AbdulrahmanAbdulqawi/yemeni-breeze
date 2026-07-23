import { Component, inject } from '@angular/core';
import { ConfirmService } from './confirm.service';

@Component({
  selector: 'app-confirm-dialog',
  template: `
    @if (confirm.request(); as request) {
      <div class="backdrop" (click)="confirm.respond(false)">
        <div class="dialog card" role="alertdialog" (click)="$event.stopPropagation()">
          <p class="message">{{ request.message }}</p>
          <div class="dialog-actions">
            <button type="button" class="btn btn-outline" (click)="confirm.respond(false)">
              {{ request.cancelLabel }}
            </button>
            <button
              type="button"
              class="btn"
              [class.btn-danger]="request.danger"
              [class.btn-primary]="!request.danger"
              (click)="confirm.respond(true)">
              {{ request.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    .backdrop {
      position: fixed;
      inset: 0;
      background: rgba(29, 12, 4, 0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 70;
      padding: 1rem;
    }

    .dialog {
      background: #fff;
      width: min(420px, 100%);
      padding: 1.5rem;
    }

    .message {
      margin: 0 0 1.3rem;
      font-size: 1rem;
      line-height: 1.5;
    }

    .dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.6rem;
    }

    .btn-danger {
      background: var(--yb-red);
      color: #fff;

      &:hover {
        background: var(--yb-maroon);
      }
    }
  `
})
export class ConfirmDialog {
  readonly confirm = inject(ConfirmService);
}
