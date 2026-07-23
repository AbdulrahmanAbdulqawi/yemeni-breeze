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
      <span class="select-wrap">
        <select
          [ngModel]="lang.current()"
          (ngModelChange)="lang.set($event)"
          [attr.aria-label]="transloco.translate('language')">
          @for (option of options; track option) {
            <option [ngValue]="option">{{ option.toUpperCase() }}</option>
          }
        </select>
        <svg class="select-chevron" viewBox="0 0 24 24" aria-hidden="true">
          <path d="m6 9 6 6 6-6" />
        </svg>
      </span>
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

    .select-wrap {
      position: relative;
      display: inline-flex;
      align-items: center;
    }

    select {
      /* appearance: none drops the browser's own grey dropdown chrome so
         the brand chevron (an inherited-colour SVG, not baked into a data
         URI) is the only arrow shown. */
      appearance: none;
      font: inherit;
      font-size: 0.82rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      color: inherit;
      background-color: color-mix(in srgb, currentColor 10%, transparent);
      border: 1.5px solid color-mix(in srgb, currentColor 40%, transparent);
      border-radius: 8px;
      padding: 0.25rem 1.6rem 0.25rem 0.55rem;
      cursor: pointer;
      accent-color: var(--yb-maroon);

      /* :host-context (not "[dir='rtl'] &") because dir="rtl" lives on <html>,
         outside this component — see gallery-page.scss for the full note. */
      :host-context([dir='rtl']) & {
        padding: 0.25rem 0.55rem 0.25rem 1.6rem;
      }

      &:hover {
        border-color: color-mix(in srgb, currentColor 70%, transparent);
        background-color: color-mix(in srgb, currentColor 16%, transparent);
      }

      &:focus-visible {
        outline: 2px solid var(--yb-gold);
        outline-offset: 1px;
      }
    }

    .select-chevron {
      position: absolute;
      inset-inline-end: 0.45rem;
      width: 11px;
      height: 11px;
      fill: none;
      stroke: currentColor;
      stroke-width: 2;
      stroke-linecap: round;
      stroke-linejoin: round;
      pointer-events: none;
      opacity: 0.85;
    }

    /* The popup list itself is drawn by the OS and can't inherit the header's
       colour, so it gets the site's paper/ink pairing explicitly instead of
       falling back to a plain white system default. */
    option {
      color: var(--yb-brown-deep);
      background: var(--yb-cream);
      font-weight: 600;
    }

    option:checked {
      color: var(--yb-maroon);
      background: var(--yb-yellow);
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
