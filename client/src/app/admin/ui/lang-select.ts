import { Component, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Lang } from '../../core/language.service';

/**
 * Picks which language of a multilingual field is being edited, so titles,
 * descriptions and captions aren't stacked three-deep. Independent of the
 * admin UI language, so any language can be edited from any interface language.
 */
@Component({
  selector: 'app-lang-select',
  imports: [FormsModule],
  template: `
    <label class="lang-select">
      <span class="lang-select-label">{{ label() }}</span>
      <select [ngModel]="active()" (ngModelChange)="active.set($event)">
        @for (option of options; track option.code) {
          <option [ngValue]="option.code">{{ option.code.toUpperCase() }}</option>
        }
      </select>
    </label>
  `,
  styles: `
    .lang-select {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
    }

    .lang-select-label {
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.07em;
      color: var(--ad-muted, #7c6a5c);
    }

    select {
      font: inherit;
      font-size: 0.85rem;
      font-weight: 700;
      padding: 0.32rem 0.55rem;
      border: 1.5px solid var(--ad-border-strong, #d3c7ac);
      border-radius: 8px;
      background: #fff;
      color: var(--yb-maroon);
      cursor: pointer;

      &:focus {
        outline: 2px solid var(--yb-blue);
        border-color: var(--yb-blue);
      }
    }
  `
})
export class LangSelect {
  readonly active = model<Lang>('en');
  readonly label = model<string>('Language');

  readonly options: { code: Lang }[] = [{ code: 'en' }, { code: 'nl' }, { code: 'ar' }];
}
