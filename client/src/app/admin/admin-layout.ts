import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe],
  template: `
    <div class="admin-shell">
      <aside class="admin-sidebar">
        <a routerLink="/" class="admin-brand">
          <img src="/assets/logo.png" alt="Yemeni Breeze" />
        </a>
        <nav>
          <a routerLink="/admin/events" routerLinkActive="active">{{ 'admin.nav.events' | transloco }}</a>
          <a routerLink="/admin/gallery" routerLinkActive="active">{{ 'admin.nav.gallery' | transloco }}</a>
          <a routerLink="/admin/messages" routerLinkActive="active">{{ 'admin.nav.messages' | transloco }}</a>
        </nav>
        <div class="admin-sidebar-footer">
          <a routerLink="/">{{ 'admin.nav.viewSite' | transloco }}</a>
          <button (click)="logout()">{{ 'admin.nav.logout' | transloco }}</button>
        </div>
      </aside>
      <main class="admin-main">
        <router-outlet />
      </main>
    </div>
  `,
  styles: `
    .admin-shell {
      display: grid;
      grid-template-columns: 230px 1fr;
      min-height: 100vh;

      @media (max-width: 760px) {
        grid-template-columns: 1fr;
        grid-template-rows: auto 1fr;
      }
    }

    .admin-sidebar {
      background: var(--yb-brown);
      color: var(--yb-cream);
      padding: 1.4rem 1rem;
      display: flex;
      flex-direction: column;
      gap: 1.6rem;

      .admin-brand img {
        height: 74px;
        display: block;
        margin: 0 auto;
      }

      nav {
        display: flex;
        flex-direction: column;
        gap: 0.3rem;

        a {
          color: var(--yb-cream);
          text-decoration: none;
          font-weight: 600;
          padding: 0.55rem 0.9rem;
          border-radius: 8px;

          &.active,
          &:hover {
            background: var(--yb-maroon);
            color: #fff;
          }
        }
      }
    }

    .admin-sidebar-footer {
      margin-top: auto;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;

      a {
        color: var(--yb-yellow);
        text-decoration: none;
        font-size: 0.92rem;
      }

      button {
        background: transparent;
        border: 1.5px solid var(--yb-cream);
        color: var(--yb-cream);
        border-radius: 8px;
        padding: 0.4rem;
        cursor: pointer;

        &:hover {
          background: var(--yb-cream);
          color: var(--yb-brown);
        }
      }
    }

    .admin-main {
      padding: 2rem;
      background: var(--yb-cream-light);
    }
  `
})
export class AdminLayout {
  private auth = inject(AuthService);
  private router = inject(Router);

  logout() {
    this.auth.logout();
    this.router.navigate(['/admin/login']);
  }
}
