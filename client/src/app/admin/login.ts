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
      width: min(400px, 100%);
      padding: 2.2rem;
      text-align: center;
      background: #fff;
      border-top: 5px solid var(--yb-orange);

      h1 {
        font-family: var(--yb-font-body);
        font-size: 1.3rem;
        color: #3d2415;
        margin-bottom: 1.4rem;
      }

      form {
        text-align: start;
      }

      .field label {
        font-size: 0.85rem;
        color: #7c6a5c;
      }

      .field input {
        font-size: 0.95rem;
        padding: 0.55rem 0.75rem;
        border-radius: 8px;
      }
    }

    .login-logo {
      height: 92px;
    }

    .login-btn {
      width: 100%;
      border-radius: 8px;
      padding: 0.6rem;
      margin-top: 0.4rem;

      &:hover {
        transform: none;
      }
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
