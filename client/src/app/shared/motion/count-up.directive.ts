import { DestroyRef, Directive, ElementRef, NgZone, afterNextRender, effect, inject, input, untracked } from '@angular/core';
import { gsap } from 'gsap';
import { prefersReducedMotion } from './motion';

/**
 * "1,200+" → prefix "", number "1,200" (grouped by ","), suffix "+".
 * Handles plain integers, 3-digit-grouped integers ("1,200" / "1.200") and
 * simple decimals ("4.5"). Anything else — including Arabic-Indic digits —
 * doesn't match and is shown as-is, un-animated.
 */
const NUMBER = /^(\D*?)(\d{1,3}(?:([,.  ])\d{3})+|\d+(?:\.\d+)?)(\D*)$/;

interface ParsedNumber {
  prefix: string;
  value: number;
  suffix: string;
  format: (n: number) => string;
}

function parse(text: string): ParsedNumber | null {
  const match = NUMBER.exec(text.trim());
  if (!match) return null;
  const [, prefix, digits, groupSep, suffix] = match;

  if (groupSep) {
    const value = Number(digits.split(groupSep).join(''));
    const format = (n: number) => String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, groupSep);
    return { prefix, value, suffix, format };
  }

  const decimals = digits.includes('.') ? digits.split('.')[1].length : 0;
  return { prefix, value: Number(digits), suffix, format: n => n.toFixed(decimals) };
}

/**
 * Renders a stat value (e.g. `[appCountUp]="'home.statViewsValue' | cms"`)
 * and counts it up from zero the first time it scrolls into view. The
 * directive owns the host's text. The value can change after first render —
 * the i18n fallback is replaced by admin-edited CMS copy once that loads, and
 * language switches swap it again — so a change mid-count continues from the
 * number on screen, and a change after counting just shows the new value.
 */
@Directive({ selector: '[appCountUp]' })
export class CountUp {
  readonly appCountUp = input.required<string>();

  private el: HTMLElement = inject(ElementRef).nativeElement;
  private zone = inject(NgZone);
  private observer: IntersectionObserver | null = null;
  private tween: gsap.core.Tween | null = null;
  private shown = 0;
  private inView = false;
  private counted = false;

  constructor() {
    effect(() => {
      const value = this.appCountUp();
      untracked(() => this.show(value));
    });

    afterNextRender(() => {
      if (prefersReducedMotion() || typeof IntersectionObserver === 'undefined') {
        this.counted = true;
        return;
      }
      this.zone.runOutsideAngular(() => {
        this.observer = new IntersectionObserver(
          entries => {
            this.inView = entries.some(e => e.isIntersecting);
            if (this.inView) this.count();
          },
          { rootMargin: '0px 0px -10% 0px' }
        );
        this.observer.observe(this.el);
      });
    });

    inject(DestroyRef).onDestroy(() => {
      this.observer?.disconnect();
      this.tween?.kill();
    });
  }

  private show(value: string) {
    const counting = this.tween !== null;
    this.tween?.kill();
    this.tween = null;
    this.el.textContent = value;
    if (counting || (this.inView && !this.counted)) this.count(counting);
  }

  /** Starts (or, after a value change, resumes) the count toward the current value. */
  private count(resume = false) {
    if (this.counted && !resume) return;

    const text = this.appCountUp();
    const parsed = parse(text);
    // Not a number yet (e.g. the i18n key before translations load): wait for the next value.
    if (!parsed || parsed.value === 0) return;

    this.counted = true;
    this.observer?.disconnect();

    const { prefix, value, suffix, format } = parsed;
    const state = { n: resume ? Math.min(this.shown, value) : 0 };
    const write = () => {
      this.shown = state.n;
      this.el.textContent = prefix + format(state.n) + suffix;
    };
    write();

    this.zone.runOutsideAngular(() => {
      this.tween = gsap.to(state, {
        n: value,
        duration: 1.8,
        ease: 'power2.out',
        onUpdate: write,
        // Land on the exact original string, whatever rounding did en route.
        onComplete: () => {
          this.el.textContent = text;
          this.tween = null;
        }
      });
    });
  }
}
