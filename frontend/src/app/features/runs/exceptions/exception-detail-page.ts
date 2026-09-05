import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import type { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ReconciliationApi } from '../../../core/api/reconciliation-api.service';
import { isProblemDetails } from '../../../core/models/problem-details.model';
import type {
  AiExplanationResponse,
  ReconciliationExceptionCategory,
  ReconciliationExceptionResponse,
  ReconciliationMatchStatus,
  ReconciliationTransactionDetailResponse,
} from '../../../core/models/reconciliation.model';

const PAGE_SIZE = 50;

type DetailState = 'loading' | 'loaded' | 'not-found' | 'error';

type AiState = 'idle' | 'loading' | 'loaded' | 'error';

/**
 * What the AI panel actually renders. Structurally compatible with
 * `AiExplanationResponse` (a fresh POST result assigns directly), but
 * `provider` is nullable here because a *pre-existing* explanation --
 * one already persisted on the exception when the page first loads --
 * only carries `aiExplanation`/`aiSuggestedCategory`/`aiExplanationGeneratedAt`
 * on `ReconciliationExceptionResponse`; the backend does not persist which
 * provider generated it. The panel omits the provider line rather than
 * inventing a value in that case.
 */
interface AiExplanationView {
  provider: string | null;
  explanation: string;
  suggestedCategory: string | null;
  generatedAtUtc: string;
}

/**
 * Exception investigation -- fetches the exception itself, its source
 * evidence (reusing F7's exact `getResultDetail` call and rendering
 * pattern), and enough of its queue page to support Previous/Next.
 *
 * This component is reused by the router across sibling exceptions (same
 * route, only the `:exceptionId` path param changes), so it reads route
 * params reactively via `route.paramMap`, never from a one-time
 * `route.snapshot` read -- a snapshot captured once would go stale the
 * moment Previous/Next navigates to a sibling without destroying this
 * component instance.
 *
 * `discrepancyDetail` is rendered as pretty-printed JSON with a raw-string
 * fallback -- its shape is real (verified from
 * ReconciliationOrchestrator.BuildExceptionDetail) but is not a
 * compiler-enforced contract, so no bespoke structured UI is built for it.
 *
 * F9: an AI explanation panel renders below the evidence/discrepancy
 * content, entirely independent of the page's own `state` -- an AI
 * request, its loading state, or its failure never hides, disables, or
 * reloads the deterministic evidence above it. If the exception fetched
 * on load already carries a persisted `aiExplanation`, the panel shows it
 * immediately without issuing a new AI request.
 */
@Component({
  selector: 'app-exception-detail-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './exception-detail-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // See BatchesPage for the full rationale. This page is a naturally
    // document-flowing investigation view (classification, AI panel,
    // evidence cards), so page-level vertical scroll is appropriate --
    // this binding only establishes correct height participation inside
    // AppShell's <main>.
    class: 'flex-1 min-h-0 flex flex-col',
  },
})
export class ExceptionDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reconciliationApi = inject(ReconciliationApi);

  protected readonly runId = signal('');
  protected readonly exceptionId = signal('');

  protected readonly state = signal<DetailState>('loading');
  protected readonly exception = signal<ReconciliationExceptionResponse | null>(null);
  protected readonly evidence = signal<ReconciliationTransactionDetailResponse | null>(null);
  protected readonly discrepancyPretty = signal('');
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly queuePageNumber = signal(1);
  protected readonly queueItems = signal<ReconciliationExceptionResponse[]>([]);
  protected readonly queueTotalPages = signal(0);
  protected readonly navigatingBoundary = signal(false);

  // Independent of `state` above by design -- see class doc comment.
  protected readonly aiState = signal<AiState>('idle');
  protected readonly aiExplanation = signal<AiExplanationView | null>(null);
  protected readonly aiErrorMessage = signal<string | null>(null);

  private readonly resultHeading = viewChild<ElementRef<HTMLElement>>('resultHeading');
  private isFirstLoad = true;

  protected readonly indexInQueue = computed(() =>
    this.queueItems().findIndex((item) => item.exceptionId === this.exceptionId()),
  );

  protected readonly hasPrevious = computed(
    () => this.indexInQueue() > 0 || this.queuePageNumber() > 1,
  );

  protected readonly hasNext = computed(() => {
    const idx = this.indexInQueue();
    return (
      (idx >= 0 && idx < this.queueItems().length - 1) ||
      this.queuePageNumber() < this.queueTotalPages()
    );
  });

  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'loading':
        return 'Loading exception…';
      case 'loaded':
        return `Investigating ${this.exception()?.transactionReference ?? ''}.`;
      case 'not-found':
        return 'Exception not found.';
      case 'error':
        return this.errorMessage() ?? 'Could not load exception.';
    }
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const runId = params.get('runId');
      const exceptionId = params.get('exceptionId');

      if (!runId || !exceptionId) {
        this.state.set('not-found');
        return;
      }

      const queuePage = Number(this.route.snapshot.queryParamMap.get('page')) || 1;

      this.runId.set(runId);
      this.exceptionId.set(exceptionId);
      this.load(exceptionId, queuePage);
    });
  }

  protected retry(): void {
    this.load(this.exceptionId(), this.queuePageNumber());
  }

  /**
   * Never fires automatically -- only ever called from the panel's own
   * button (both the initial "Explain this exception" action and the
   * 503/error Retry action). Independent of `state`/`retry()` above: an
   * AI failure only ever affects `aiState`, never the evidence.
   */
  protected requestAiExplanation(): void {
    const exceptionId = this.exceptionId();

    if (!exceptionId || this.aiState() === 'loading') {
      return;
    }

    this.aiState.set('loading');
    this.aiErrorMessage.set(null);

    this.reconciliationApi.generateAiExplanation(exceptionId).subscribe({
      next: (response: AiExplanationResponse) => {
        this.aiExplanation.set(response);
        this.aiState.set('loaded');
      },
      error: (error: HttpErrorResponse) => {
        this.aiErrorMessage.set(ExceptionDetailPage.toAiErrorMessage(error));
        this.aiState.set('error');
      },
    });
  }

  protected statusBadgeClasses(status: ReconciliationMatchStatus): string {
    switch (status) {
      case 'Matched':
        return 'bg-matched-bg text-matched';
      case 'Mismatched':
        return 'bg-mismatched-bg text-mismatched';
      case 'Missing':
        return 'bg-missing-bg text-missing';
      case 'Duplicate':
        return 'bg-duplicate-bg text-duplicate';
      case 'Unresolved':
        return 'bg-unresolved-bg text-unresolved';
    }
  }

  protected categoryBadgeClasses(category: ReconciliationExceptionCategory): string {
    switch (category) {
      case 'AmountMismatch':
      case 'DateMismatch':
        return 'bg-mismatched-bg text-mismatched';
      case 'MissingRecord':
        return 'bg-missing-bg text-missing';
      case 'DuplicateRecord':
        return 'bg-duplicate-bg text-duplicate';
      case 'Unresolved':
        return 'bg-unresolved-bg text-unresolved';
    }
  }

  protected formatInvolvedSources(involvedSources: string): string {
    return involvedSources
      .split(',')
      .map((source) => source.trim())
      .filter((source) => source.length > 0)
      .join(', ');
  }

  protected goToPrevious(): void {
    if (this.navigatingBoundary()) {
      return;
    }

    const idx = this.indexInQueue();

    if (idx > 0) {
      this.navigateTo(this.queueItems()[idx - 1].exceptionId, this.queuePageNumber());
      return;
    }

    if (this.queuePageNumber() > 1) {
      this.crossBoundary(this.queuePageNumber() - 1, 'last');
    }
  }

  protected goToNext(): void {
    if (this.navigatingBoundary()) {
      return;
    }

    const idx = this.indexInQueue();

    if (idx >= 0 && idx < this.queueItems().length - 1) {
      this.navigateTo(this.queueItems()[idx + 1].exceptionId, this.queuePageNumber());
      return;
    }

    if (this.queuePageNumber() < this.queueTotalPages()) {
      this.crossBoundary(this.queuePageNumber() + 1, 'first');
    }
  }

  /**
   * Fetches exactly one adjacent queue page, then jumps to its first/last
   * item. The fetched page is stored immediately -- before navigating --
   * so that `load()` recognizes the destination exception as already known
   * and does not fetch the same page a second time.
   */
  private crossBoundary(targetPage: number, edge: 'first' | 'last'): void {
    const runId = this.exception()?.runId;

    if (!runId) {
      return;
    }

    this.navigatingBoundary.set(true);

    this.reconciliationApi.getExceptions(runId, targetPage, PAGE_SIZE).subscribe({
      next: (response) => {
        this.navigatingBoundary.set(false);

        if (response.items.length > 0) {
          this.queuePageNumber.set(response.pageNumber);
          this.queueTotalPages.set(response.totalPages);
          this.queueItems.set(response.items);

          const target = edge === 'first' ? response.items[0] : response.items[response.items.length - 1];
          this.navigateTo(target.exceptionId, targetPage);
        }
      },
      error: () => this.navigatingBoundary.set(false),
    });
  }

  private navigateTo(exceptionId: string, page: number): void {
    void this.router.navigate(['/runs', this.runId(), 'exceptions', exceptionId], {
      queryParams: { page },
    });
  }

  private load(exceptionId: string, queuePageNumber: number): void {
    this.state.set('loading');
    this.errorMessage.set(null);
    this.exception.set(null);
    this.evidence.set(null);

    // Reset for the new exception -- this component instance is reused
    // across sibling Previous/Next navigations (see class doc comment),
    // so a stale AI panel from the previous exception must not persist.
    this.aiState.set('idle');
    this.aiExplanation.set(null);
    this.aiErrorMessage.set(null);

    this.reconciliationApi.getException(exceptionId).subscribe({
      next: (exception) => {
        this.exception.set(exception);
        this.discrepancyPretty.set(ExceptionDetailPage.prettyPrint(exception.discrepancyDetail));
        this.loadEvidence(exception);

        if (exception.aiExplanation) {
          // Already explained in a prior request -- show it directly,
          // never issue a redundant AI call.
          this.aiExplanation.set({
            provider: null,
            explanation: exception.aiExplanation,
            suggestedCategory: exception.aiSuggestedCategory,
            generatedAtUtc: exception.aiExplanationGeneratedAt ?? '',
          });
          this.aiState.set('loaded');
        }

        // The component instance is reused across sibling navigations
        // (same route, only :exceptionId changes), so `queueItems` from a
        // Previous/Next click genuinely carries over. If the destination
        // is already in the held page, no additional queue fetch is
        // issued -- only a true cold load or a page-boundary crossing
        // (which already stored its fetched page in `crossBoundary`)
        // fetches here.
        const alreadyKnown = this.queueItems().some((item) => item.exceptionId === exceptionId);

        if (!alreadyKnown || this.queuePageNumber() !== queuePageNumber) {
          this.loadQueuePosition(exception.runId, queuePageNumber);
        }
      },
      error: (error: HttpErrorResponse) => this.handleError(error),
    });
  }

  private loadEvidence(exception: ReconciliationExceptionResponse): void {
    this.reconciliationApi
      .getResultDetail(exception.runId, exception.reconciliationResultId)
      .subscribe({
        next: (evidence) => {
          this.evidence.set(evidence);
          this.state.set('loaded');

          if (!this.isFirstLoad) {
            this.restoreFocus();
          }

          this.isFirstLoad = false;
        },
        error: (error: HttpErrorResponse) => this.handleError(error),
      });
  }

  /**
   * Non-fatal: if this fails, Previous/Next simply become unavailable --
   * the exception and its evidence are already real and already shown, and
   * queue position is a navigation convenience, not core content.
   */
  private loadQueuePosition(runId: string, pageNumber: number): void {
    this.reconciliationApi.getExceptions(runId, pageNumber, PAGE_SIZE).subscribe({
      next: (response) => {
        this.queuePageNumber.set(response.pageNumber);
        this.queueTotalPages.set(response.totalPages);
        this.queueItems.set(response.items);
      },
      error: () => {
        this.queueItems.set([]);
        this.queueTotalPages.set(0);
      },
    });
  }

  private handleError(error: HttpErrorResponse): void {
    if (error.status === 404) {
      this.state.set('not-found');
    } else {
      this.errorMessage.set(ExceptionDetailPage.toMessage(error));
      this.state.set('error');
    }

    if (!this.isFirstLoad) {
      this.restoreFocus();
    }

    this.isFirstLoad = false;
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.resultHeading()?.nativeElement.focus());
  }

  /** Never crashes the page: falls back to the original string on any parse failure. */
  private static prettyPrint(raw: string): string {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  /**
   * 503 gets the exact copy required by docs/design/05-ai-ux.md -- calm,
   * explicit that the reconciliation result is unaffected. Every other
   * status surfaces the backend's own ProblemDetails `detail` (matching
   * the 400/404 messages GenerateAiExplanation actually returns) with a
   * generic fallback, consistent with `toMessage` below.
   */
  private static toAiErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 503) {
      return 'AI explanation unavailable. Reconciliation result is unaffected.';
    }

    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    const detail = isProblemDetails(error.error) ? error.error.detail : undefined;

    if (detail) {
      return detail;
    }

    return 'Could not generate an AI explanation. Please try again.';
  }

  private static toMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    const detail = isProblemDetails(error.error) ? error.error.detail : undefined;

    if (detail) {
      return detail;
    }

    if (error.status >= 500) {
      return 'The server could not complete the request. Please try again.';
    }

    return 'Could not load this exception. Please try again.';
  }
}
