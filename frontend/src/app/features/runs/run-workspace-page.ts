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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReconciliationApi } from '../../core/api/reconciliation-api.service';
import { BatchApi } from '../../core/api/batch-api.service';
import { isProblemDetails } from '../../core/models/problem-details.model';
import type { BatchResponse } from '../../core/models/batch.model';
import type {
  ReconciliationRunDetailsResponse,
  ReconciliationRunStatus,
} from '../../core/models/reconciliation.model';
import { FinanceAssistantPanel } from './finance-assistant/finance-assistant-panel';

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
  imports: [DatePipe, RouterLink, FinanceAssistantPanel],
  templateUrl: './run-workspace-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunWorkspacePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reconciliationApi = inject(ReconciliationApi);
  private readonly batchApi = inject(BatchApi);

  protected readonly state = signal<WorkspaceState>('loading');
  protected readonly run = signal<ReconciliationRunDetailsResponse | null>(null);
  protected readonly batch = signal<BatchResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly resultHeading = viewChild<ElementRef<HTMLElement>>('resultHeading');

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

    this.reconciliationApi.getRun(runId).subscribe({
      next: (run) => {
        this.run.set(run);
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
