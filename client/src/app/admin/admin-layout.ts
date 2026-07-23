import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { ApiService } from '../core/api.service';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcher } from '../core/language-switcher';
import { ConfirmDialog } from './ui/confirm-dialog';
import { ToastHost } from './ui/toast-host';

@Component({
  selector: 'app-admin-layout',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe,
    ConfirmDialog, ToastHost, LanguageSwitcher
  ],
  template: `
    <div class="admin-shell" [class.nav-open]="navOpen()">
      @if (navOpen()) {
        <div class="nav-scrim" (click)="navOpen.set(false)"></div>
      }

      <aside class="admin-sidebar">
        <a routerLink="/" class="admin-brand">
          <img src="/assets/logo.png" alt="Yemeni Breeze" />
        </a>

        <nav>
          <p class="nav-label">{{ 'admin.nav.manage' | transloco }}</p>

          <a routerLink="/admin/events" routerLinkActive="active" (click)="navOpen.set(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <rect x="3" y="5" width="18" height="16" rx="2" />
              <path d="M3 10h18M8 3v4M16 3v4" />
            </svg>
            {{ 'admin.nav.events' | transloco }}
          </a>

          <a routerLink="/admin/media" routerLinkActive="active" (click)="navOpen.set(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <rect x="3" y="4" width="18" height="16" rx="2" />
              <path d="m3 16 5-5 4 4 3-3 6 6" />
              <circle cx="8.5" cy="9" r="1.5" />
            </svg>
            {{ 'admin.nav.media' | transloco }}
          </a>

          <a routerLink="/admin/messages" routerLinkActive="active" (click)="navOpen.set(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <rect x="3" y="5" width="18" height="14" rx="2" />
              <path d="m3 7 9 6 9-6" />
            </svg>
            {{ 'admin.nav.messages' | transloco }}
            @if (unread() > 0) {
              <span class="nav-count">{{ unread() }}</span>
            }
          </a>
        </nav>

        <div class="admin-sidebar-footer">
          @if (auth.email(); as email) {
            <p class="signed-in" [title]="email">{{ email }}</p>
          }
          <a routerLink="/">{{ 'admin.nav.viewSite' | transloco }}</a>
          <button (click)="logout()">{{ 'admin.nav.logout' | transloco }}</button>
        </div>
      </aside>

      <div class="admin-content">
        <header class="admin-topbar">
          <button class="nav-toggle" type="button" (click)="navOpen.set(!navOpen())" aria-label="Menu">
            <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true">
              <path d="M3 6h18M3 12h18M3 18h18" stroke="currentColor" stroke-width="2" stroke-linecap="round" fill="none" />
            </svg>
          </button>

          <img src="/assets/logo.png" alt="" class="topbar-logo" />
          <span class="topbar-title">Yemeni Breeze</span>

          <div class="topbar-right">
            <app-language-switcher />
            <a routerLink="/" class="topbar-link">{{ 'admin.nav.viewSite' | transloco }}</a>
          </div>
        </header>

        <main class="admin-main">
          <router-outlet />
        </main>
      </div>

      <app-toast-host />
      <app-confirm-dialog />
    </div>
  `,
  styles: `
    .admin-shell {
      min-height: 100vh;
      background: var(--ad-bg);
    }

    /* Sidebar is pinned full-height; only the content column scrolls. */
    .admin-content {
      margin-inline-start: 232px;
      min-width: 0;
    }

    .admin-topbar {
      position: sticky;
      inset-block-start: 0;
      z-index: 30;
      display: flex;
      align-items: center;
      gap: 0.7rem;
      background: #fff;
      border-bottom: 1px solid var(--ad-border);
      padding: 0.55rem 1.4rem;
      min-height: 54px;
    }

    .topbar-logo {
      height: 30px;
    }

    .topbar-title {
      font-weight: 700;
      font-family: var(--yb-font-heading);
      color: var(--yb-maroon);
      letter-spacing: 0.02em;
    }

    .topbar-right {
      margin-inline-start: auto;
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .topbar-link {
      font-size: 0.86rem;
      color: var(--ad-muted);
      text-decoration: none;

      &:hover {
        color: var(--yb-maroon);
      }
    }

    .nav-toggle {
      display: none;
      background: none;
      color: var(--yb-brown);
      border: none;
      border-radius: 8px;
      padding: 0.3rem;
      cursor: pointer;
    }

    .nav-scrim {
      display: none;
      position: fixed;
      inset: 0;
      background: rgba(29, 12, 4, 0.45);
      z-index: 44;
    }

    .admin-sidebar {
      position: fixed;
      inset-block: 0;
      inset-inline-start: 0;
      width: 232px;
      overflow-y: auto;
      background: var(--yb-brown);
      color: var(--yb-cream);
      padding: 1.3rem 0.9rem;
      display: flex;
      flex-direction: column;
      gap: 1.4rem;
      z-index: 45;

      .admin-brand img {
        height: 64px;
        display: block;
        margin: 0 auto;
      }

      nav {
        display: flex;
        flex-direction: column;
        gap: 0.15rem;

        a {
          display: flex;
          align-items: center;
          gap: 0.65rem;
          color: var(--yb-cream);
          text-decoration: none;
          font-weight: 600;
          font-size: 0.92rem;
          padding: 0.55rem 0.8rem;
          border-radius: 8px;

          svg {
            width: 18px;
            height: 18px;
            flex: none;
            fill: none;
            stroke: currentColor;
            stroke-width: 1.8;
            stroke-linecap: round;
            stroke-linejoin: round;
            opacity: 0.85;
          }

          &.active,
          &:hover {
            background: var(--yb-maroon);
            color: #fff;

            svg {
              opacity: 1;
            }
          }
        }
      }
    }

    .nav-label {
      margin: 0 0 0.4rem 0.8rem;
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.09em;
      opacity: 0.6;
    }

    .nav-count {
      margin-inline-start: auto;
      background: var(--yb-orange);
      color: var(--yb-brown-deep);
      border-radius: 999px;
      font-size: 0.74rem;
      font-weight: 700;
      padding: 0.05rem 0.45rem;
    }

    .admin-sidebar-footer {
      margin-top: auto;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;

      .signed-in {
        margin: 0;
        font-size: 0.78rem;
        opacity: 0.7;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      a {
        color: var(--yb-yellow);
        text-decoration: none;
        font-size: 0.88rem;
      }

      button {
        background: transparent;
        border: 1.5px solid rgba(246, 231, 190, 0.5);
        color: var(--yb-cream);
        border-radius: 8px;
        padding: 0.4rem;
        cursor: pointer;
        font: inherit;
        font-size: 0.88rem;

        &:hover {
          background: var(--yb-cream);
          color: var(--yb-brown);
        }
      }
    }

    .admin-main {
      padding: 1.8rem 2rem 3rem;
      min-width: 0;
    }

    @media (max-width: 860px) {
      .nav-toggle {
        display: block;
      }

      .admin-content {
        margin-inline-start: 0;
      }

      .admin-sidebar {
        transform: translateX(-102%);
        transition: transform 0.2s ease;
      }

      /* Arabic: the drawer lives on the right, so it slides out that way */
      :host-context([dir='rtl']) .admin-sidebar {
        transform: translateX(102%);
      }

      .nav-open .admin-sidebar {
        transform: none;
      }

      .nav-open .nav-scrim {
        display: block;
      }

      .admin-topbar {
        padding-inline: 0.8rem;
      }

      .admin-main {
        padding: 1.2rem 1.1rem 2.5rem;
      }
    }
  `
})
export class AdminLayout {
  private auth$ = inject(AuthService);
  private api = inject(ApiService);
  private router = inject(Router);

  readonly auth = this.auth$;
  readonly lang = inject(LanguageService);
  readonly navOpen = signal(false);
  readonly unread = signal(0);

  constructor() {
    this.loadUnread();
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => this.loadUnread());
  }

  private loadUnread() {
    this.api.adminGetMessages().subscribe({
      next: messages => this.unread.set(messages.filter(m => !m.isRead).length),
      error: () => this.unread.set(0)
    });
  }

  logout() {
    this.auth$.logout();
    this.router.navigate(['/admin/login']);
  }
}
