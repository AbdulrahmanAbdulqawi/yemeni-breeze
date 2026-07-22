import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-admin-login',
  imports: [ReactiveFormsModule, TranslocoPipe],
  template: `
    <div class="login-wrap">
      <div class="card login-card">
        <img src="/assets/logo.png" alt="Yemeni Breeze" class="login-logo" />
        <h1>{{ 'admin.login.title' | transloco }}</h1>
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label for="email">{{ 'admin.login.email' | transloco }}</label>
            <input id="email" formControlName="email" type="email" autocomplete="username" />
          </div>
          <div class="field">
            <label for="password">{{ 'admin.login.password' | transloco }}</label>
            <input id="password" formControlName="password" type="password" autocomplete="current-password" />
          </div>
          @if (failed()) {
            <p class="error-text">{{ 'admin.login.error' | transloco }}</p>
          }
          <button class="btn btn-primary login-btn" type="submit" [disabled]="loading()">
            {{ 'admin.login.submit' | transloco }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: `
    .login-wrap {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--yb-brown);
      padding: 1rem;
    }

    .login-card {
      width: min(420px, 100%);
      padding: 2.5rem;
      text-align: center;
      border-top: 6px solid var(--yb-orange);

      form {
        text-align: start;
      }
    }

    .login-logo {
      height: 110px;
    }

    .login-btn {
      width: 100%;
    }
  `
})
export class AdminLogin {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly failed = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  submit() {
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.failed.set(false);
    const { email, password } = this.form.getRawValue();
    this.api.login(email, password).subscribe({
      next: response => {
        this.auth.storeSession(response);
        this.router.navigate(['/admin']);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      }
    });
  }
}
