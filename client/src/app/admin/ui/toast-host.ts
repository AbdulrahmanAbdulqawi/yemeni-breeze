import { Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast-host',
  template: `
    <div class="toast-host" role="status" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class.error]="toast.kind === 'error'">
          <span>{{ toast.text }}</span>
          <button type="button" (click)="toasts.dismiss(toast.id)" aria-label="Dismiss">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .toast-host {
      position: fixed;
      inset-block-end: 1.2rem;
      inset-inline-end: 1.2rem;
      z-index: 60;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: min(360px, calc(100vw - 2.4rem));
    }

    .toast {
      display: flex;
      align-items: start;
      gap: 0.8rem;
      background: #1d7c2e;
      color: #fff;
      padding: 0.7rem 0.9rem;
      border-radius: 10px;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.18);
      font-size: 0.9rem;
      animation: toast-in 0.16s ease-out;

      &.error {
        background: var(--yb-maroon);
      }

      button {
        background: none;
        border: none;
        color: inherit;
        font-size: 1.1rem;
        line-height: 1;
        cursor: pointer;
        padding: 0;
        margin-inline-start: auto;
        opacity: 0.8;

        &:hover {
          opacity: 1;
        }
      }
    }

    @keyframes toast-in {
      from {
        opacity: 0;
        transform: translateY(6px);
      }
    }
  `
})
export class ToastHost {
  readonly toasts = inject(ToastService);
}
