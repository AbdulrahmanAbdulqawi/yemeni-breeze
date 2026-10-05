import { Component, input } from '@angular/core';
import { CmsPipe } from '../core/cms.pipe';

export type ServiceKey = 'photography' | 'corner' | 'food';

/** What people can book Yemen Breeze for — listed on the About and Book us pages. */
export const SERVICES: ServiceKey[] = ['photography', 'corner', 'food'];

/** One service: icon, name and short description, all admin-editable under `services.*`. */
@Component({
  selector: 'app-service-card',
  imports: [CmsPipe],
  host: { class: 'card card-content' },
  template: `
    <span class="icon-badge">
      @switch (service()) {
        @case ('photography') {
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3Z" />
            <circle cx="12" cy="13" r="3.5" />
          </svg>
        }
        @case ('corner') {
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M5 21V10a7 7 0 0 1 14 0v11M3 21h18M12 3v18M5 12h14" />
          </svg>
        }
        @case ('food') {
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M3 12h18a9 9 0 0 1-18 0ZM7 21h10M8 3c-.8 1.3.8 2.2 0 3.5M12 3c-.8 1.3.8 2.2 0 3.5M16 3c-.8 1.3.8 2.2 0 3.5" />
          </svg>
        }
      }
    </span>
    <h3>{{ 'services.' + service() | cms }}</h3>
    <p>{{ 'services.' + service() + 'Text' | cms }}</p>
  `,
  styles: `
    :host {
      display: block;
    }

    h3 {
      margin-bottom: 0.3rem;
    }

    p {
      margin: 0;
    }
  `
})
export class ServiceCard {
  readonly service = input.required<ServiceKey>();
}
