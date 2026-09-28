/**
 * Shared motion preferences for the GSAP / Three.js effects. Every effect
 * checks these before arming itself, so "reduce motion" users and touch
 * devices always get the plain, fully-visible page.
 */
export function prefersReducedMotion(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/** Mouse/trackpad users — pointer-driven effects (tilt, parallax) are skipped on touch. */
export function hasFinePointer(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(hover: hover) and (pointer: fine)').matches;
}
