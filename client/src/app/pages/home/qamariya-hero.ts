import { Component, DestroyRef, ElementRef, NgZone, afterNextRender, inject, viewChild } from '@angular/core';
import { hasFinePointer, prefersReducedMotion } from '../../shared/motion/motion';
import type { QamariyaScene } from './qamariya-scene';

/**
 * Decorative Three.js layer behind the Home hero (see qamariya-scene.ts).
 * Purely progressive: if WebGL is missing or three.js fails to load, the host
 * just stays empty and the hero looks exactly as it did before. The render
 * loop only runs while the hero is on screen and the tab is visible; with
 * reduced motion a single still frame is drawn instead.
 */
@Component({
  selector: 'app-qamariya-hero',
  template: `<canvas #canvas></canvas>`,
  host: { 'aria-hidden': 'true' },
  styles: `
    :host {
      position: absolute;
      inset: 0;
      pointer-events: none;
      opacity: 0;
      transition: opacity 1.4s ease;
    }

    :host(.is-ready) {
      opacity: 1;
    }

    canvas {
      display: block;
      width: 100%;
      height: 100%;
    }
  `
})
export class QamariyaHero {
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    const host: HTMLElement = inject(ElementRef).nativeElement;
    const zone = inject(NgZone);
    const cleanup: (() => void)[] = [];
    let destroyed = false;

    inject(DestroyRef).onDestroy(() => {
      destroyed = true;
      cleanup.forEach(fn => fn());
    });

    afterNextRender(() => {
      if (typeof WebGLRenderingContext === 'undefined') return;
      zone.runOutsideAngular(async () => {
        let scene: QamariyaScene;
        try {
          const { QamariyaScene } = await import('./qamariya-scene');
          if (destroyed) return;
          scene = new QamariyaScene(this.canvas().nativeElement, {
            compact: host.clientWidth < 640,
            rtl: document.documentElement.dir === 'rtl'
          });
        } catch {
          return; // no WebGL / chunk failed to load: keep the plain hero
        }
        cleanup.push(() => scene.dispose());

        const still = prefersReducedMotion();
        let onScreen = false;
        let running = false;
        const sync = () => {
          const shouldRun = !still && onScreen && !document.hidden;
          if (shouldRun === running) return;
          running = shouldRun;
          if (running) scene.start();
          else scene.stop();
        };

        let heroHeight = host.clientHeight;
        const onScroll = () => scene.setScroll(heroHeight ? window.scrollY / heroHeight : 0);
        scene.setSize(host.clientWidth, heroHeight);
        onScroll();

        const resize = new ResizeObserver(() => {
          heroHeight = host.clientHeight;
          scene.setSize(host.clientWidth, heroHeight);
          onScroll();
          if (still) scene.renderStill();
        });
        resize.observe(host);
        cleanup.push(() => resize.disconnect());

        if (still) {
          host.classList.add('is-ready');
          return;
        }

        const visibility = new IntersectionObserver(entries => {
          onScreen = entries.some(e => e.isIntersecting);
          sync();
        });
        visibility.observe(host);
        cleanup.push(() => visibility.disconnect());

        document.addEventListener('visibilitychange', sync);
        cleanup.push(() => document.removeEventListener('visibilitychange', sync));

        window.addEventListener('scroll', onScroll, { passive: true });
        cleanup.push(() => window.removeEventListener('scroll', onScroll));

        if (hasFinePointer()) {
          const onPointer = (e: PointerEvent) =>
            scene.setPointer((e.clientX / window.innerWidth) * 2 - 1, -((e.clientY / window.innerHeight) * 2 - 1));
          window.addEventListener('pointermove', onPointer, { passive: true });
          cleanup.push(() => window.removeEventListener('pointermove', onPointer));
        }

        host.classList.add('is-ready');
      });
    });
  }
}
