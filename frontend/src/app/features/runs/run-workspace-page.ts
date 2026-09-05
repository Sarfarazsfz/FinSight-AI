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
import { DatePipe, DecimalPipe } from '@angular/common';
import type { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReconciliationApi } from '../../core/api/reconciliation-api.service';
import { BatchApi } from '../../core/api/batch-api.service';
import { isProblemDetails } from '../../core/models/problem-details.model';
import type { BatchResponse } from '../../core/models/batch.model';
import type {
  ReconciliationRunDetailsResponse,
  ReconciliationRunStatus,
  ReconciliationRunSummaryResponse,
} from '../../core/models/reconciliation.model';
import { FinanceAssistantPanel } from './finance-assistant/finance-assistant-panel';
import { AuditEvidencePanel } from './audit/audit-evidence-panel';

type WorkspaceState = 'loading' | 'loaded' | 'not-found' | 'error';

/**
 * Run Workspace -- the minimum foundation for "what run am I looking at?".
 *
 * Deliberately not a dashboard: no KPI tiles, no charts, no fabricated
 * numbers. Every value rendered here comes verbatim from
 * GET /api/reconciliation/runs/{runId} and, for the human batch label only,
 * GET /api/batches/{batchId}. The backend remains the sole source of
 * financial truth -- this component computes nothing.
 *
 * No polling. `ExecuteAsync` on the backend runs a reconciliation fully
 * synchronously before `POST /api/reconciliation/runs` ever returns, so
 * there is no "processing" window for this page to observe. A `Running`/
 * `Pending` row here would only ever be a crash artifact, not a normal
 * transient state -- it is rendered honestly, with a manual Refresh, never
 * papered over with an automatic poll loop.
 */
@Component({
  selector: 'app-run-workspace-page',
  imports: [DatePipe, DecimalPipe, RouterLink, FinanceAssistantPanel, AuditEvidencePanel],
  templateUrl: './run-workspace-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // Angular renders this component's template inside its own host
    // element (<app-run-workspace-page>), sitting directly inside
    // AppShell's <main class="... lg:h-screen lg:overflow-hidden flex
    // flex-col">. Without this, the host element has no flex-grow of its
    // own (default flex: 0 1 auto) and no min-height override (default
    // min-height: auto), so it sizes to its content's natural height
    // instead of filling <main>'s already-definite viewport height --
    // breaking the entire nested h-full/overflow-hidden containment
    // chain down to the Finance Assistant's conversation area (which
    // already carries the equivalent fix -- see FinanceAssistantPanel's
    // own `host` binding). The practical symptom: the whole page scrolls
    // instead of only the chat conversation scrolling internally.
    class: 'flex-1 min-h-0 flex flex-col',
  },
})
export class RunWorkspacePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reconciliationApi = inject(ReconciliationApi);
  private readonly batchApi = inject(BatchApi);

  protected readonly state = signal<WorkspaceState>('loading');
  protected readonly run = signal<ReconciliationRunDetailsResponse | null>(null);
  protected readonly batch = signal<BatchResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isMobileAssistantOpen = signal(false);

  /**
   * The desktop rail's own open/close state -- independent of the mobile
   * drawer above. Closing only hides the rail (see the template's class
   * toggle): `app-finance-assistant-panel` stays mounted underneath, so
   * its conversation, draft input, and loading state are never destroyed
   * and reopening shows exactly what was there before.
   */
  protected readonly isDesktopAssistantOpen = signal(true);

  /**
   * Whole-run status totals from GET .../summary -- the sole authoritative
   * source of the five counts. Null until loaded, or if the summary call
   * fails; the breakdown then renders an honest unavailable state rather
   * than counting a page of results, which would be wrong for any run
   * larger than one page.
   */
  protected readonly summary =
    signal<ReconciliationRunSummaryResponse | null>(null);

  protected readonly summaryFailed = signal(false);

  /**
   * Rendered in declaration order, which is the review order a reviewer
   * reads: clean matches first, then the exceptions in decreasing
   * obviousness. Each entry maps to exactly one MatchStatus -- Missing,
   * Duplicate and Unresolved are never collapsed together, because they
   * are genuinely different failures with different remediations.
   */
  protected readonly breakdown = computed(() => {
    const summary = this.summary();

    if (summary === null) {
      return [];
    }

    return [
      {
        key: 'matched',
        label: 'Matched',
        count: summary.matched,
        meaning: 'Reconciled across all sources',
        dotClass: 'bg-[var(--lp-teal)]',
        countClass: 'text-[var(--lp-teal)]',
      },
      {
        key: 'mismatched',
        label: 'Mismatched',
        count: summary.mismatched,
        meaning: 'Amount or date disagreement',
        dotClass: 'bg-[var(--lp-danger)]',
        countClass: 'text-[var(--lp-danger)]',
      },
      {
        key: 'missing',
        label: 'Missing',
        count: summary.missing,
        meaning: 'Absent from at least one source',
        dotClass: 'bg-[var(--lp-accent)]',
        countClass: 'text-[var(--lp-accent)]',
      },
      {
        key: 'duplicate',
        label: 'Duplicate',
        count: summary.duplicate,
        meaning: 'Repeated record in a source',
        dotClass: 'bg-[#2A5CAA]',
        countClass: 'text-[#2A5CAA]',
      },
      {
        key: 'unresolved',
        label: 'Unresolved',
        count: summary.unresolved,
        meaning: 'Needs manual investigation',
        dotClass: 'bg-[#62605B]',
        countClass: 'text-[var(--lp-text-muted)]',
      },
    ] as const;
  });

  /**
   * True when the five counts sum to the reported total.
   *
   * The backend derives both from the same unpaginated result set, so this
   * holds by construction -- it is surfaced as an explicit, checkable
   * statement rather than an assumption, because "the counts add up" is
   * precisely the claim that makes the exception list credible.
   */
  protected readonly countsReconcile = computed(() => {
    const summary = this.summary();

    if (summary === null) {
      return false;
    }

    return (
      summary.matched +
        summary.mismatched +
        summary.missing +
        summary.duplicate +
        summary.unresolved ===
      summary.totalUnits
    );
  });

  private readonly resultHeading = viewChild<ElementRef<HTMLElement>>('resultHeading');
  private readonly assistantMobileTrigger = viewChild<ElementRef<HTMLElement>>('assistantMobileTrigger');
  private readonly assistantDesktopTrigger = viewChild<ElementRef<HTMLElement>>('assistantDesktopTrigger');

  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'loading':
        return 'Loading run…';
      case 'loaded':
        return `Run status: ${this.run()?.status ?? ''}.`;
      case 'not-found':
        return 'Run not found.';
      case 'error':
        return this.errorMessage() ?? 'Could not load this run.';
    }
  });

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected openMobileAssistant(): void {
    this.isMobileAssistantOpen.set(true);
  }

  protected closeMobileAssistant(): void {
    this.isMobileAssistantOpen.set(false);
    queueMicrotask(() => this.assistantMobileTrigger()?.nativeElement.focus());
  }

  protected openDesktopAssistant(): void {
    this.isDesktopAssistantOpen.set(true);
  }

  protected closeDesktopAssistant(): void {
    this.isDesktopAssistantOpen.set(false);
    queueMicrotask(() => this.assistantDesktopTrigger()?.nativeElement.focus());
  }

  protected statusBadgeClasses(status: ReconciliationRunStatus): string {
    switch (status) {
      case 'Completed':
        return 'bg-matched-bg text-matched';
      case 'Failed':
        return 'bg-danger-bg text-danger';
      case 'Running':
      case 'Pending':
        return 'bg-pending-bg text-pending';
    }
  }

  private load(): void {
    const runId = this.route.snapshot.paramMap.get('runId');

    if (!runId) {
      this.state.set('not-found');
      return;
    }

    this.state.set('loading');
    this.errorMessage.set(null);
    this.run.set(null);
    this.batch.set(null);
    this.summary.set(null);
    this.summaryFailed.set(false);

    this.reconciliationApi.getRun(runId).subscribe({
      next: (run) => {
        this.run.set(run);
        this.fetchSummary(run.runId);
        this.fetchBatchLabel(run.batchId);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.state.set('not-found');
        } else {
          this.errorMessage.set(RunWorkspacePage.toMessage(error));
          this.state.set('error');
        }

        this.restoreFocus();
      },
    });
  }

  /**
   * The run itself is real and already fetched -- a failure here (the batch
   * record is gone, a network blip) doesn't invalidate that. Rather than
   * failing the whole page over a secondary lookup, the page still loads
   * and falls back to the real batch id as its own label, which is honest
   * (it's a real identifier, not an invented name), just less readable.
   */
  /**
   * Secondary, like the batch label: the run itself is already loaded and
   * valid, so a summary failure degrades the breakdown section rather than
   * failing the whole page. It never falls back to counting results --
   * a wrong total would be worse than an absent one.
   */
  private fetchSummary(runId: string): void {
    this.reconciliationApi.getSummary(runId).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.summaryFailed.set(false);
      },
      error: () => {
        this.summary.set(null);
        this.summaryFailed.set(true);
      },
    });
  }

  private fetchBatchLabel(batchId: string): void {
    this.batchApi.getBatch(batchId).subscribe({
      next: (batch) => {
        this.batch.set(batch);
        this.state.set('loaded');
        this.restoreFocus();
      },
      error: () => {
        this.batch.set(null);
        this.state.set('loaded');
        this.restoreFocus();
      },
    });
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.resultHeading()?.nativeElement.focus());
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

    return 'Could not load this run. Please try again.';
  }
}
