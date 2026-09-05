import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { FinanceAssistantPanel } from './finance-assistant-panel';
import { environment } from '../../../../environments/environment';
import type { FinanceAssistantResponse } from '../../../core/models/reconciliation.model';

describe('FinanceAssistantPanel', () => {
  let fixture: ComponentFixture<FinanceAssistantPanel>;
  let httpMock: HttpTestingController;

  const runId = '22222222-2222-2222-2222-222222222222';
  const askUrl = `${environment.apiBaseUrl}/finance-assistant/ask`;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [FinanceAssistantPanel],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(FinanceAssistantPanel);
    fixture.componentRef.setInput('runId', runId);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function askButton(): HTMLButtonElement {
    return el().querySelector<HTMLButtonElement>('[data-testid="assistant-ask-button"]')!;
  }

  function questionInput(): HTMLTextAreaElement {
    return el().querySelector<HTMLTextAreaElement>('[data-testid="assistant-question-input"]')!;
  }

  function conversationContainer(): HTMLElement {
    return el().querySelector<HTMLElement>('[data-testid="assistant-conversation-container"]')!;
  }

  function typeQuestion(text: string): void {
    const input = questionInput();
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function pressKey(
    element: HTMLElement,
    key: string,
    options: Partial<KeyboardEventInit> = {},
  ): void {
    const event = new KeyboardEvent('keydown', { key, cancelable: true, ...options });
    element.dispatchEvent(event);
    fixture.detectChanges();
  }

  function response(overrides: Partial<FinanceAssistantResponse> = {}): FinanceAssistantResponse {
    return {
      answer: 'The match rate for this run is 91.5%.',
      toolsUsed: ['getReconciliationSummary'],
      traceId: null,
      ...overrides,
    };
  }

  function exchanges(): NodeListOf<Element> {
    return el().querySelectorAll('[data-testid="assistant-exchange"]');
  }

  function answerTextOf(index: number): HTMLElement {
    return exchanges()[index].querySelector<HTMLElement>('[data-testid="assistant-answer-text"]')!;
  }

  function askAndFlush(question: string, resp: FinanceAssistantResponse = response()): void {
    typeQuestion(question);
    askButton().click();
    httpMock.expectOne(askUrl).flush(resp);
    fixture.detectChanges();
  }

  // ---------------------------------------------------------------------
  // Empty state / suggestions
  // ---------------------------------------------------------------------

  it('renders the empty state with suggested questions on load, with no fake assistant message', () => {
    configure();

    expect(el().querySelector('[data-testid="assistant-empty-state"]')).toBeTruthy();
    expect(el().querySelectorAll('[data-testid="suggested-question"]').length).toBe(4);
    expect(el().textContent).toContain('What is the match rate?');
    expect(el().textContent).toContain('Explain TXN-0098');
    expect(exchanges().length).toBe(0);

    // No AI call on load -- idle means idle.
    expect(httpMock.match(askUrl).length).toBe(0);
  });

  it('indicates the assistant is scoped to the current run and is not the source of financial truth', () => {
    configure();
    expect(el().textContent).toContain('this reconciliation run');
    expect(el().textContent?.toLowerCase()).toContain('never the source of financial truth');
  });

  it('does not render the redundant bottom disclaimer footer, keeping only the top trust statement', () => {
    configure();

    expect(el().textContent).not.toContain('AI-generated analysis may contain errors');
  });

  // ---------------------------------------------------------------------
  // Multiline composer
  // ---------------------------------------------------------------------

  it('is a real textarea, not a single-line input', () => {
    configure();
    expect(questionInput().tagName).toBe('TEXTAREA');
  });

  it('starts at a compact, roughly one-line height', () => {
    configure();
    const height = parseInt(questionInput().style.height || '0', 10);
    expect(height).toBeLessThanOrEqual(48);
    expect(height).toBeGreaterThan(0);
  });

  it('grows as the user types multiple lines', () => {
    configure();
    const textarea = questionInput();
    const compactHeight = parseInt(textarea.style.height, 10);

    textarea.value = 'line one\nline two\nline three\nline four\nline five';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const grownHeight = parseInt(textarea.style.height, 10);
    expect(grownHeight).toBeGreaterThan(compactHeight);
  });

  it('caps growth at the configured maximum height', () => {
    configure();
    const textarea = questionInput();

    textarea.value = Array.from({ length: 40 }, (_, i) => `line ${i}`).join('\n');
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(parseInt(textarea.style.height, 10)).toBeLessThanOrEqual(160);
  });

  it('resets to the compact height after a message is sent', () => {
    configure();
    const textarea = questionInput();

    textarea.value = 'line one\nline two\nline three\nline four';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(parseInt(textarea.style.height, 10)).toBeGreaterThan(40);

    askButton().click();
    fixture.detectChanges();

    expect(parseInt(textarea.style.height, 10)).toBeLessThanOrEqual(40);
    httpMock.expectOne(askUrl).flush(response());
  });

  // ---------------------------------------------------------------------
  // Auto-scroll
  // ---------------------------------------------------------------------

  it('scrolls the conversation to the latest content after sending and after the response arrives', () => {
    configure();

    // An explicit, real pixel height (not just max-height) so the
    // container genuinely overflows in the real headless-Chrome layout
    // this suite runs in, regardless of the ambient unstyled test host.
    const container = conversationContainer();
    container.style.height = '120px';
    fixture.detectChanges();

    for (let i = 0; i < 5; i++) {
      askAndFlush(
        `Question number ${i}`,
        response({ answer: 'A reasonably long answer with enough text to take up real vertical space.' }),
      );
    }

    expect(container.scrollTop + container.clientHeight).toBeGreaterThanOrEqual(
      container.scrollHeight - 2,
    );
  });

  it('does not force-scroll away from history the user intentionally scrolled up to read', () => {
    configure();

    const container = conversationContainer();
    container.style.height = '120px';
    fixture.detectChanges();

    for (let i = 0; i < 5; i++) {
      askAndFlush(
        `Question number ${i}`,
        response({ answer: 'A reasonably long answer with enough text to take up real vertical space.' }),
      );
    }

    // Scroll away from the bottom, as a user re-reading earlier history
    // would.
    container.scrollTop = 0;
    container.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    // Retrying an earlier exchange is not the same as sending a fresh
    // question -- it must not yank the reader back to the bottom either
    // while loading or once the retried answer completes.
    typeQuestion('One more, which will fail');
    askButton().click();
    httpMock.expectOne(askUrl).flush({ detail: 'boom' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    container.scrollTop = 0;
    container.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    el().querySelector<HTMLButtonElement>('[data-testid="assistant-retry-button"]')!.click();
    fixture.detectChanges();

    expect(container.scrollTop).toBe(0);

    httpMock.expectOne(askUrl).flush(response());
    fixture.detectChanges();

    expect(container.scrollTop).toBe(0);
  });

  it('clicking a suggested question fills the input without submitting', () => {
    configure();

    el().querySelectorAll<HTMLButtonElement>('[data-testid="suggested-question"]')[0].click();
    fixture.detectChanges();

    expect(questionInput().value).toBe('What is the match rate?');
    expect(httpMock.match(askUrl).length).toBe(0);
  });

  // ---------------------------------------------------------------------
  // Composer
  // ---------------------------------------------------------------------

  it('disables Send when the question is blank, enables it once text is entered', () => {
    configure();

    expect(askButton().disabled).toBeTrue();

    typeQuestion('What is the match rate?');

    expect(askButton().disabled).toBeFalse();
  });

  it('disables Send for whitespace-only input', () => {
    configure();

    typeQuestion('   ');

    expect(askButton().disabled).toBeTrue();
  });

  it('pressing Enter submits the question', () => {
    configure();
    typeQuestion('What is the match rate?');

    pressKey(questionInput(), 'Enter');

    const req = httpMock.expectOne((r) => r.url === askUrl && r.method === 'POST');
    expect(req.request.body).toEqual({ runId, question: 'What is the match rate?' });
    req.flush(response());
  });

  it('Shift+Enter does not submit -- it is reserved for inserting a newline in the multiline composer', () => {
    configure();
    typeQuestion('What is the match rate?');

    pressKey(questionInput(), 'Enter', { shiftKey: true });

    expect(httpMock.match(askUrl).length).toBe(0);
  });

  it('plain Enter (no Shift) submits', () => {
    configure();
    typeQuestion('What is the match rate?');

    pressKey(questionInput(), 'Enter');

    const req = httpMock.expectOne((r) => r.url === askUrl && r.method === 'POST');
    expect(req.request.body).toEqual({ runId, question: 'What is the match rate?' });
    req.flush(response());
  });

  it('placeholder is clean -- no keyboard-shortcut hint text', () => {
    configure();

    expect(questionInput().placeholder).not.toContain('Shift+Enter');
    expect(questionInput().placeholder).toBe('Ask about this run...');
  });

  it('clicking Send POSTs to the finance-assistant endpoint with the current runId and exact question', () => {
    configure();
    typeQuestion('What is the match rate?');

    askButton().click();

    const req = httpMock.expectOne((r) => r.url === askUrl && r.method === 'POST');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      runId,
      question: 'What is the match rate?',
    });

    req.flush(response());
  });

  it('clears the composer once a question is submitted', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    fixture.detectChanges();

    expect(questionInput().value).toBe('');
    httpMock.expectOne(askUrl).flush(response());
  });

  // ---------------------------------------------------------------------
  // Loading state
  // ---------------------------------------------------------------------

  it('shows safe, generic progress copy while pending -- never a specific tool name before the response arrives', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    fixture.detectChanges();

    const loading = el().querySelector('[data-testid="assistant-loading"]')!;
    expect(loading).toBeTruthy();
    expect(loading.textContent).toContain('Checking verified reconciliation data');
    expect(loading.textContent).not.toContain('getReconciliationSummary');
    expect(questionInput().disabled).toBeTrue();
    expect(askButton().disabled).toBeTrue();

    httpMock.expectOne(askUrl).flush(response());
  });

  it('prevents duplicate submission while a request is already in flight', () => {
    configure();
    typeQuestion('What is the match rate?');

    const form = el().querySelector('form')!;

    form.dispatchEvent(new Event('submit', { cancelable: true }));
    form.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(httpMock.match(askUrl).length).toBe(1);
    httpMock.match(askUrl)[0]?.flush(response());
  });

  it('re-enables the composer after the request completes', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(response());
    fixture.detectChanges();

    expect(questionInput().disabled).toBeFalse();
  });

  // ---------------------------------------------------------------------
  // Conversation thread
  // ---------------------------------------------------------------------

  it('renders the question and answer as a conversation turn on success', () => {
    configure();
    askAndFlush('What is the match rate?', response({ answer: 'The match rate is 91.5%.' }));

    expect(exchanges().length).toBe(1);
    expect(
      exchanges()[0].querySelector('[data-testid="assistant-question-text"]')!.textContent,
    ).toContain('What is the match rate?');
    expect(answerTextOf(0).textContent).toContain('The match rate is 91.5%.');
  });

  it('accumulates a real multi-turn conversation without losing earlier turns', () => {
    configure();
    askAndFlush('What is the match rate?', response({ answer: 'Match rate is 91.5%.' }));
    askAndFlush('Which exceptions need attention?', response({ answer: '3 exceptions need review.' }));

    expect(exchanges().length).toBe(2);
    expect(
      exchanges()[0].querySelector('[data-testid="assistant-question-text"]')!.textContent,
    ).toContain('What is the match rate?');
    expect(answerTextOf(0).textContent).toContain('Match rate is 91.5%.');
    expect(
      exchanges()[1].querySelector('[data-testid="assistant-question-text"]')!.textContent,
    ).toContain('Which exceptions need attention?');
    expect(answerTextOf(1).textContent).toContain('3 exceptions need review.');
  });

  // ---------------------------------------------------------------------
  // Markdown rendering
  // ---------------------------------------------------------------------

  it('renders a Markdown heading as a real heading element, never literal "##"', () => {
    configure();
    askAndFlush('Summarize', response({ answer: '## Match rate\n\nSummary text.' }));

    const heading = answerTextOf(0).querySelector('h3, h4, h5, h6');
    expect(heading).toBeTruthy();
    expect(heading!.textContent).toContain('Match rate');
    expect(answerTextOf(0).textContent).not.toContain('##');
  });

  it('renders Markdown bold as a real <strong> element, never literal "**"', () => {
    configure();
    askAndFlush('Summarize', response({ answer: 'This run is **fully reconciled**.' }));

    const strong = answerTextOf(0).querySelector('strong');
    expect(strong).toBeTruthy();
    expect(strong!.textContent).toContain('fully reconciled');
    expect(answerTextOf(0).textContent).not.toContain('**');
  });

  it('renders a Markdown unordered list as real <ul>/<li> elements', () => {
    configure();
    askAndFlush('List', response({ answer: '- First item\n- Second item' }));

    const items = answerTextOf(0).querySelectorAll('ul > li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('First item');
    expect(items[1].textContent).toContain('Second item');
  });

  it('renders a Markdown ordered list as real <ol>/<li> elements', () => {
    configure();
    askAndFlush('List', response({ answer: '1. Review TXN-0098\n2. Review TXN-0099' }));

    const items = answerTextOf(0).querySelectorAll('ol > li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Review TXN-0098');
  });

  it('renders a Markdown table as a real, readable <table> with right-aligned numeric columns', () => {
    configure();
    const table = [
      '| Metric | Count |',
      '|---|---:|',
      '| Matched | 97 |',
      '| Missing | 3 |',
    ].join('\n');

    askAndFlush('Table', response({ answer: table }));

    const tableEl = answerTextOf(0).querySelector('table');
    expect(tableEl).toBeTruthy();
    expect(tableEl!.querySelectorAll('thead th').length).toBe(2);
    expect(tableEl!.querySelectorAll('tbody tr').length).toBe(2);

    const numericCell = tableEl!.querySelectorAll('tbody td')[1] as HTMLElement;
    expect(numericCell.textContent).toContain('97');
    expect(numericCell.className).toContain('text-right');
  });

  it('renders inline code as a real <code> element', () => {
    configure();
    askAndFlush('Tool', response({ answer: 'Use `getReconciliationSummary` to verify.' }));

    const code = answerTextOf(0).querySelector('code');
    expect(code).toBeTruthy();
    expect(code!.textContent).toBe('getReconciliationSummary');
  });

  it('renders a fenced code block safely, preserving its literal content as text', () => {
    configure();
    askAndFlush('Payload', response({ answer: '```json\n{ "matchRate": 0.97 }\n```' }));

    const block = answerTextOf(0).querySelector('pre code');
    expect(block).toBeTruthy();
    expect(block!.textContent).toContain('{ "matchRate": 0.97 }');
  });

  it('never executes or renders a real element for HTML embedded in the answer -- sanitized to inert text', () => {
    configure();
    askAndFlush(
      'Explain',
      response({ answer: 'Hello <img src=x onerror="window.__xss=true"> world.' }),
    );

    expect((window as unknown as { __xss?: boolean }).__xss).toBeUndefined();
    expect(answerTextOf(0).querySelector('img')).toBeFalsy();
    expect(answerTextOf(0).textContent).toContain('<img src=x');
  });

  it('never executes a script tag embedded in the answer', () => {
    configure();
    askAndFlush(
      'Explain',
      response({ answer: 'Hello <script>window.__xss2=true</script> world.' }),
    );

    expect((window as unknown as { __xss2?: boolean }).__xss2).toBeUndefined();
    expect(answerTextOf(0).querySelector('script')).toBeFalsy();
  });

  // ---------------------------------------------------------------------
  // Metadata, tool trace, verified-data signal
  // ---------------------------------------------------------------------

  it('renders lightweight relative-time metadata for the answer', () => {
    configure();
    askAndFlush('What is the match rate?');

    expect(
      exchanges()[0].querySelector('[data-testid="assistant-metadata"]')!.textContent,
    ).toContain('just now');
  });

  it('does not render a trace field when traceId is null', () => {
    configure();
    askAndFlush('What is the match rate?', response({ traceId: null }));

    expect(el().querySelector('[data-testid="assistant-trace-id"]')).toBeFalsy();
  });

  it('renders traceId as secondary metadata only when present', () => {
    configure();
    askAndFlush('What is the match rate?', response({ traceId: 'trace-abc-123' }));

    expect(el().querySelector('[data-testid="assistant-trace-id"]')!.textContent).toContain(
      'trace-abc-123',
    );
  });

  it('renders toolsUsed exactly as returned by the backend, in order', () => {
    configure();
    askAndFlush('Explain TXN-0098', response({ toolsUsed: ['getExceptionDetails'] }));

    const chips = el().querySelectorAll('[data-testid="assistant-tool-chip"]');
    expect(chips.length).toBe(1);
    expect(chips[0].textContent).toContain('getExceptionDetails');
  });

  it('renders multiple tool chips correctly', () => {
    configure();
    askAndFlush(
      'Summarize this run.',
      response({ toolsUsed: ['getReconciliationSummary', 'getUnmatchedRecords'] }),
    );

    const chips = el().querySelectorAll('[data-testid="assistant-tool-chip"]');
    expect(chips.length).toBe(2);
    expect(chips[0].textContent).toContain('getReconciliationSummary');
    expect(chips[1].textContent).toContain('getUnmatchedRecords');
  });

  it('never invents a tool when toolsUsed is empty', () => {
    configure();
    askAndFlush('What is a reconciliation run?', response({ toolsUsed: [] }));

    expect(el().querySelector('[data-testid="assistant-no-tools"]')!.textContent).toContain(
      'No backend tools were used',
    );
    expect(el().querySelector('[data-testid="assistant-tools-used"]')).toBeFalsy();
  });

  // ---------------------------------------------------------------------
  // Copy / feedback
  // ---------------------------------------------------------------------

  it('copies the exact answer text to the clipboard', async () => {
    configure();
    askAndFlush('What is the match rate?', response({ answer: 'The match rate is 91.5%.' }));

    spyOn(navigator.clipboard, 'writeText').and.resolveTo();

    el().querySelector<HTMLButtonElement>('[data-testid="assistant-copy-button"]')!.click();

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('The match rate is 91.5%.');
  });

  it('toggles helpful/not-helpful feedback state independently, frontend-only', () => {
    configure();
    askAndFlush('What is the match rate?');

    const up = el().querySelector<HTMLButtonElement>('[data-testid="assistant-feedback-up"]')!;
    const down = el().querySelector<HTMLButtonElement>('[data-testid="assistant-feedback-down"]')!;

    expect(up.getAttribute('aria-pressed')).toBe('false');

    up.click();
    fixture.detectChanges();
    expect(up.getAttribute('aria-pressed')).toBe('true');

    up.click();
    fixture.detectChanges();
    expect(up.getAttribute('aria-pressed')).toBe('false');

    down.click();
    fixture.detectChanges();
    expect(down.getAttribute('aria-pressed')).toBe('true');
  });

  // ---------------------------------------------------------------------
  // Error handling
  // ---------------------------------------------------------------------

  it('shows the exact required 503 message and a Retry action', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'AI Provider Unavailable', status: 503 },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="assistant-error-message"]')!.textContent).toContain(
      'Finance Assistant temporarily unavailable. Reconciliation results are unaffected.',
    );
    expect(el().querySelector('[data-testid="assistant-retry-button"]')).toBeTruthy();
  });

  it('does not automatically retry after a 503', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'AI Provider Unavailable', status: 503 },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    expect(httpMock.match(askUrl).length).toBe(0);
  });

  it('Retry re-issues a new request for the same turn, using the same question, without duplicating the thread', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'AI Provider Unavailable', status: 503 },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    expect(exchanges().length).toBe(1);

    el().querySelector<HTMLButtonElement>('[data-testid="assistant-retry-button"]')!.click();

    const req = httpMock.expectOne((r) => r.url === askUrl && r.method === 'POST');
    expect(req.request.body).toEqual({ runId, question: 'What is the match rate?' });
    req.flush(response());
    fixture.detectChanges();

    expect(exchanges().length).toBe(1);
    expect(answerTextOf(0).textContent).toContain('The match rate for this run is 91.5%.');
  });

  it('shows the backend detail message on a 400 inline, without a stack trace', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'Bad Request', status: 400, detail: 'question is required.' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="assistant-error-message"]')!.textContent).toContain(
      'question is required.',
    );
    expect(el().textContent).not.toContain('System.');
  });

  it('shows a calm generic message on an unexpected 500, without a stack trace', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'An unexpected error occurred.', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    const message = el().querySelector('[data-testid="assistant-error-message"]')!.textContent!;
    expect(message).not.toContain('System.');
    expect(message).not.toContain('Exception');
    expect(message.length).toBeGreaterThan(0);
  });

  it('does not render bespoke session-expired copy on a 401 -- that is the global interceptor’s job', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    fixture.detectChanges();

    const text = el().textContent!.toLowerCase();
    expect(text).not.toContain('session');
    expect(text).not.toContain('expired');
  });

  it('a failed turn does not block asking a further, different question', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();
    httpMock.expectOne(askUrl).flush(
      { title: 'AI Provider Unavailable', status: 503 },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    expect(questionInput().disabled).toBeFalse();
    expect(askButton().disabled).toBeTrue(); // still true because the input is empty
  });

  // ---------------------------------------------------------------------
  // Contract / scope
  // ---------------------------------------------------------------------

  it('sends exactly the existing request shape -- no new fields added to support the new UI', () => {
    configure();
    typeQuestion('What is the match rate?');
    askButton().click();

    const req = httpMock.expectOne((r) => r.url === askUrl && r.method === 'POST');
    expect(Object.keys(req.request.body as object).sort()).toEqual(['question', 'runId']);
    req.flush(response());
  });

  it('exposes no runId input control -- runId comes only from the component input', () => {
    configure();

    expect(el().querySelector('input[name="runId"]')).toBeFalsy();
    expect(el().textContent?.toLowerCase()).not.toContain('run id:');
  });
});
