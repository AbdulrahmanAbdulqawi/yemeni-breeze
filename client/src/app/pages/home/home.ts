import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { LocalizedDatePipe } from '../../core/localized-date.pipe';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, TranslocoPipe, LocalizedDatePipe],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  private api = inject(ApiService);
  readonly lang = inject(LanguageService);

  readonly nextEvent = toSignal(
    this.api.getEvents().pipe(
      map(events => events
        .filter(e => e.status === 'Published' && e.isRegistrationOpen)
        .sort((a, b) => a.date.localeCompare(b.date))[0] ?? null)
    ),
    { initialValue: null }
  );
}
