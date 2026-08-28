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
  ReconciliationResultResponse,
} from '../../../core/models/reconciliation.model';

const PAGE_SIZE = 50;

type ResultsPageState = 'loading' | 'loaded' | 'empty' | 'not-found' | 'error';

/**
 * Reconciliation results -- a verbatim, server-paginated rendering of
 * `GET /api/reconciliation/runs/{runId}/results`.
 *
 * No filter, no sort: the endpoint accepts only `pageNumber`/`pageSize`, so
 * neither exists here. No column header is clickable. Every value shown is
 * a direct field from the response -- status, reason code, and strategy are
 * rendered as the exact literal strings the backend returned, never
 * humanized, never recomputed.
 */
@Component({
  selector: 'app-results-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './results-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reconciliationApi = inject(ReconciliationApi);

  protected readonly runId = this.route.snapshot.paramMap.get('runId') ?? '';

  protected readonly state = signal<ResultsPageState>('loading');
  protected readonly items = signal<ReconciliationResultResponse[]>([]);
  protected readonly pageNumber = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly resultsHeading = viewChild<ElementRef<HTMLElement>>('resultsHeading');

  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'loading':
        return 'Loading results…';
      case 'loaded':
        return `Showing ${this.items().length} of ${this.totalCount()} results.`;
      case 'empty':
        return 'No results found.';
      case 'not-found':
        return 'Run not found.';
      case 'error':
        return this.errorMessage() ?? 'Could not load results.';
    }
  });

  ngOnInit(): void {
    if (!this.runId) {
      this.state.set('not-found');
      return;
    }

    this.load(1);
  }

  protected retry(): void {
    this.load(this.pageNumber());
  }

  protected firstPage(): void {
    if (this.pageNumber() !== 1) {
      this.load(1, true);
    }
  }

  protected previousPage(): void {
    if (this.pageNumber() > 1) {
      this.load(this.pageNumber() - 1, true);
    }
  }

  protected nextPage(): void {
    if (this.pageNumber() < this.totalPages()) {
      this.load(this.pageNumber() + 1, true);
    }
  }

  protected lastPage(): void {
    if (this.pageNumber() !== this.totalPages()) {
      this.load(this.totalPages(), true);
    }
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

  private load(pageNumber: number, restoreFocusAfter = false): void {
    this.state.set('loading');
    this.errorMessage.set(null);

    this.reconciliationApi.getResults(this.runId, pageNumber, PAGE_SIZE).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.pageNumber.set(response.pageNumber);
        this.totalPages.set(response.totalPages);
        this.totalCount.set(response.totalCount);
        this.state.set(response.items.length === 0 ? 'empty' : 'loaded');

        if (restoreFocusAfter) {
          this.restoreFocus();
        }
      },
      error: (error: HttpErrorResponse) => {
        this.pageNumber.set(pageNumber);

        if (error.status === 404) {
          this.state.set('not-found');
        } else {
          this.errorMessage.set(ResultsPage.toMessage(error));
          this.state.set('error');
        }

        if (restoreFocusAfter) {
          this.restoreFocus();
        }
      },
    });
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.resultsHeading()?.nativeElement.focus());
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

    return 'Could not load results. Please try again.';
  }
}
