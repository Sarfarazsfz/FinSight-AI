import {
  Directive,
  ElementRef,
  OnDestroy,
  afterNextRender,
  inject,
} from '@angular/core';

/**
 * Adds the `is-revealed` class the first time the host element scrolls
 * into view, then stops observing -- a one-shot section reveal with no
 * animation library and no continuous work after the reveal fires.
 *
 * Fails open, not closed: if `prefers-reduced-motion` is set, or
 * `IntersectionObserver` is unavailable for any reason, the class is
 * applied immediately. Content is never hidden behind a JavaScript
 * condition that could fail to run -- the CSS pairing this class with
 * (see landing-page.html) only ever animates an *entrance*, never
 * controls whether content exists at all.
 */
@Directive({
  selector: '[appRevealOnScroll]',
  host: {
    class: 'reveal-on-scroll',
  },
})
export class RevealOnScrollDirective implements OnDestroy {
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private observer: IntersectionObserver | null = null;

  constructor() {
    afterNextRender(() => this.startObserving());
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private startObserving(): void {
    const element = this.elementRef.nativeElement;
    const prefersReducedMotion = window.matchMedia?.(
      '(prefers-reduced-motion: reduce)',
    ).matches;

    if (prefersReducedMotion || typeof IntersectionObserver === 'undefined') {
      element.classList.add('is-revealed');
      return;
    }

    this.observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            element.classList.add('is-revealed');
            this.observer?.disconnect();
          }
        }
      },
      { threshold: 0.15 },
    );

    this.observer.observe(element);
  }
}
