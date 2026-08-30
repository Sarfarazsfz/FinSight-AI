import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { provideRouter } from '@angular/router';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { LandingPage } from './landing-page';

@Component({ template: '', selector: 'app-dummy' })
class DummyComponent {}

describe('LandingPage', () => {
  let fixture: ComponentFixture<LandingPage>;
  let httpMock: HttpTestingController;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [LandingPage],
      providers: [
        provideRouter([
          { path: 'login', component: DummyComponent },
          { path: '', component: DummyComponent },
        ]),
        provideHttpClientTesting(),
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LandingPage);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  // ---------------------------------------------------------------------
  // 1-4: route loads, page renders, hero identity, core message
  // ---------------------------------------------------------------------

  it('renders the landing page', () => {
    configure();
    expect(el().querySelector('[data-testid="landing-hero"]')).toBeTruthy();
  });

  it('renders the FinSight AI hero identity as the page h1', () => {
    configure();
    const h1 = el().querySelector('h1')!;
    expect(h1.textContent).toContain('Reconcile the books.');
    expect(el().textContent).toContain('FinSight');
  });

  it('renders the "AI Finance Controller" positioning', () => {
    configure();
    expect(el().textContent).toContain('AI Finance Controller');
  });

  it('renders the exact core message', () => {
    configure();
    const text = el().textContent!.replace(/\s+/g, ' ');
    expect(text).toContain('Reconcile the books.');
    expect(text).toContain('Understand the exceptions.');
    expect(text).toContain('Verify the cash position.');
  });

  it('does not claim unimplemented capabilities', () => {
    configure();
    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('forecast');
    expect(text).not.toContain('autonomous');
  });

  // ---------------------------------------------------------------------
  // 5-6: hero actions + navigation
  // ---------------------------------------------------------------------

  it('the "Sign in" hero action links to /login', () => {
    configure();
    const link = el().querySelector<HTMLAnchorElement>(
      '[data-testid="landing-hero-sign-in"]',
    )!;
    expect(link.getAttribute('href')).toBe('/login');
  });

  it('the nav "Sign in" action links to /login', () => {
    configure();
    const link = el().querySelector<HTMLAnchorElement>(
      '[data-testid="landing-nav-sign-in"]',
    )!;
    expect(link.getAttribute('href')).toBe('/login');
  });

  it('the nav renders Product, How it works, Trust and AI Assistant links', () => {
    configure();
    const nav = el().querySelector('nav')!;
    expect(nav.textContent).toContain('Product');
    expect(nav.textContent).toContain('How it works');
    expect(nav.textContent).toContain('Trust');
    expect(nav.textContent).toContain('AI Assistant');
  });

  it('"See how it works" smoothly scrolls to the workflow section', () => {
    configure();
    const workflowSection = el().querySelector<HTMLElement>('#workflow')!;
    spyOn(workflowSection, 'scrollIntoView');

    el()
      .querySelector<HTMLButtonElement>('[data-testid="landing-see-how-it-works"]')!
      .click();

    expect(workflowSection.scrollIntoView).toHaveBeenCalledWith(
      jasmine.objectContaining({ block: 'start' }),
    );
  });

  it('the nav "Trust" link scrolls to the trust section', () => {
    configure();
    const trustSection = el().querySelector<HTMLElement>('#trust')!;
    spyOn(trustSection, 'scrollIntoView');

    const trustButton = Array.from(el().querySelectorAll('nav button')).find(
      (b) => b.textContent?.trim() === 'Trust',
    ) as HTMLButtonElement;
    trustButton.click();

    expect(trustSection.scrollIntoView).toHaveBeenCalled();
  });

  // ---------------------------------------------------------------------
  // 5b: hero product visualization
  // ---------------------------------------------------------------------

  it('renders a hero product visualization showing Payment, Bank and Settlement converging to Matched', () => {
    configure();
    const visual = el().querySelector('[data-testid="landing-hero-visual"]')!;

    expect(visual.textContent).toContain('Payment');
    expect(visual.textContent).toContain('Bank');
    expect(visual.textContent).toContain('Settlement');
    expect(visual.textContent).toContain('Matched');
    expect(visual.textContent).toContain('97%');
  });

  it('labels the hero visualization as demo data, not live', () => {
    configure();
    const visual = el().querySelector('[data-testid="landing-hero-visual"]')!;
    expect(visual.textContent).toContain('Example run');
    expect(visual.textContent?.toLowerCase()).toContain('demo data');
  });

  // ---------------------------------------------------------------------
  // 7: six-step workflow section
  // ---------------------------------------------------------------------

  it('renders the six-step workflow with the current step names', () => {
    configure();
    const workflow = el().querySelector('[data-testid="landing-workflow"]')!;

    expect(workflow.textContent).toContain('Upload');
    expect(workflow.textContent).toContain('Normalize');
    expect(workflow.textContent).toContain('Reconcile');
    expect(workflow.textContent).toContain('Verify');
    expect(workflow.textContent).toContain('Investigate');
    expect(workflow.textContent).toContain('Explain');
  });

  it('connects the six workflow steps with a single flow visual, not disconnected cards', () => {
    configure();
    const diagram = el().querySelector('[data-testid="landing-flow-diagram"]')!;
    expect(diagram.getAttribute('role')).toBe('img');
    // Five connecting arrows between six nodes -- proves this is one connected flow
    expect(diagram.querySelectorAll('[aria-hidden="true"] svg').length).toBe(5);
  });

  // ---------------------------------------------------------------------
  // 8: trust section
  // ---------------------------------------------------------------------

  it('renders the trust architecture section distinguishing engine, ground truth and AI', () => {
    configure();
    const trust = el().querySelector('[data-testid="landing-trust"]')!;

    expect(trust.textContent).toContain('Deterministic Engine');
    expect(trust.textContent).toContain('Ground Truth');
    expect(trust.textContent).toContain('AI Investigation');
    expect(trust.textContent!.toLowerCase()).toContain('never decides the financial truth');
  });

  it('states plainly that AI is not the financial source of truth', () => {
    configure();
    const trust = el().querySelector('[data-testid="landing-trust"]')!;
    expect(trust.textContent?.toLowerCase()).toContain(
      'ai is not the financial source of truth',
    );
  });

  // ---------------------------------------------------------------------
  // 9-10: product preview & ground-truth preview
  // ---------------------------------------------------------------------

  it('renders the example run preview, clearly labeled as example/demo data', () => {
    configure();
    const preview = el().querySelector('[data-testid="landing-example-run"]')!;

    expect(preview.textContent).toContain('97%');
    expect(preview.textContent).toContain('Example run');
    expect(preview.textContent?.toLowerCase()).toContain('illustrative demo data');
  });

  it('renders a visible "Ground truth verified" signal on the example run', () => {
    configure();
    const preview = el().querySelector('[data-testid="landing-example-run"]')!;
    expect(preview.textContent).toContain('Ground truth verified');
  });

  it('renders the example exceptions and an AI explanation line', () => {
    configure();
    const preview = el().querySelector('[data-testid="landing-example-run"]')!;

    expect(preview.textContent).toContain('TXN-0098');
    expect(preview.textContent).toContain('TXN-0099');
    expect(preview.textContent).toContain('TXN-0100');
    expect(preview.textContent).toContain('AI explanation');
    expect(preview.textContent).toContain('Missing bank-side records require review.');
  });

  it('renders the AI Assistant preview, clearly labeled as illustration only', () => {
    configure();
    const aiPreview = el().querySelector('[data-testid="landing-ai-preview"]')!;

    expect(aiPreview.textContent).toContain('FinSight Assistant');
    expect(aiPreview.textContent).toContain('getUnmatchedRecords');
    expect(aiPreview.textContent?.toLowerCase()).toContain('illustration only');
  });

  it('allows clicking suggested questions in the assistant preview', () => {
    configure();
    const aiPreview = el().querySelector('[data-testid="landing-ai-preview"]')!;
    const questionButtons = Array.from(aiPreview.querySelectorAll('button'));
    const matchRateBtn = questionButtons.find((b) => b.textContent?.includes('What is our match rate?'));
    expect(matchRateBtn).toBeTruthy();

    matchRateBtn!.click();
    fixture.detectChanges();

    expect(aiPreview.textContent).toContain('getReconciliationSummary');
    expect(aiPreview.textContent).toContain('97.00%');
  });

  // ---------------------------------------------------------------------
  // 11: final CTA
  // ---------------------------------------------------------------------

  it('the final CTA links to /login', () => {
    configure();
    const link = el().querySelector<HTMLAnchorElement>(
      '[data-testid="landing-final-sign-in"]',
    )!;

    expect(el().querySelector('[data-testid="landing-final-cta"]')!.textContent).toContain(
      'Ready to reconcile?',
    );
    expect(link.getAttribute('href')).toBe('/login');
  });

  it('renders a minimal footer with only real, functional actions', () => {
    configure();
    const footer = el().querySelector('[data-testid="landing-footer"]')!;

    expect(footer.textContent).toContain('FinSight AI');
    expect(footer.textContent).toContain('AI Finance Controller');

    // Exactly one real link (Sign in) -- Product/How it works/Trust are in-page buttons
    const links = Array.from(footer.querySelectorAll('a'));
    expect(links.length).toBe(1);
    expect(links[0].getAttribute('href')).toBe('/login');

    expect(footer.textContent).toContain('Product');
    expect(footer.textContent).toContain('How it works');
    expect(footer.textContent).toContain('Trust');
  });

  // ---------------------------------------------------------------------
  // 12: no protected API calls
  // ---------------------------------------------------------------------

  it('makes no HTTP calls of any kind -- the landing page is static', () => {
    configure();
    httpMock.expectNone(() => true);
    expect(true).toBeTrue();
  });

  // ---------------------------------------------------------------------
  // 15: accessibility semantics
  // ---------------------------------------------------------------------

  it('has exactly one h1 and a sensible heading hierarchy', () => {
    configure();
    expect(el().querySelectorAll('h1').length).toBe(1);
    expect(el().querySelectorAll('h2').length).toBeGreaterThanOrEqual(4);
  });

  it('provides a skip-to-content link', () => {
    configure();
    const skipLink = el().querySelector<HTMLAnchorElement>('a[href="#main-content"]');
    expect(skipLink).toBeTruthy();
    expect(el().querySelector('#main-content')).toBeTruthy();
  });

  it('decorative icons are hidden from assistive technology', () => {
    configure();
    const decorativeSvgs = el().querySelectorAll('svg[aria-hidden="true"]');
    expect(decorativeSvgs.length).toBeGreaterThan(0);
  });

  it('the primary nav is an accessible landmark', () => {
    configure();
    expect(el().querySelector('nav[aria-label]')).toBeTruthy();
  });
});

