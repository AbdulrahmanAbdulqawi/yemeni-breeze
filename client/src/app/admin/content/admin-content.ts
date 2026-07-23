import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { Lang } from '../../core/language.service';
import { ContentBlockInput } from '../../core/models';
import { ToastService } from '../ui/toast.service';
import { PageHeader } from '../ui/page-header';
import { Spinner } from '../ui/state-views';
import { LangSelect } from '../ui/lang-select';
import { CONTENT_FIELDS, CONTENT_GROUPS, ContentGroup } from './content-fields';

type FieldValue = { en: string; nl: string; ar: string };

@Component({
  selector: 'app-admin-content',
  imports: [FormsModule, TranslocoPipe, PageHeader, Spinner, LangSelect],
  template: `
    <app-page-header [heading]="'admin.content.title' | transloco" [subtitle]="'admin.content.subtitle' | transloco">
      <app-lang-select [(active)]="contentLang" [label]="'admin.events.translation' | transloco" />
      <button class="btn btn-primary" type="button" [disabled]="saving()" (click)="save()">
        {{ 'admin.content.save' | transloco }}
      </button>
    </app-page-header>

    @if (loading()) {
      <app-spinner />
    } @else {
      @for (group of groups; track group) {
        <section class="card content-group">
          <h2>{{ ('admin.content.groups.' + group) | transloco }}</h2>
          <div class="fields">
            @for (field of fieldsByGroup(group); track field.key) {
              <div class="field">
                <label [for]="field.key">{{ label(field.key) }}</label>
                @if (field.shared) {
                  <input
                    [id]="field.key"
                    [ngModel]="values()[field.key]?.en ?? ''"
                    (ngModelChange)="setShared(field.key, $event)" />
                } @else if (field.multiline) {
                  <textarea
                    [id]="field.key"
                    rows="3"
                    [dir]="contentLang() === 'ar' ? 'rtl' : 'ltr'"
                    [ngModel]="values()[field.key]?.[contentLang()] ?? ''"
                    (ngModelChange)="setValue(field.key, $event)"></textarea>
                } @else {
                  <input
                    [id]="field.key"
                    [dir]="contentLang() === 'ar' ? 'rtl' : 'ltr'"
                    [ngModel]="values()[field.key]?.[contentLang()] ?? ''"
                    (ngModelChange)="setValue(field.key, $event)" />
                }
              </div>
            }
          </div>
        </section>
      }
    }
  `,
  styles: `
    .content-group {
      padding: 1.4rem 1.6rem 1.6rem;
      margin-bottom: 1.2rem;

      h2 {
        margin: 0 0 1rem;
        font-size: 1.05rem;
        font-family: var(--yb-font-body);
        color: var(--yb-brown);
      }
    }

    .fields {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 1rem 1.2rem;
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;

      label {
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--ad-muted, #7c6a5c);
      }

      input,
      textarea {
        font: inherit;
        padding: 0.5rem 0.7rem;
        border: 1.5px solid var(--ad-border-strong, #d3c7ac);
        border-radius: 8px;

        &:focus {
          outline: 2px solid var(--yb-blue);
          border-color: var(--yb-blue);
        }
      }
    }
  `
})
export class AdminContent {
  private api = inject(ApiService);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);

  readonly groups = CONTENT_GROUPS;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly contentLang = signal<Lang>('en');
  readonly values = signal<Record<string, FieldValue>>({});

  fieldsByGroup(group: ContentGroup) {
    return CONTENT_FIELDS.filter(f => f.group === group);
  }

  label(key: string) {
    const last = key.split('.').pop() ?? key;
    return last
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/^./, c => c.toUpperCase());
  }

  constructor() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.adminGetContent().subscribe({
      next: blocks => {
        const values: Record<string, FieldValue> = {};
        for (const b of blocks) values[b.key] = { en: b.valueEn, nl: b.valueNl, ar: b.valueAr };
        this.values.set(values);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  setValue(key: string, value: string) {
    this.values.update(all => ({
      ...all,
      [key]: { ...(all[key] ?? { en: '', nl: '', ar: '' }), [this.contentLang()]: value }
    }));
  }

  setShared(key: string, value: string) {
    this.values.update(all => ({ ...all, [key]: { en: value, nl: value, ar: value } }));
  }

  save() {
    this.saving.set(true);
    const payload: Record<string, ContentBlockInput> = {};
    for (const [key, v] of Object.entries(this.values()))
      payload[key] = { en: v.en, nl: v.nl, ar: v.ar };

    this.api.adminUpdateContent(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.toasts.success(this.transloco.translate('admin.common.saved'));
      },
      error: () => {
        this.saving.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }
}
