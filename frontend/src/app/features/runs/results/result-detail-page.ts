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
import { ReconciliationApi } from '../../../core/api/reconciliation-api.service';
import { isProblemDetails } from '../../../core/models/problem-details.model';
import type {
  ReconciliationMatchStatus,
  ReconciliationTransactionDetailResponse,
} from '../../../core/models/reconciliation.model';

type DetailState = 'loading' | 'loaded' | 'not-found' | 'error';

/**
 * Source evidence for one reconciliation result -- a verbatim rendering of
 * `GET /api/reconciliation/runs/{runId}/results/{resultId}`.
 *
 * `payments`/`banks`/`settlements` are real variable-length arrays, not a
 * fixed one-per-source triple: an empty array is rendered as an honest
 * "no matching record" statement (that IS how a Missing result looks),
 * and every item in an array is rendered when there is more than one (that
 * IS how a Duplicate result looks). Nothing is collapsed, summarized, or
 * inferred beyond what the response actually contains.
 *
 * This is a plain routed page, not a drawer/overlay -- deliberately, so
 * that no new dependency (Angular CDK) is needed for focus management this
 * phase. A drawer-based evidence experience remains a distinct, later
 * decision.
 */
@Component({
  selector: 'app-result-detail-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './result-detail-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reconciliationApi = inject(ReconciliationApi);

  protected readonly runId = this.route.snapshot.paramMap.get('runId') ?? '';
  protected readonly resultId = this.route.snapshot.paramMap.get('resultId') ?? '';

  protected readonly state = signal<DetailState>('loading');
  protected readonly detail = signal<ReconciliationTransactionDetailResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly resultHeading = viewChild<ElementRef<HTMLElement>>('resultHeading');

  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'loading':
        return 'Loading evidence…';
      case 'loaded':
        return `Evidence loaded for ${this.detail()?.transactionReference ?? ''}.`;
      case 'not-found':
        return 'Result not found.';
      case 'error':
        return this.errorMessage() ?? 'Could not load evidence.';
    }
  });

  ngOnInit(): void {
    if (!this.runId || !this.resultId) {
      this.state.set('not-found');
      return;
    }

    this.load();
  }

  protected retry(): void {
    this.load();
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

  private load(): void {
    this.state.set('loading');
    this.errorMessage.set(null);

    this.reconciliationApi.getResultDetail(this.runId, this.resultId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.state.set('loaded');
        this.restoreFocus();
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.state.set('not-found');
        } else {
          this.errorMessage.set(ResultDetailPage.toMessage(error));
          this.state.set('error');
        }

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

    return 'Could not load evidence. Please try again.';
  }
}
