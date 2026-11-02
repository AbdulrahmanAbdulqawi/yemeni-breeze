import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-page-header',
  imports: [RouterLink, TranslocoPipe],
  template: `
    <header class="page-header">
      <div class="title-row">
        @if (back(); as link) {
          <a class="back" [routerLink]="link" [attr.aria-label]="'admin.common.back' | transloco"
             [title]="'admin.common.back' | transloco">
            <span class="arrow" aria-hidden="true">←</span>
          </a>
        }
        <div>
          <h1>{{ heading() }}</h1>
          @if (subtitle()) {
            <p class="subtitle">{{ subtitle() }}</p>
          }
        </div>
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

    .title-row {
      display: flex;
      align-items: flex-start;
      gap: 0.8rem;
      min-width: 0;

      h1 {
        margin: 0;
      }
    }

    /* Round back button at the start of the header, before the title */
    .back {
      flex: none;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2.4rem;
      height: 2.4rem;
      border-radius: 50%;
      border: 1px solid var(--ad-border-strong, #d3c7ac);
      background: var(--ad-surface, #fff);
      color: var(--ad-text, #3d2415);
      text-decoration: none;
      font-size: 1.2rem;
      line-height: 1;
      transition: background 0.15s ease, color 0.15s ease, border-color 0.15s ease;

      &:hover {
        background: var(--yb-maroon);
        border-color: var(--yb-maroon);
        color: var(--yb-cream);
      }
    }

    /* "Back" points the way you came: left in English, right in Arabic */
    :host-context([dir='rtl']) .arrow {
      display: inline-block;
      transform: scaleX(-1);
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
  /** Where the back button goes. No button when left out. */
  readonly back = input<string | readonly (string | number)[] | null>(null);
}
