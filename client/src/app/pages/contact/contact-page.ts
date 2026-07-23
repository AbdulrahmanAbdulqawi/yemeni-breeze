import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';

@Component({
  selector: 'app-contact-page',
  imports: [ReactiveFormsModule, TranslocoPipe, CmsPipe],
  templateUrl: './contact-page.html',
  styleUrl: './contact-page.scss'
})
export class ContactPage {
  private api = inject(ApiService);
  private fb = inject(FormBuilder);

  readonly sending = signal(false);
  readonly sent = signal(false);
  readonly failed = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    subject: [''],
    message: ['', Validators.required]
  });

  submit() {
    if (this.form.invalid || this.sending()) {
      this.form.markAllAsTouched();
      return;
    }
    this.sending.set(true);
    this.failed.set(false);
    this.api.sendContact(this.form.getRawValue()).subscribe({
      next: () => {
        this.sent.set(true);
        this.sending.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.sending.set(false);
      }
    });
  }
}
