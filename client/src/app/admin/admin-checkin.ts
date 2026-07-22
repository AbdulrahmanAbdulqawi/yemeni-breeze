import { Component, OnDestroy, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Html5Qrcode } from 'html5-qrcode';
import { ApiService } from '../core/api.service';
import { RegistrationDto } from '../core/models';

@Component({
  selector: 'app-admin-checkin',
  imports: [FormsModule, TranslocoPipe, DatePipe, RouterLink],
  template: `
    <div class="admin-head">
      <h1>{{ 'admin.checkin.title' | transloco }}</h1>
      <a [routerLink]="['/admin/events', eventId(), 'registrations']" class="btn btn-outline">←</a>
    </div>

    <p class="counter">
      <span class="counter-number">{{ checkedInSeats() }}</span> / {{ confirmedSeats() }}
      {{ 'admin.checkin.seatsCheckedIn' | transloco }}
    </p>

    <div class="checkin-grid">
      <!-- Scanner -->
      <div class="card scanner-card">
        <h3>{{ 'admin.checkin.scan' | transloco }}</h3>
        <div id="qr-reader"></div>
        @if (!scanning()) {
          <button class="btn btn-primary" (click)="startScanner()">
            {{ 'admin.checkin.startCamera' | transloco }}
          </button>
        } @else {
          <button class="btn btn-outline" (click)="stopScanner()">
            {{ 'admin.checkin.stopCamera' | transloco }}
          </button>
        }

        @if (lastResult(); as result) {
          <div class="scan-result" [class]="'scan-result ' + result.kind">
            <strong>{{ result.title }}</strong>
            <span>{{ result.detail }}</span>
          </div>
        }
      </div>

      <!-- Manual search fallback -->
      <div class="card search-card">
        <h3>{{ 'admin.checkin.search' | transloco }}</h3>
        <input
          type="search"
          [(ngModel)]="query"
          [placeholder]="'admin.checkin.searchPlaceholder' | transloco" />
        <div class="table-wrap">
          <table class="yb-table">
            <tbody>
              @for (registration of filtered(); track registration.id) {
                <tr>
                  <td>
                    {{ registration.fullName }}<br />
                    <small>{{ registration.email }} · {{ registration.guestsCount }}p</small>
                  </td>
                  <td>
                    @if (registration.checkedInAt) {
                      <span class="badge badge-confirmed">✓ {{ registration.checkedInAt | date: 'HH:mm' }}</span>
                    } @else if (registration.status === 'Confirmed') {
                      <button class="btn btn-primary btn-small" (click)="checkInManually(registration)">
                        {{ 'admin.checkin.checkIn' | transloco }}
                      </button>
                    } @else {
                      <span class="badge badge-waitlisted">{{ registration.status }}</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: `
    .admin-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.6rem;
    }

    .counter {
      font-size: 1.1rem;

      .counter-number {
        font-family: var(--yb-font-heading);
        font-size: 1.8rem;
        color: var(--yb-blue);
        font-weight: 700;
      }
    }

    .checkin-grid {
      display: grid;
      grid-template-columns: minmax(280px, 420px) 1fr;
      gap: 1.6rem;
      align-items: start;

      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }

    .scanner-card,
    .search-card {
      padding: 1.4rem;
      border-top: 5px solid var(--yb-blue);
    }

    #qr-reader {
      width: 100%;
      margin-bottom: 0.8rem;
    }

    .scan-result {
      margin-top: 1rem;
      padding: 0.9rem 1.1rem;
      border-radius: 10px;
      display: flex;
      flex-direction: column;

      &.ok {
        background: #e2f4e5;
        color: #1d7c2e;
      }

      &.warn {
        background: #fdf0d7;
        color: #a86b0e;
      }

      &.error {
        background: #f6dad6;
        color: var(--yb-maroon);
      }
    }

    .search-card input {
      width: 100%;
      padding: 0.65rem 0.9rem;
      border: 1.5px solid #d9c9a8;
      border-radius: 10px;
      margin-bottom: 1rem;
      font-size: 1rem;

      &:focus {
        outline: 2px solid var(--yb-blue);
      }
    }

    .btn-small {
      padding: 0.35rem 1rem;
      font-size: 0.9rem;
    }
  `
})
export class AdminCheckin implements OnDestroy {
  private api = inject(ApiService);

  readonly eventId = input.required<string>();
  readonly registrations = signal<RegistrationDto[]>([]);
  readonly scanning = signal(false);
  readonly lastResult = signal<{ kind: 'ok' | 'warn' | 'error'; title: string; detail: string } | null>(null);
  query = '';

  private scanner: Html5Qrcode | null = null;
  private lastScanned = '';

  readonly confirmedSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Confirmed')
      .reduce((sum, r) => sum + r.guestsCount, 0));

  readonly checkedInSeats = computed(() =>
    this.registrations()
      .filter(r => r.status === 'Confirmed' && r.checkedInAt)
      .reduce((sum, r) => sum + r.guestsCount, 0));

  filtered(): RegistrationDto[] {
    const q = this.query.trim().toLowerCase();
    const rows = this.registrations();
    if (!q) return rows;
    return rows.filter(r =>
      r.fullName.toLowerCase().includes(q) || r.email.toLowerCase().includes(q));
  }

  constructor() {
    effect(() => this.load(+this.eventId()));
  }

  load(eventId: number) {
    this.api.adminGetRegistrations(eventId).subscribe(rows => this.registrations.set(rows));
  }

  async startScanner() {
    this.scanner = new Html5Qrcode('qr-reader');
    this.scanning.set(true);
    try {
      await this.scanner.start(
        { facingMode: 'environment' },
        { fps: 8, qrbox: { width: 220, height: 220 } },
        text => this.onScan(text),
        () => { /* per-frame decode misses are expected */ }
      );
    } catch {
      this.scanning.set(false);
      this.lastResult.set({ kind: 'error', title: 'Camera', detail: 'Camera unavailable — use search instead.' });
    }
  }

  async stopScanner() {
    if (this.scanner) {
      try { await this.scanner.stop(); } catch { /* already stopped */ }
      this.scanner = null;
    }
    this.scanning.set(false);
  }

  private onScan(text: string) {
    if (text === this.lastScanned) return; // debounce same code held in front of camera
    this.lastScanned = text;
    this.api.adminCheckin(+this.eventId(), text).subscribe({
      next: result => {
        this.lastResult.set(result.alreadyCheckedIn
          ? { kind: 'warn', title: result.registration.fullName, detail: '⚠ already checked in' }
          : { kind: 'ok', title: result.registration.fullName, detail: `✓ ${result.registration.guestsCount}p` });
        this.load(+this.eventId());
      },
      error: err => this.lastResult.set({
        kind: 'error',
        title: 'Ticket',
        detail: err.error?.message ?? 'Unknown ticket'
      })
    });
  }

  checkInManually(registration: RegistrationDto) {
    this.api.adminCheckinById(+this.eventId(), registration.id).subscribe(() => this.load(+this.eventId()));
  }

  ngOnDestroy() {
    this.stopScanner();
  }
}
