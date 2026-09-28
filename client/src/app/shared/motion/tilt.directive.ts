import { DestroyRef, Directive, ElementRef, NgZone, afterNextRender, inject } from '@angular/core';
import { hasFinePointer, prefersReducedMotion } from './motion';

const MAX_TILT_DEG = 7;
const EASE = 0.14;

/**
 * Vanilla-tilt style 3D hover: the host leans toward the pointer with a soft
 * light glare. Only writes CSS custom properties (see `.yb-tilt` in
 * styles.scss); the transform itself is only applied while `.is-tilting` is
 * set, so at rest the element has no transform for other animations (the
 * GSAP scroll reveal) to fight with. Mouse/trackpad only.
 */
@Directive({ selector: '[appTilt]', host: { class: 'yb-tilt' } })
export class Tilt {
  constructor() {
    const el: HTMLElement = inject(ElementRef).nativeElement;
    const zone = inject(NgZone);

    const target = { rx: 0, ry: 0, gx: 50, gy: 50, glare: 0 };
    const current = { ...target };
    let frame = 0;
    let hovering = false;

    const render = () => {
      let settled = true;
      for (const key of Object.keys(target) as (keyof typeof target)[]) {
        current[key] += (target[key] - current[key]) * EASE;
        if (Math.abs(target[key] - current[key]) > 0.01) settled = false;
      }
      el.style.setProperty('--tilt-rx', `${current.rx.toFixed(2)}deg`);
      el.style.setProperty('--tilt-ry', `${current.ry.toFixed(2)}deg`);
      el.style.setProperty('--tilt-gx', `${current.gx.toFixed(1)}%`);
      el.style.setProperty('--tilt-gy', `${current.gy.toFixed(1)}%`);
      el.style.setProperty('--tilt-glare', current.glare.toFixed(3));

      if (settled && !hovering) {
        frame = 0;
        el.classList.remove('is-tilting');
        return;
      }
      frame = requestAnimationFrame(render);
    };

    const wake = () => {
      if (!frame) frame = requestAnimationFrame(render);
    };

    const onMove = (event: PointerEvent) => {
      const rect = el.getBoundingClientRect();
      const px = (event.clientX - rect.left) / rect.width;
      const py = (event.clientY - rect.top) / rect.height;
      target.rx = (0.5 - py) * 2 * MAX_TILT_DEG;
      target.ry = (px - 0.5) * 2 * MAX_TILT_DEG;
      target.gx = px * 100;
      target.gy = py * 100;
      target.glare = 1;
      wake();
    };

    const onEnter = (event: PointerEvent) => {
      hovering = true;
      el.classList.add('is-tilting');
      onMove(event);
    };

    const onLeave = () => {
      hovering = false;
      Object.assign(target, { rx: 0, ry: 0, glare: 0 });
      wake();
    };

    let armed = false;
    afterNextRender(() => {
      if (!hasFinePointer() || prefersReducedMotion()) return;
      zone.runOutsideAngular(() => {
        el.addEventListener('pointerenter', onEnter);
        el.addEventListener('pointermove', onMove);
        el.addEventListener('pointerleave', onLeave);
        armed = true;
      });
    });

    inject(DestroyRef).onDestroy(() => {
      if (!armed) return;
      cancelAnimationFrame(frame);
      el.removeEventListener('pointerenter', onEnter);
      el.removeEventListener('pointermove', onMove);
      el.removeEventListener('pointerleave', onLeave);
    });
  }
}
