import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  afterNextRender,
  afterRenderEffect,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { HttpErrorResponse } from '@angular/common/http';
import { Marked } from 'marked';
import { ReconciliationApi } from '../../../core/api/reconciliation-api.service';
import { isProblemDetails } from '../../../core/models/problem-details.model';
import type { FinanceAssistantResponse } from '../../../core/models/reconciliation.model';

const SUGGESTED_QUESTIONS: readonly string[] = [
  'What is the match rate?',
  'How many unmatched transactions are there?',
  'What exceptions need attention?',
  'Explain TXN-0098',
];

/**
 * One question/answer turn in the on-page conversation thread. This is
 * purely in-memory, page-local presentation state -- it exists only for as
 * long as the Run Workspace stays open (a reload starts a fresh, empty
 * thread) and is never treated as anything the backend persists as a
 * "conversation". Each `ask()` call remains exactly what it always was: one
 * independent `POST /finance-assistant/ask` carrying only `{ runId,
 * question }` -- accumulating turns client-side into a visible thread does
 * not add, imply, or require any server-side session/history concept.
 */
interface AssistantExchange {
  readonly id: number;
  readonly question: string;
  readonly askedAt: number;
  status: 'loading' | 'done' | 'error';
  response: FinanceAssistantResponse | null;
  /**
   * `response.answer` run through the shared Markdown renderer below.
   * Computed once, when the response arrives -- never recomputed on every
   * change-detection pass. Bound via a plain `[innerHTML]` string, which
   * Angular sanitizes automatically (see the renderer's own doc comment
   * for the second, independent layer of defense this stacks with).
   */
  renderedAnswerHtml: string | null;
  errorMessage: string | null;
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * Shared, module-level Markdown parser -- constructed once, not per
 * component instance. Every renderer hook emits plain FinSight Tailwind
 * utility classes (the same tokens/scale every other component in this
 * codebase uses directly in templates) instead of a separate stylesheet,
 * because this is the one place in the frontend that must style content it
 * did not author in a template: the model's own Markdown.
 *
 * Two independent safety layers, deliberately not just one:
 *  1. The `html` hook below never passes a model-supplied raw HTML tag
 *     through as a real element -- it renders it as inert, HTML-escaped
 *     text instead, so a literal `<script>`/`<img onerror>` in the answer
 *     can never become a real DOM node.
 *  2. Angular's own `[innerHTML]` binding (see the template) runs its
 *     built-in sanitizer over the resulting string regardless -- this is
 *     framework behavior neither this file nor the template opts out of.
 * Losing either layer independently still leaves an answer safe to render.
 */
const markdown = new Marked({ gfm: true, breaks: true });

markdown.use({
  renderer: {
    heading({ tokens, depth }): string {
      // Nested one level below the panel's own <h2> -- ### in an answer
      // must never render as large as this codebase's page-level headings
      // (see the "no giant headings inside every answer" constraint).
      const level = Math.min(depth + 2, 6);
      const sizeClass = depth <= 2 ? 'text-body font-semibold' : 'text-small font-semibold';
      const text = this.parser.parseInline(tokens);
      return `<h${level} class="${sizeClass} text-text mt-4 mb-1.5 first:mt-0">${text}</h${level}>`;
    },
    paragraph({ tokens }): string {
      return `<p class="text-body text-text mb-3 last:mb-0">${this.parser.parseInline(tokens)}</p>`;
    },
    strong({ tokens }): string {
      return `<strong class="font-semibold">${this.parser.parseInline(tokens)}</strong>`;
    },
    em({ tokens }): string {
      return `<em class="italic">${this.parser.parseInline(tokens)}</em>`;
    },
    codespan({ text }): string {
      return `<code class="rounded border border-border bg-surface-sunken px-1 py-0.5 text-small font-mono">${escapeHtml(text)}</code>`;
    },
    code({ text }): string {
      return (
        `<pre class="mb-3 overflow-x-auto rounded-md border border-border bg-surface-sunken p-3">` +
        `<code class="text-small font-mono whitespace-pre">${escapeHtml(text)}</code></pre>`
      );
    },
    blockquote({ tokens }): string {
      return `<blockquote class="mb-3 border-l-2 border-border-strong pl-3 text-text-muted">${this.parser.parse(tokens)}</blockquote>`;
    },
    list(token): string {
      const tag = token.ordered ? 'ol' : 'ul';
      const listClass = token.ordered ? 'list-decimal' : 'list-disc';
      const startAttr = token.ordered && token.start !== 1 ? ` start="${token.start}"` : '';
      const items = token.items.map((item) => this.listitem(item)).join('');
      return `<${tag}${startAttr} class="${listClass} mb-3 flex flex-col gap-1 pl-5 text-body text-text last:mb-0">${items}</${tag}>`;
    },
    listitem(item): string {
      return `<li>${this.parser.parse(item.tokens)}</li>`;
    },
    table(token): string {
      const headerCells = token.header.map((cell) => this.tablecell(cell)).join('');
      const bodyRows = token.rows
        .map(
          (row) =>
            `<tr class="border-b border-border last:border-0">${row
              .map((cell) => this.tablecell(cell))
              .join('')}</tr>`,
        )
        .join('');
      return (
        `<div class="mb-3 overflow-x-auto rounded-md border border-border">` +
        `<table class="w-full border-collapse text-small">` +
        `<thead><tr class="border-b border-border-strong">${headerCells}</tr></thead>` +
        `<tbody>${bodyRows}</tbody></table></div>`
      );
    },
    tablecell(token): string {
      const tag = token.header ? 'th' : 'td';
      const align = token.align ? `text-${token.align}` : 'text-left';
      const base = token.header
        ? 'px-3 py-2 text-meta uppercase tracking-wide text-text-faint font-medium'
        : 'px-3 py-2 text-text tabular';
      return `<${tag} class="${base} ${align}">${this.parser.parseInline(token.tokens)}</${tag}>`;
    },
    link({ href, title, tokens }): string {
      const text = this.parser.parseInline(tokens);
      // Only ever emit an http(s)/mailto href -- anything else (in
      // particular a javascript: URL) degrades to a harmless "#", never a
      // live handler. Angular's [innerHTML] sanitizer strips unsafe
      // hrefs regardless; this is the same defense-in-depth stance as the
      // `html` hook below.
      const safeHref = /^(https?:|mailto:)/i.test(href) ? href : '#';
      const titleAttr = title ? ` title="${escapeHtml(title)}"` : '';
      return `<a href="${escapeHtml(safeHref)}"${titleAttr} class="text-accent underline hover:text-accent-strong" target="_blank" rel="noopener noreferrer">${text}</a>`;
    },
    html({ text }): string {
      return escapeHtml(text);
    },
  },
});

function renderAnswerMarkdown(answer: string): string {
  return markdown.parse(answer, { async: false }) as string;
}

function formatRelativeTime(askedAtMs: number, nowMs: number): string {
  const minutes = Math.floor((nowMs - askedAtMs) / 60_000);

  if (minutes < 1) {
    return 'just now';
  }

  if (minutes < 60) {
    return `${minutes}m ago`;
  }

  const hours = Math.floor(minutes / 60);
  return `${hours}h ago`;
}

/**
 * Run-scoped Finance Assistant conversational workspace, embedded directly
 * in the Run Workspace overview -- not a standalone page, not a general
 * chatbot. The assistant answers using backend read-only tools grounded in
 * already-verified reconciliation data (see FinanceAssistantService on the
 * backend); this component computes no financial facts itself. `toolsUsed`
 * is rendered exactly as the backend reports it -- never inferred,
 * reconstructed, or invented here, including while a request is still in
 * flight (the loading state uses only generic, safe copy, never a
 * specific tool name it doesn't yet know).
 *
 * The response contract carries no provider field (see
 * FinanceAssistantResponse) -- only a relative timestamp and, when
 * present, traceId are shown as metadata. A provider name is deliberately
 * never fabricated here.
 *
 * Exactly one request is ever in flight at a time (`pendingId`); a
 * question is only ever sent after an explicit Ask/Retry action, never on
 * load, never queued, never polled.
 */
@Component({
  selector: 'app-finance-assistant-panel',
  imports: [],
  templateUrl: './finance-assistant-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block h-full w-full min-h-0',
  },
})
export class FinanceAssistantPanel {
  private readonly reconciliationApi = inject(ReconciliationApi);
  private nextExchangeId = 0;

  readonly runId = input.required<string>();
  readonly showCloseButton = input<boolean>(false);
  readonly closed = output<void>();

  private readonly conversationContainer = viewChild<ElementRef<HTMLElement>>('conversationContainer');
  private readonly questionTextarea = viewChild<ElementRef<HTMLTextAreaElement>>('questionTextarea');

  /**
   * Compact single-line height and a generous-but-bounded cap -- about
   * six or seven lines before the textarea itself starts scrolling
   * internally, so a long pasted question never grows the composer (or
   * the page) without limit. Matches the `max-h-40` (160px) Tailwind
   * class on the textarea in the template; kept here too since the
   * auto-grow calculation needs the numeric value, not just the class.
   */
  private static readonly ComposerMinHeightPx = 40;
  private static readonly ComposerMaxHeightPx = 160;

  protected readonly suggestedQuestions = SUGGESTED_QUESTIONS;

  protected readonly exchanges = signal<readonly AssistantExchange[]>([]);
  protected readonly questionText = signal('');
  protected readonly pendingId = signal<number | null>(null);

  /**
   * Tracks whether the conversation was scrolled at/near its bottom the
   * last time the user touched the scroll container. Read (not written)
   * by the auto-scroll effect below: a reader who has intentionally
   * scrolled up to re-read an earlier answer is never yanked back down
   * by a loading indicator or a response arriving, but sending a
   * question always re-anchors to "follow latest" -- the same behavior
   * a modern chat UI gives for free.
   */
  private isFollowingBottom = true;

  constructor() {
    // Runs after Angular has actually patched the DOM for any render in
    // which exchanges()/pendingId() changed -- deliberately not the
    // queueMicrotask this replaced, which could fire before OnPush's
    // change detection had flushed the new message into the DOM and so
    // sometimes measured the conversation's old, shorter scrollHeight.
    afterRenderEffect(() => {
      // Read to establish the dependency; the effect reruns whenever
      // either changes.
      this.exchanges();
      this.pendingId();

      const container = this.conversationContainer()?.nativeElement;

      if (container && this.isFollowingBottom) {
        container.scrollTop = container.scrollHeight;
      }
    });

    // One-shot, not reactive: sets the textarea's initial compact height
    // through the exact same calculation typing uses, rather than
    // relying on the `rows="1"` attribute's own natural sizing to happen
    // to agree with ComposerMinHeightPx.
    afterNextRender(() => {
      const textarea = this.questionTextarea()?.nativeElement;

      if (textarea) {
        this.autoGrow(textarea);
      }
    });
  }

  @HostListener('document:keydown.escape', ['$event'])
  protected onEscape(event: Event): void {
    if (this.showCloseButton()) {
      event.preventDefault();
      this.close();
    }
  }

  protected close(): void {
    this.closed.emit();
  }

  /**
   * Threshold (px) below which the user is considered "at the bottom"
   * and new content should keep following automatically.
   */
  private static readonly NearBottomThresholdPx = 96;

  protected onConversationScroll(event: Event): void {
    const el = event.target as HTMLElement;
    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    this.isFollowingBottom = distanceFromBottom < FinanceAssistantPanel.NearBottomThresholdPx;
  }

  /** Frontend-only, never persisted -- see the composer's own doc comment. */
  protected readonly feedbackByExchangeId = signal<ReadonlyMap<number, 'up' | 'down'>>(new Map());
  protected readonly copiedExchangeId = signal<number | null>(null);

  protected readonly canSubmit = computed(
    () => this.questionText().trim().length > 0 && this.pendingId() === null,
  );

  protected readonly isEmpty = computed(() => this.exchanges().length === 0);

  protected onComposerInput(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    this.questionText.set(textarea.value);
    this.autoGrow(textarea);
  }

  /**
   * ChatGPT-style semantics: Enter alone sends; Shift+Enter inserts a
   * real newline. Only Enter-without-Shift is intercepted -- Shift+Enter
   * is left completely alone so the browser's own default textarea
   * behavior (insert "\n" at the caret) handles it, which is simpler and
   * more correct than reimplementing newline insertion by hand.
   */
  protected onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.submit();
    }
  }

  protected useSuggestedQuestion(question: string): void {
    this.questionText.set(question);

    const textarea = this.questionTextarea()?.nativeElement;

    if (textarea) {
      // The signal write above updates the bound [value] on the next
      // change-detection pass, not synchronously -- defer the height
      // recalculation to the same tick that DOM update lands in.
      queueMicrotask(() => this.autoGrow(textarea));
    }
  }

  /**
   * Resets height to the minimum first (never just "auto" left in place
   * -- a shrinking edit, e.g. deleting a pasted paragraph, must be able
   * to shrink back down, not only grow), reads the resulting
   * `scrollHeight` (which now reflects exactly the content with no
   * extra reserved space), then clamps to the configured maximum. Once
   * the content exceeds the maximum, the textarea's own `overflow-y:
   * auto` (see the template's `max-h-40 overflow-y-auto` classes) takes
   * over -- this method never lets the element grow past that cap, so
   * the surrounding page never grows because of the composer.
   */
  private autoGrow(textarea: HTMLTextAreaElement): void {
    textarea.style.height = `${FinanceAssistantPanel.ComposerMinHeightPx}px`;

    const next = Math.min(
      textarea.scrollHeight,
      FinanceAssistantPanel.ComposerMaxHeightPx,
    );

    textarea.style.height = `${Math.max(next, FinanceAssistantPanel.ComposerMinHeightPx)}px`;
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    this.submit();
  }

  protected retry(id: number): void {
    const exchange = this.exchanges().find((e) => e.id === id);

    if (!exchange || this.pendingId() !== null) {
      return;
    }

    this.pendingId.set(id);
    this.patchExchange(id, { status: 'loading', errorMessage: null });
    this.dispatch(id, exchange.question);
  }

  protected copyAnswer(id: number): void {
    const exchange = this.exchanges().find((e) => e.id === id);

    if (!exchange?.response) {
      return;
    }

    navigator.clipboard
      ?.writeText(exchange.response.answer)
      .then(() => {
        this.copiedExchangeId.set(id);
        setTimeout(() => {
          if (this.copiedExchangeId() === id) {
            this.copiedExchangeId.set(null);
          }
        }, 2000);
      })
      .catch(() => {
        // Clipboard access can be denied by the browser/permissions --
        // there is no verified fact at stake here, so this fails silently
        // rather than surfacing an alarming error for a convenience action.
      });
  }

  protected toggleFeedback(id: number, value: 'up' | 'down'): void {
    const next = new Map(this.feedbackByExchangeId());

    if (next.get(id) === value) {
      next.delete(id);
    } else {
      next.set(id, value);
    }

    this.feedbackByExchangeId.set(next);
  }

  protected relativeTime(askedAt: number): string {
    return formatRelativeTime(askedAt, Date.now());
  }

  /** Guards against duplicate submission regardless of how it's triggered. */
  private submit(): void {
    const trimmed = this.questionText().trim();

    if (!trimmed || this.pendingId() !== null) {
      return;
    }

    const id = this.nextExchangeId++;

    const exchange: AssistantExchange = {
      id,
      question: trimmed,
      askedAt: Date.now(),
      status: 'loading',
      response: null,
      renderedAnswerHtml: null,
      errorMessage: null,
    };

    this.exchanges.update((list) => [...list, exchange]);
    this.pendingId.set(id);
    this.questionText.set('');
    // Sending is always an explicit, intentional action -- re-anchor to
    // "follow latest" even if the user had scrolled up to reread
    // something, matching modern chat UIs.
    this.isFollowingBottom = true;

    const textarea = this.questionTextarea()?.nativeElement;

    if (textarea) {
      textarea.style.height = `${FinanceAssistantPanel.ComposerMinHeightPx}px`;
    }

    this.dispatch(id, trimmed);
  }

  private dispatch(id: number, question: string): void {
    this.reconciliationApi.askFinanceAssistant(this.runId(), question).subscribe({
      next: (response) => {
        this.patchExchange(id, {
          status: 'done',
          response,
          renderedAnswerHtml: renderAnswerMarkdown(response.answer),
        });
        this.pendingId.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.patchExchange(id, {
          status: 'error',
          errorMessage: FinanceAssistantPanel.toErrorMessage(error),
        });
        this.pendingId.set(null);
      },
    });
  }

  private patchExchange(id: number, patch: Partial<AssistantExchange>): void {
    this.exchanges.update((list) =>
      list.map((exchange) => (exchange.id === id ? { ...exchange, ...patch } : exchange)),
    );
  }

  /**
   * 503 gets the exact copy the backend now guarantees (F10 hardening) --
   * hardcoded here too so the calm message is guaranteed even if the
   * response body ever lacks a detail, matching the identical pattern in
   * ExceptionDetailPage.toAiErrorMessage for F9's AI surface.
   */
  private static toErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 503) {
      return 'Finance Assistant temporarily unavailable. Reconciliation results are unaffected.';
    }

    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    const detail = isProblemDetails(error.error) ? error.error.detail : undefined;

    if (detail) {
      return detail;
    }

    return 'Could not get an answer from the Finance Assistant. Please try again.';
  }
}
