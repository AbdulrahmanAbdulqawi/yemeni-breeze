import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { LocalizedText, VolunteerFieldType, VolunteerFormField } from '../core/models';
import { ConfirmService } from './ui/confirm.service';
import { PageHeader } from './ui/page-header';
import { Spinner } from './ui/state-views';
import { ToastService } from './ui/toast.service';

type Lang = 'en' | 'nl' | 'ar';

/** Answer types a new question can have. Email is only for the email question. */
const PICKABLE: VolunteerFieldType[] = ['short', 'long', 'phone', 'one', 'many', 'yesno', 'date'];
const LANGS: { code: Lang; label: string }[] = [
  { code: 'en', label: 'EN' }, { code: 'nl', label: 'NL' }, { code: 'ar', label: 'العربية' }
];

const hasOptions = (type: VolunteerFieldType) => type === 'one' || type === 'many';
const blank = (): LocalizedText => ({ en: '', nl: '', ar: '' });
const newId = (prefix: string) => `${prefix}-${Math.random().toString(36).slice(2, 10)}`;

/**
 * The "Sign-up form" tab: the questions on the website's Volunteer form. Nothing changes on the
 * site until Save; name, email and the "help with" question can be reworded but not removed.
 */
@Component({
  selector: 'app-admin-volunteer-form',
  imports: [RouterLink, TranslocoPipe, PageHeader, Spinner],
  template: `
    <app-page-header [heading]="'admin.volunteers.title' | transloco"
                     [subtitle]="'admin.volunteerForm.intro' | transloco">
      @if (dirty()) {
        <span class="unsaved"><span class="dot" aria-hidden="true"></span>{{ 'admin.volunteerForm.unsaved' | transloco }}</span>
      }
      <a class="btn-link" routerLink="/volunteer" target="_blank" rel="noopener">{{ 'admin.volunteerForm.preview' | transloco }}</a>
      <button type="button" class="btn btn-outline" [disabled]="!dirty() || saving()" (click)="discard()">
        {{ 'admin.volunteerForm.discard' | transloco }}
      </button>
      <button type="button" class="btn btn-primary" [disabled]="!dirty() || saving()" (click)="save()">
        {{ (saving() ? 'admin.volunteerForm.saving' : 'admin.volunteerForm.save') | transloco }}
      </button>
    </app-page-header>

    <nav class="ad-tabs" [attr.aria-label]="'admin.volunteers.sections' | transloco">
      <a routerLink="/admin/volunteers">{{ 'admin.volunteers.tabPeople' | transloco }}</a>
      <a class="on" aria-current="page">{{ 'admin.volunteers.tabForm' | transloco }}</a>
    </nav>

    @if (loading()) {
      <app-spinner />
    } @else {
      <div class="fe">
        <section class="card fe-list" [attr.aria-label]="'admin.volunteerForm.questions' | transloco">
          <div class="fe-list-head">
            <h2>{{ 'admin.volunteerForm.questions' | transloco }} <span class="muted">· {{ fields().length }}</span></h2>
            <button type="button" class="btn btn-outline btn-sm" (click)="addField()">+ {{ 'admin.volunteerForm.add' | transloco }}</button>
          </div>
          <ol>
            @for (f of fields(); track f.id; let i = $index, first = $first, last = $last) {
              <li [class.on]="f.id === selectedId()">
                <span class="n" aria-hidden="true">{{ i + 1 }}</span>
                <button type="button" class="pick" (click)="selectedId.set(f.id)" [attr.aria-pressed]="f.id === selectedId()">
                  <span class="q">{{ f.label.en || ('admin.volunteerForm.untitled' | transloco) }}</span>
                  <span class="meta">
                    {{ 'admin.volunteerForm.types.' + f.type | transloco }} ·
                    {{ (f.required ? 'admin.volunteerForm.required' : 'admin.volunteerForm.optional') | transloco }}
                    @if (f.lock) {
                      <span class="tag">{{ 'admin.volunteerForm.alwaysOn' | transloco }}</span>
                    }
                    @for (l of missing(f); track l) {
                      <span class="tag warn">{{ 'admin.volunteerForm.needs.' + l | transloco }}</span>
                    }
                  </span>
                </button>
                <button type="button" class="icon" [disabled]="first" (click)="move(i, -1)"
                        [attr.aria-label]="'admin.volunteerForm.moveUp' | transloco: { question: f.label.en }">
                  <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 15 6-6 6 6" /></svg>
                </button>
                <button type="button" class="icon" [disabled]="last" (click)="move(i, 1)"
                        [attr.aria-label]="'admin.volunteerForm.moveDown' | transloco: { question: f.label.en }">
                  <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg>
                </button>
              </li>
            }
          </ol>
          <p class="muted foot">{{ 'admin.volunteerForm.alwaysLast' | transloco }}</p>
        </section>

        @if (selected(); as f) {
          <section class="card fe-edit" [attr.aria-label]="'admin.volunteerForm.edit' | transloco">
            <div class="part">
              <h2>{{ 'admin.volunteerForm.edit' | transloco }}</h2>
              @if (f.lock) {
                <p class="lock">{{ 'admin.volunteerForm.lock.' + f.lock | transloco }}</p>
              }
            </div>

            <div class="part row">
              <div class="grow">
                <label for="fe-type">{{ 'admin.volunteerForm.type' | transloco }}</label>
                @if (f.lock) {
                  <span class="fixed">{{ 'admin.volunteerForm.types.' + f.type | transloco }}</span>
                } @else {
                  <select id="fe-type" [value]="f.type" (change)="setType($any($event.target).value)">
                    @for (t of pickable; track t) {
                      <option [value]="t" [selected]="t === f.type">{{ 'admin.volunteerForm.types.' + t | transloco }}</option>
                    }
                  </select>
                }
              </div>
              <label class="check">
                <input type="checkbox" [checked]="f.required" [disabled]="f.lock === 'contact'"
                       (change)="toggleRequired()" />
                {{ 'admin.volunteerForm.mustAnswer' | transloco }}
              </label>
            </div>

            <div class="part">
              <div class="row">
                <h3 class="grow">{{ 'admin.volunteerForm.wording' | transloco }}</h3>
                <div class="segmented" role="group" [attr.aria-label]="'admin.volunteerForm.language' | transloco">
                  @for (l of langs; track l.code) {
                    <button type="button" [class.on]="lang() === l.code" [attr.aria-pressed]="lang() === l.code"
                            (click)="lang.set(l.code)">
                      {{ l.label }}
                      @if (missing(f).includes(l.code)) {
                        <span class="dot" [attr.aria-label]="'admin.volunteerForm.missingText' | transloco"></span>
                      }
                    </button>
                  }
                </div>
              </div>
              <label for="fe-label">{{ 'admin.volunteerForm.question' | transloco }}</label>
              <input id="fe-label" [attr.dir]="dir()" [value]="f.label[lang()]" maxlength="200"
                     [placeholder]="lang() === 'en' ? '' : f.label.en"
                     (input)="setText('label', $any($event.target).value)" />
              <label for="fe-help">{{ 'admin.volunteerForm.help' | transloco }}</label>
              <input id="fe-help" [attr.dir]="dir()" [value]="f.help[lang()]" maxlength="300"
                     [placeholder]="lang() === 'en' ? '' : f.help.en"
                     (input)="setText('help', $any($event.target).value)" />

              @if (hasOptions(f.type)) {
                <span class="lbl">{{ 'admin.volunteerForm.choices' | transloco }}</span>
                @for (o of f.options; track o.id; let i = $index) {
                  <div class="choice">
                    <span class="mark" [class.round]="f.type === 'one'" aria-hidden="true"></span>
                    <input [attr.dir]="dir()" [value]="o.label[lang()]" maxlength="200"
                           [placeholder]="lang() === 'en' ? ('admin.volunteerForm.choiceN' | transloco: { n: i + 1 }) : o.label.en"
                           [attr.aria-label]="'admin.volunteerForm.choiceN' | transloco: { n: i + 1 }"
                           (input)="setOption(i, $any($event.target).value)" />
                    <button type="button" class="icon" [disabled]="f.options.length <= 1"
                            (click)="removeOption(i)"
                            [attr.aria-label]="'admin.volunteerForm.removeChoice' | transloco: { n: i + 1 }">
                      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18" /></svg>
                    </button>
                  </div>
                }
                <button type="button" class="add-choice" (click)="addOption()">+ {{ 'admin.volunteerForm.addChoice' | transloco }}</button>
                <p class="muted">{{ 'admin.volunteerForm.choiceNote' | transloco }}</p>
              }
            </div>

            @if (!f.lock) {
              <div class="part">
                <button type="button" class="btn btn-danger-outline" (click)="remove(f)">
                  {{ 'admin.volunteerForm.remove' | transloco }}
                </button>
              </div>
            }
          </section>
        }
      </div>
    }
  `,
  styles: `
    .unsaved {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.85rem;
      font-weight: 700;
      color: #7f4e07;
    }

    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--yb-orange);
      display: inline-block;
    }

    .btn-link {
      font-weight: 700;
      font-size: 0.9rem;
      color: var(--yb-maroon);
    }

    .fe {
      display: flex;
      flex-wrap: wrap;
      gap: 1.1rem;
      align-items: flex-start;
    }

    .fe-list {
      flex: 1 1 400px;
      min-width: 0;
      padding: 0;

      ol {
        list-style: none;
        margin: 0;
        padding: 0.5rem;
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
      }

      li {
        display: flex;
        align-items: center;
        gap: 0.45rem;
        padding: 0.4rem 0.5rem 0.4rem 0.75rem;
        border: 1.5px solid #ece4d2;
        border-radius: 8px;

        &.on {
          border-color: var(--yb-maroon);
          background: var(--yb-surface-low);
        }
      }
    }

    .fe-list-head {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.85rem 1rem;
      border-bottom: 1px solid var(--ad-border);

      h2 {
        flex: 1;
        margin: 0;
        font-size: 0.95rem;
      }
    }

    .n {
      width: 24px;
      height: 24px;
      border-radius: 50%;
      background: var(--yb-surface);
      color: var(--yb-maroon);
      font-size: 0.75rem;
      font-weight: 800;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: none;
    }

    .pick {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      background: none;
      border: none;
      padding: 0.25rem 0;
      font: inherit;
      color: inherit;
      text-align: start;
      cursor: pointer;
    }

    .q {
      font-weight: 700;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      align-items: center;
      font-size: 0.78rem;
      font-weight: 600;
      color: var(--ad-muted);
    }

    .tag {
      padding: 0 0.5rem;
      border-radius: 999px;
      background: var(--ad-bg);

      &.warn {
        background: #fdf0d7;
        color: #7f4e07;
      }
    }

    .foot {
      margin: 0;
      padding: 0.75rem 1rem 1rem;
      border-top: 1px solid var(--ad-border);
    }

    .fe-edit {
      flex: 1.25 1 440px;
      min-width: 0;
      padding: 0;
    }
  `
})
export class AdminVolunteerForm {
  private api = inject(ApiService);
  private toasts = inject(ToastService);
  private confirm = inject(ConfirmService);
  private transloco = inject(TranslocoService);

  readonly pickable = PICKABLE;
  readonly langs = LANGS;
  readonly hasOptions = hasOptions;

  readonly fields = signal<VolunteerFormField[]>([]);
  private readonly savedJson = signal('[]');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly selectedId = signal<string | null>(null);
  readonly lang = signal<Lang>('en');

  readonly dirty = computed(() => JSON.stringify(this.fields()) !== this.savedJson());
  readonly selected = computed(() => this.fields().find(f => f.id === this.selectedId()) ?? null);
  readonly dir = computed(() => (this.lang() === 'ar' ? 'rtl' : 'ltr'));

  constructor() {
    this.api.adminGetVolunteerForm().subscribe({
      next: form => {
        this.load(form.fields);
        this.selectedId.set(form.fields[0]?.id ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  private load(fields: VolunteerFormField[]) {
    this.fields.set(structuredClone(fields));
    this.savedJson.set(JSON.stringify(fields));
  }

  /** Languages (other than English) that still lack the question or one of its choices. */
  missing(f: VolunteerFormField): Lang[] {
    return (['nl', 'ar'] as const).filter(l =>
      !f.label[l].trim() || (hasOptions(f.type) && f.options.some(o => !o.label[l].trim())));
  }

  /** Changes the selected question. */
  change(fn: (f: VolunteerFormField) => void) {
    const id = this.selectedId();
    this.fields.update(list => {
      const next = structuredClone(list);
      const f = next.find(x => x.id === id);
      if (f) fn(f);
      return next;
    });
  }

  toggleRequired() {
    this.change(f => (f.required = !f.required));
  }

  setText(part: 'label' | 'help', value: string) {
    const lang = this.lang();
    this.change(f => (f[part][lang] = value));
  }

  setOption(index: number, value: string) {
    const lang = this.lang();
    this.change(f => (f.options[index].label[lang] = value));
  }

  removeOption(index: number) {
    this.change(f => f.options.splice(index, 1));
  }

  setType(type: VolunteerFieldType) {
    this.change(f => {
      f.type = type;
      if (hasOptions(type) && !f.options.length) {
        f.options = [
          { id: newId('c'), label: { ...blank(), en: this.choiceName(1) } },
          { id: newId('c'), label: { ...blank(), en: this.choiceName(2) } }
        ];
      }
      if (!hasOptions(type)) f.options = [];
    });
  }

  private choiceName(n: number) {
    return this.transloco.translate('admin.volunteerForm.choiceN', { n });
  }

  addOption() {
    this.change(f => f.options.push({ id: newId('c'), label: blank() }));
  }

  move(index: number, by: number) {
    this.fields.update(list => {
      const next = [...list];
      const [item] = next.splice(index, 1);
      next.splice(index + by, 0, item);
      return next;
    });
  }

  addField() {
    const field: VolunteerFormField = {
      id: newId('q'), type: 'short', lock: null, required: false,
      label: { ...blank(), en: this.transloco.translate('admin.volunteerForm.newQuestion') },
      help: blank(), options: []
    };
    this.fields.update(list => [...list, field]);
    this.selectedId.set(field.id);
    this.lang.set('en');
  }

  async remove(f: VolunteerFormField) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.volunteerForm.removeConfirm', { question: f.label.en }),
      confirmLabel: this.transloco.translate('admin.volunteerForm.removeYes'),
      cancelLabel: this.transloco.translate('admin.volunteerForm.keep'),
      danger: true
    });
    if (!ok) return;
    const index = this.fields().findIndex(x => x.id === f.id);
    const next = this.fields().filter(x => x.id !== f.id);
    this.fields.set(next);
    this.selectedId.set(next[Math.min(index, next.length - 1)]?.id ?? null);
  }

  discard() {
    const fields = JSON.parse(this.savedJson()) as VolunteerFormField[];
    this.fields.set(fields);
    if (!fields.some(f => f.id === this.selectedId())) this.selectedId.set(fields[0]?.id ?? null);
  }

  save() {
    this.saving.set(true);
    this.api.adminSaveVolunteerForm(this.fields()).subscribe({
      next: form => {
        this.load(form.fields);
        this.saving.set(false);
        this.toasts.success(this.transloco.translate('admin.volunteerForm.saved'));
      },
      error: e => {
        this.saving.set(false);
        this.toasts.error(e?.error?.message ?? this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  /** Asks before leaving the tab with unsaved changes (used by the route's canDeactivate). */
  canLeave(): boolean | Promise<boolean> {
    if (!this.dirty()) return true;
    return this.confirm.ask({
      message: this.transloco.translate('admin.volunteerForm.leaveConfirm'),
      confirmLabel: this.transloco.translate('admin.volunteerForm.leaveYes'),
      cancelLabel: this.transloco.translate('admin.volunteerForm.stay'),
      danger: true
    });
  }

  @HostListener('window:beforeunload', ['$event'])
  warnOnClose(event: BeforeUnloadEvent) {
    if (this.dirty()) event.preventDefault();
  }
}
