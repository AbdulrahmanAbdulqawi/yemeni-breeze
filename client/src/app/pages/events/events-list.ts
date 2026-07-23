import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { EventDto } from '../../core/models';
import { EventCard } from '../../shared/event-card';

@Component({
  selector: 'app-events-list',
  imports: [TranslocoPipe, EventCard],
  templateUrl: './events-list.html',
  styleUrl: './events-list.scss'
})
export class EventsList {
  private api = inject(ApiService);
  readonly lang = inject(LanguageService);

  readonly events = toSignal(this.api.getEvents(), { initialValue: [] as EventDto[] });

  readonly upcoming = computed(() =>
    this.events()
      .filter(e => e.status === 'Published')
      .sort((a, b) => a.date.localeCompare(b.date)));

  readonly past = computed(() =>
    this.events()
      .filter(e => e.status === 'Past')
      .sort((a, b) => b.date.localeCompare(a.date)));
}
