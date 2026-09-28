import { Component, DestroyRef, ElementRef, NgZone, afterNextRender, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { gsap } from 'gsap';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';
import { EventCard } from '../../shared/event-card';
import { CountUp } from '../../shared/motion/count-up.directive';
import { Reveal } from '../../shared/motion/reveal.directive';
import { QamariyaHero } from './qamariya-hero';

@Component({
  selector: 'app-home',
  imports: [RouterLink, CmsPipe, EventCard, QamariyaHero, Reveal, CountUp],
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

  constructor() {
    const host: HTMLElement = inject(ElementRef).nativeElement;
    const zone = inject(NgZone);
    let motion: gsap.MatchMedia | undefined;

    // Hero entrance: logo settles in, then the copy and actions rise in turn.
    // matchMedia scopes it to users without reduced motion and reverts cleanly.
    afterNextRender(() => {
      zone.runOutsideAngular(() => {
        motion = gsap.matchMedia(host);
        motion.add('(prefers-reduced-motion: no-preference)', () => {
          gsap
            .timeline({ defaults: { ease: 'power3.out', clearProps: 'opacity,transform' } })
            .from('.hero-logo', { opacity: 0, scale: 0.86, y: 18, duration: 1.1 })
            .from(['.hero-title', '.hero-subtitle'], { opacity: 0, y: 26, duration: 0.8, stagger: 0.12 }, '-=0.65')
            .from('.hero-actions', { opacity: 0, y: 18, duration: 0.7 }, '-=0.45');
        });
      });
    });

    inject(DestroyRef).onDestroy(() => motion?.revert());
  }
}
