import { Component, input } from '@angular/core';

@Component({
  selector: 'app-spinner',
  template: `<div class="spinner-wrap"><span class="spinner"></span></div>`,
  styles: `
    .spinner-wrap {
      display: flex;
      justify-content: center;
      padding: 2.5rem;
    }

    .spinner {
      width: 26px;
      height: 26px;
      border: 3px solid var(--ad-border-strong, #d3c7ac);
      border-top-color: var(--yb-maroon);
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `
})
export class Spinner {}

@Component({
  selector: 'app-empty-state',
  template: `
    <div class="empty">
      <p class="empty-title">{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      text-align: center;
      padding: 3rem 1.5rem;
      border: 1.5px dashed var(--ad-border-strong, #d3c7ac);
      border-radius: var(--ad-radius, 10px);
      color: var(--ad-muted, #7c6a5c);
    }

    .empty-title {
      margin: 0 0 0.6rem;
      font-weight: 600;
    }
  `
})
export class EmptyState {
  readonly message = input.required<string>();
}
