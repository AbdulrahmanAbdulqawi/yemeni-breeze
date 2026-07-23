import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Lang, LanguageService } from './language.service';

/**
 * Site language picker, shared by the public header and the admin top bar.
 * Colours are inherited from the surrounding header so it reads correctly on
 * both the dark public header and the light admin bar.
 */
@Component({
  selector: 'app-language-switcher',
  imports: [FormsModule, TranslocoPipe],
  template: `
    <label class="lang-switcher">
      <span class="sr-only">{{ 'language' | transloco }}</span>
      <svg class="lang-icon" viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="9" />
        <path d="M3 12h18M12 3a15 15 0 0 1 0 18a15 15 0 0 1 0-18" />
      </svg>
      <select
        [ngModel]="lang.current()"
        (ngModelChange)="lang.set($event)"
        [attr.aria-label]="transloco.translate('language')">
        @for (option of options; track option) {
          <option [ngValue]="option">{{ option.toUpperCase() }}</option>
        }
      </select>
    </label>
  `,
  styles: `
    .lang-switcher {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      color: inherit;
      cursor: pointer;
    }

    .lang-icon {
      width: 17px;
      height: 17px;
      flex: none;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.6;
      opacity: 0.75;
    }

    select {
      font: inherit;
      font-size: 0.82rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      color: inherit;
      background: transparent;
      border: 1.5px solid currentColor;
      border-radius: 8px;
      padding: 0.25rem 0.4rem;
      cursor: pointer;
      /* the border is decorative; keep it lighter than the text */
      border-color: color-mix(in srgb, currentColor 40%, transparent);

      &:hover {
        border-color: color-mix(in srgb, currentColor 70%, transparent);
      }

      &:focus-visible {
        outline: 2px solid var(--yb-gold);
        outline-offset: 1px;
      }
    }

    /* The popup list is drawn by the OS, so it needs explicit colours
       rather than the inherited (possibly light-on-dark) ones. */
    option {
      color: var(--yb-brown-deep);
      background: #fff;
      font-weight: 600;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
  `
})
export class LanguageSwitcher {
  readonly lang = inject(LanguageService);
  readonly transloco = inject(TranslocoService);
  readonly options: Lang[] = ['en', 'nl', 'ar'];
}
