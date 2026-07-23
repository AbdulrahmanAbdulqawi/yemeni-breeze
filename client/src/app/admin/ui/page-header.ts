import { Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div>
        <h1>{{ heading() }}</h1>
        @if (subtitle()) {
          <p class="subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="header-actions">
        <ng-content />
      </div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      flex-wrap: wrap;
      margin-bottom: 1.3rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid var(--ad-border, #e4dcc9);
    }

    .subtitle {
      margin: 0.25rem 0 0;
      color: var(--ad-muted, #7c6a5c);
      font-size: 0.9rem;
    }

    .header-actions {
      display: flex;
      gap: 0.6rem;
      flex-wrap: wrap;
    }
  `
})
export class PageHeader {
  readonly heading = input.required<string>();
  readonly subtitle = input<string>('');
}
