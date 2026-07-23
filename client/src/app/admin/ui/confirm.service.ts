import { Injectable, signal } from '@angular/core';

export interface ConfirmRequest {
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  danger: boolean;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly request = signal<ConfirmRequest | null>(null);
  private resolver: ((ok: boolean) => void) | null = null;

  ask(options: Partial<ConfirmRequest> & { message: string }): Promise<boolean> {
    this.request.set({
      confirmLabel: 'OK',
      cancelLabel: 'Cancel',
      danger: false,
      ...options
    });
    return new Promise<boolean>(resolve => (this.resolver = resolve));
  }

  respond(ok: boolean) {
    this.request.set(null);
    this.resolver?.(ok);
    this.resolver = null;
  }
}
