import { Component, model } from '@angular/core';
import { Lang } from '../../core/language.service';

/**
 * Tab strip for editing multilingual content one language at a time,
 * instead of stacking an English, Dutch and Arabic field for every value.
 * Independent of the admin UI language, so any language can be edited
 * from any interface language.
 */
@Component({
  selector: 'app-lang-tabs',
  template: `
    <div class="lang-tabs" role="tablist">
      @for (tab of tabs; track tab.code) {
        <button
          type="button"
          role="tab"
          [class.active]="active() === tab.code"
          [attr.aria-selected]="active() === tab.code"
          (click)="active.set(tab.code)">
          {{ tab.label }}
        </button>
      }
    </div>
  `,
  styles: `
    .lang-tabs {
      display: inline-flex;
      gap: 0.2rem;
      background: var(--ad-bg, #f4f1ea);
      border: 1px solid var(--ad-border, #e4dcc9);
      border-radius: 8px;
      padding: 0.2rem;

      button {
        font: inherit;
        font-size: 0.82rem;
        font-weight: 600;
        border: none;
        background: none;
        color: var(--ad-muted, #7c6a5c);
        padding: 0.3rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;

        &:hover {
          color: var(--yb-maroon);
        }

        &.active {
          background: #fff;
          color: var(--yb-maroon);
          box-shadow: 0 1px 2px rgba(71, 24, 10, 0.12);
        }
      }
    }
  `
})
export class LangTabs {
  readonly active = model<Lang>('en');

  readonly tabs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'English' },
    { code: 'nl', label: 'Nederlands' },
    { code: 'ar', label: 'العربية' }
  ];
}
