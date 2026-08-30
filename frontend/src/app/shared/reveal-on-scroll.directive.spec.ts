import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RevealOnScrollDirective } from './reveal-on-scroll.directive';

@Component({
  template: `<div appRevealOnScroll>Content</div>`,
  imports: [RevealOnScrollDirective],
})
class HostComponent {}

describe('RevealOnScrollDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let originalIntersectionObserver: typeof IntersectionObserver;
  let observedCallback: IntersectionObserverCallback | null;
  let disconnectSpy: jasmine.Spy;

  beforeEach(() => {
    observedCallback = null;
    disconnectSpy = jasmine.createSpy('disconnect');

    originalIntersectionObserver = window.IntersectionObserver;

    (window as unknown as { IntersectionObserver: unknown }).IntersectionObserver =
      class {
        constructor(callback: IntersectionObserverCallback) {
          observedCallback = callback;
        }
        observe(): void {}
        disconnect(): void {
          disconnectSpy();
        }
      };
  });

  afterEach(() => {
    window.IntersectionObserver = originalIntersectionObserver;
  });

  function configure(): void {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  }

  function hostEl(): HTMLElement {
    return (fixture.nativeElement as HTMLElement).querySelector('div')!;
  }

  it('does not add is-revealed until the element intersects', () => {
    configure();
    expect(hostEl().classList.contains('is-revealed')).toBeFalse();
  });

  it('adds is-revealed once the element intersects, then disconnects', () => {
    configure();

    observedCallback!(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      {} as IntersectionObserver,
    );

    expect(hostEl().classList.contains('is-revealed')).toBeTrue();
    expect(disconnectSpy).toHaveBeenCalled();
  });

  it('does nothing on a non-intersecting entry', () => {
    configure();

    observedCallback!(
      [{ isIntersecting: false } as IntersectionObserverEntry],
      {} as IntersectionObserver,
    );

    expect(hostEl().classList.contains('is-revealed')).toBeFalse();
  });

  it('reveals immediately when the user prefers reduced motion', () => {
    spyOn(window, 'matchMedia').and.returnValue({
      matches: true,
    } as MediaQueryList);

    configure();

    expect(hostEl().classList.contains('is-revealed')).toBeTrue();
  });

  it('applies the reveal-on-scroll host class', () => {
    configure();
    expect(hostEl().classList.contains('reveal-on-scroll')).toBeTrue();
  });
});
