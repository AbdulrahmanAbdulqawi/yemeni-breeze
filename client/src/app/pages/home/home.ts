import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';
import { EventCard } from '../../shared/event-card';

@Component({
  selector: 'app-home',
  imports: [RouterLink, CmsPipe, EventCard],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  private api = inject(ApiService);

  readonly nextEvent = toSignal(
    this.api.getEvents().pipe(
      map(events => events
        .filter(e => e.status === 'Published' && e.isRegistrationOpen)
        .sort((a, b) => a.date.localeCompare(b.date))[0] ?? null)
    ),
    { initialValue: null }
  );

  readonly heroImage = toSignal(
    this.api.getSettings().pipe(map(s => s['heroImageUrl'] || null)),
    { initialValue: null }
  );
}
