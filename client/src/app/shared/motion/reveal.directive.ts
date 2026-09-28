import { DestroyRef, Directive, ElementRef, NgZone, afterNextRender, inject } from '@angular/core';
import { gsap } from 'gsap';
import { prefersReducedMotion } from './motion';

/*
 * One IntersectionObserver for every [appReveal] on the page. Elements that
 * enter the viewport in the same frame are revealed together as a GSAP
 * stagger (the ScrollTrigger.batch pattern), so a row of cards cascades in
 * rather than popping all at once.
 *
 * IntersectionObserver (not ScrollTrigger) decides *when* to reveal: its
 * geometry is live, so content that loads in above an element later (API
 * data, images) can't leave it stuck invisible behind a stale trigger point.
 */
let observer: IntersectionObserver | null = null;
const queue = new Set<HTMLElement>();
let flushFrame = 0;

function flush() {
  flushFrame = 0;
  const batch = [...queue].sort((a, b) =>
    a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1
  );
  queue.clear();
  gsap.to(batch, {
    opacity: 1,
    y: 0,
    duration: 0.9,
    ease: 'power3.out',
    stagger: 0.09,
    clearProps: 'opacity,transform'
  });
}

function getObserver(): IntersectionObserver {
  observer ??= new IntersectionObserver(
    entries => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        observer!.unobserve(entry.target);
        queue.add(entry.target as HTMLElement);
      }
      if (queue.size && !flushFrame) flushFrame = requestAnimationFrame(flush);
    },
    { rootMargin: '0px 0px -8% 0px' }
  );
  return observer;
}

/**
 * Fades + lifts the host in the first time it scrolls into view. Only
 * opacity is hidden (never visibility), so hidden elements stay reachable by
 * keyboard — focusing one scrolls it into view, which reveals it.
 */
@Directive({ selector: '[appReveal]' })
export class Reveal {
  constructor() {
    const el: HTMLElement = inject(ElementRef).nativeElement;
    const zone = inject(NgZone);
    let armed = false;

    afterNextRender(() => {
      if (prefersReducedMotion() || typeof IntersectionObserver === 'undefined') return;
      zone.runOutsideAngular(() => {
        gsap.set(el, { opacity: 0, y: 28 });
        getObserver().observe(el);
        armed = true;
      });
    });

    inject(DestroyRef).onDestroy(() => {
      if (!armed) return;
      observer?.unobserve(el);
      queue.delete(el);
      gsap.killTweensOf(el);
    });
  }
}
