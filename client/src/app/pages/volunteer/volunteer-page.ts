import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { LocalizedText, VolunteerFormField } from '../../core/models';
import { Reveal } from '../../shared/motion/reveal.directive';

const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

/**
 * The website's Volunteer page. Its questions come from the admin's form editor, so the
 * form is drawn from the field list: name, email and "what would you like to help with" are always there.
 */
@Component({
  selector: 'app-volunteer-page',
  imports: [FormsModule, RouterLink, TranslocoPipe, Reveal],
  templateUrl: './volunteer-page.html',
  styleUrl: './volunteer-page.scss'
})
export class VolunteerPage {
  private api = inject(ApiService);
  private transloco = inject(TranslocoService);
  readonly lang = inject(LanguageService);

  private readonly formTop = viewChild<ElementRef<HTMLElement>>('formTop');

  readonly fields = signal<VolunteerFormField[]>([]);
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  readonly sent = signal<{ name: string; email: string } | null>(null);

  /** Text answers by question id; "pick several" answers live in `picks`. */
  values: Record<string, string> = {};
  picks: Record<string, string[]> = {};
  helpOther = '';
  consent = false;

  constructor() {
    this.api.getVolunteerForm().subscribe({
      next: form => {
        this.fields.set(form.fields);
        this.state.set('ready');
      },
      error: () => this.state.set('error')
    });
  }

  text(t: LocalizedText) {
    const l = this.lang.current();
    return (l === 'ar' ? t.ar : l === 'nl' ? t.nl : t.en) || t.en;
  }

  inputType(field: VolunteerFormField) {
    return field.type === 'email' ? 'email' : field.type === 'phone' ? 'tel' : field.type === 'date' ? 'date' : 'text';
  }

  autocomplete(field: VolunteerFormField) {
    return field.id === 'name' ? 'name' : field.type === 'email' ? 'email' : field.type === 'phone' ? 'tel' : 'off';
  }

  picked(field: VolunteerFormField, optionId: string) {
    return this.picks[field.id]?.includes(optionId) ?? false;
  }

  togglePick(field: VolunteerFormField, optionId: string) {
    const current = this.picks[field.id] ?? [];
    this.picks[field.id] = current.includes(optionId) ? current.filter(id => id !== optionId) : [...current, optionId];
  }

  /** "Something else" in the help question opens a box for what else. */
  showsOther(field: VolunteerFormField) {
    return field.id === 'help' && this.picked(field, 'other');
  }

  private isEmpty(field: VolunteerFormField) {
    return field.type === 'many' ? !(this.picks[field.id]?.length) : !this.values[field.id]?.trim();
  }

  submit() {
    if (this.sending()) return;
    const fields = this.fields();
    const missing = fields.find(f => f.required && this.isEmpty(f));
    if (missing) {
      this.error.set(this.transloco.translate('volunteer.errorRequired', { question: this.text(missing.label) }));
      return;
    }
    const email = this.values['email']?.trim() ?? '';
    if (!EMAIL.test(email)) {
      this.error.set(this.transloco.translate('volunteer.errorEmail'));
      return;
    }
    if (!this.consent) {
      this.error.set(this.transloco.translate('volunteer.errorConsent'));
      return;
    }

    const answers: Record<string, string | string[]> = {};
    for (const f of fields) {
      if (f.type === 'many') answers[f.id] = this.picks[f.id] ?? [];
      else if (this.values[f.id]?.trim()) answers[f.id] = this.values[f.id].trim();
    }

    this.error.set(null);
    this.sending.set(true);
    this.api.volunteerSignup({
      language: this.lang.current(),
      answers,
      helpOther: this.helpOther.trim() || null,
      consent: this.consent
    }).subscribe({
      next: () => {
        this.sending.set(false);
        const name = this.values['name']?.trim() ?? '';
        this.sent.set({ name: name.split(/\s+/)[0] ?? name, email });
        this.scrollToForm();
      },
      error: (e: HttpErrorResponse) => {
        this.sending.set(false);
        this.error.set(this.transloco.translate(e.status === 429 ? 'volunteer.errorBusy' : 'volunteer.errorSend'));
      }
    });
  }

  reset() {
    this.values = {};
    this.picks = {};
    this.helpOther = '';
    this.consent = false;
    this.error.set(null);
    this.sent.set(null);
    this.scrollToForm();
  }

  private scrollToForm() {
    setTimeout(() => this.formTop()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }
}
