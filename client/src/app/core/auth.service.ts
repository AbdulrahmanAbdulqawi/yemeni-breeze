import { Injectable, signal } from '@angular/core';
import { LoginResponse } from './models';

const TOKEN_KEY = 'yb_token';
const EXPIRY_KEY = 'yb_token_expiry';
const EMAIL_KEY = 'yb_email';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly isLoggedIn = signal(this.hasValidToken());
  readonly email = signal(localStorage.getItem(EMAIL_KEY) ?? '');

  get token(): string | null {
    return this.hasValidToken() ? localStorage.getItem(TOKEN_KEY) : null;
  }

  storeSession(response: LoginResponse) {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(EXPIRY_KEY, response.expiresAt);
    localStorage.setItem(EMAIL_KEY, response.email);
    this.isLoggedIn.set(true);
    this.email.set(response.email);
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRY_KEY);
    localStorage.removeItem(EMAIL_KEY);
    this.isLoggedIn.set(false);
    this.email.set('');
  }

  private hasValidToken(): boolean {
    const token = localStorage.getItem(TOKEN_KEY);
    const expiry = localStorage.getItem(EXPIRY_KEY);
    return !!token && !!expiry && new Date(expiry) > new Date();
  }
}
