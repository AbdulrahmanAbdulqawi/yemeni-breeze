import { Injectable, signal } from '@angular/core';
import { LoginResponse } from './models';

const TOKEN_KEY = 'yb_token';
const EXPIRY_KEY = 'yb_token_expiry';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly isLoggedIn = signal(this.hasValidToken());

  get token(): string | null {
    return this.hasValidToken() ? localStorage.getItem(TOKEN_KEY) : null;
  }

  storeSession(response: LoginResponse) {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(EXPIRY_KEY, response.expiresAt);
    this.isLoggedIn.set(true);
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRY_KEY);
    this.isLoggedIn.set(false);
  }

  private hasValidToken(): boolean {
    const token = localStorage.getItem(TOKEN_KEY);
    const expiry = localStorage.getItem(EXPIRY_KEY);
    return !!token && !!expiry && new Date(expiry) > new Date();
  }
}
