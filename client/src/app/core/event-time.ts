import { EventDto } from './models';

/** Today's date (yyyy-MM-dd) in Amsterdam, where all events are. */
export function todayInAmsterdam(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Amsterdam' }).format(new Date());
}

/**
 * An event is past when an admin marked it so, or once its day is over, even if its status is
 * still "Published". An event stays upcoming for the whole of its own day.
 */
export function isPastEvent(event: EventDto, today = todayInAmsterdam()): boolean {
  return event.status === 'Past' || (event.status === 'Published' && event.date < today);
}
