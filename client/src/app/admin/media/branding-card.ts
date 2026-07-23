import { Component, inject, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { SiteSettings } from '../../core/models';
import { ToastService } from '../ui/toast.service';

type BrandingKey = 'heroImageUrl' | 'aboutImageUrl';

@Component({
  selector: 'app-branding-card',
  imports: [TranslocoPipe],
  template: `
    <section class="card branding">
      <h3>{{ 'admin.gallery.branding' | transloco }}</h3>
      <div class="slots">
        @for (slot of slots; track slot.key) {
          <div class="slot">
            <label>{{ slot.labelKey | transloco }}</label>
            @if (settings()[slot.key]; as url) {
              <img [src]="url" alt="" />
              <button type="button" class="btn-link danger" (click)="remove(slot.key)">
                {{ 'admin.gallery.removeImage' | transloco }}
              </button>
            } @else {
              <div class="slot-empty">—</div>
            }
            <input type="file" accept="image/*" (change)="upload($event, slot.key)" [disabled]="busy()" />
          </div>
        }
      </div>
    </section>
  `,
  styles: `
    .branding {
      background: #fff;
      padding: 1.1rem 1.3rem;
      margin-bottom: 1.2rem;

      h3 {
        margin: 0 0 0.8rem;
      }
    }

    .slots {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 1.2rem;
    }

    .slot {
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
      align-items: flex-start;

      label {
        font-weight: 600;
        font-size: 0.85rem;
        color: var(--ad-muted, #7c6a5c);
      }

      img {
        max-height: 96px;
        border-radius: 8px;
        object-fit: cover;
      }

      input[type='file'] {
        font-size: 0.82rem;
      }
    }

    .slot-empty {
      height: 60px;
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1.5px dashed var(--ad-border-strong, #d3c7ac);
      border-radius: 8px;
      color: var(--ad-muted, #7c6a5c);
    }
  `
})
export class BrandingCard {
  private api = inject(ApiService);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);

  readonly settings = signal<SiteSettings>({});
  readonly busy = signal(false);

  readonly slots: { key: BrandingKey; labelKey: string }[] = [
    { key: 'heroImageUrl', labelKey: 'admin.gallery.heroImage' },
    { key: 'aboutImageUrl', labelKey: 'admin.gallery.aboutImage' }
  ];

  constructor() {
    this.api.getSettings().subscribe(settings => this.settings.set(settings));
  }

  upload(event: Event, key: BrandingKey) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.busy.set(true);
    this.api.adminUpload(file).subscribe({
      next: result => this.save({ [key]: result.url }),
      error: () => {
        this.busy.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  remove(key: BrandingKey) {
    this.busy.set(true);
    this.save({ [key]: '' });
  }

  private save(patch: SiteSettings) {
    this.api.adminUpdateSettings(patch).subscribe({
      next: settings => {
        this.settings.set(settings);
        this.busy.set(false);
        this.toasts.success(this.transloco.translate('admin.common.saved'));
      },
      error: () => {
        this.busy.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }
}
