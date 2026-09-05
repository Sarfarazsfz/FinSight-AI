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
import { Router, RouterLink } from '@angular/router';
import { BatchApi } from '../../core/api/batch-api.service';
import { ReconciliationApi } from '../../core/api/reconciliation-api.service';
import { isProblemDetails } from '../../core/models/problem-details.model';
import type {
  BatchResponse,
  BatchValidationStatus,
} from '../../core/models/batch.model';

const PAGE_SIZE = 20;

type BatchesPageState = 'loading' | 'loaded' | 'empty' | 'error';

/**
 * Batch history — the first screen in the product that renders real,
 * persisted business data from the backend.
 *
 * Pagination is entirely server-authoritative: `pageNumber`, `totalPages`
 * and `totalCount` are read from the response and only from the response.
 * This component never fetches "all" batches, never recomputes
 * `totalPages`, and never sorts or filters what the server returned — it is
 * a verbatim rendering of one page of `GET /api/batches`.
 *
 * 401 is deliberately not handled here. The global `errorInterceptor`
 * already clears the session and redirects to `/login` whenever a 401
 * arrives while a session exists; duplicating that here would risk the two
 * copies drifting out of sync.
 */
@Component({
  selector: 'app-batches-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './batches-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // Angular renders this component's template inside its own host
    // element (<app-batches-page>), sitting directly inside AppShell's
    // <main class="... lg:h-screen lg:overflow-hidden flex flex-col">.
    // Without this, the host has no flex-grow of its own (default
    // flex: 0 1 auto) and no min-height override (default min-height:
    // auto), so it sizes to its content's natural height instead of
    // filling <main>'s already-definite viewport height -- which is
    // exactly what let a long batch table grow the whole document
    // instead of scrolling inside its own contained viewport. Same fix
    // already applied to RunWorkspacePage for the identical reason.
    class: 'flex-1 min-h-0 flex flex-col',
  },
})
export class BatchesPage implements OnInit {
  private readonly batchApi = inject(BatchApi);
  private readonly reconciliationApi = inject(ReconciliationApi);
  private readonly router = inject(Router);

  protected readonly state = signal<BatchesPageState>('loading');
  protected readonly items = signal<BatchResponse[]>([]);
  protected readonly pageNumber = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly errorMessage = signal<string | null>(null);

  /** The batchId currently being turned into a run, or null when idle. Only
   *  one run creation is allowed in flight at a time -- every row's action
   *  disables while this is set, avoiding an ambiguous concurrent request. */
  protected readonly creatingRunForBatchId = signal<string | null>(null);
  protected readonly runCreationError = signal<string | null>(null);

  private readonly resultsHeading =
    viewChild<ElementRef<HTMLElement>>('resultsHeading');

  /** Announced to screen readers whenever it changes; decorative for sighted users. */
  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'loading':
        return 'Loading batches…';
      case 'loaded':
        return `Showing ${this.items().length} of ${this.totalCount()} batches.`;
      case 'empty':
        return 'No batches found.';
      case 'error':
        return this.errorMessage() ?? 'Could not load batches.';
    }
  });

  ngOnInit(): void {
    this.load(1);
  }

  protected retry(): void {
    this.load(this.pageNumber());
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

  protected statusBadgeClasses(status: BatchValidationStatus): string {
    return status === 'Valid'
      ? 'bg-matched-bg text-matched'
      : 'bg-danger-bg text-danger';
  }

  /**
   * Starts a real reconciliation run for one batch and navigates to its
   * real Workspace on success.
   *
   * The 201 response (`ReconciliationRunResult`) is read only for `runId` --
   * its `status` is the raw backend enum and serializes as a number, unlike
   * the string status the Workspace itself renders from a fresh GET. Never
   * rendering that number here (or anywhere) sidesteps the asymmetry
   * entirely rather than normalizing it.
   */
  protected createRun(batch: BatchResponse): void {
    if (this.creatingRunForBatchId() !== null) {
      return;
    }

    this.creatingRunForBatchId.set(batch.batchId);
    this.runCreationError.set(null);

    this.reconciliationApi.createRun(batch.batchId).subscribe({
      next: (result) => {
        this.creatingRunForBatchId.set(null);
        void this.router.navigateByUrl(`/runs/${result.runId}`);
      },
      error: (error: HttpErrorResponse) => {
        this.creatingRunForBatchId.set(null);
        this.runCreationError.set(BatchesPage.toRunCreationMessage(error));
      },
    });
  }

  protected dismissRunCreationError(): void {
    this.runCreationError.set(null);
  }

  private load(pageNumber: number, restoreFocusAfter = false): void {
    this.state.set('loading');
    this.errorMessage.set(null);

    this.batchApi.getBatches(pageNumber, PAGE_SIZE).subscribe({
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
        this.errorMessage.set(BatchesPage.toMessage(error));
        this.state.set('error');

        if (restoreFocusAfter) {
          this.restoreFocus();
        }
      },
    });
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.resultsHeading()?.nativeElement.focus());
  }

  /**
   * Turns a failed batches request into something a person can act on.
   *
   * `ProblemDetails.detail` is rendered as-is, exactly like LoginPage —
   * never parsed to reconstruct structured data. `errors[]` does not apply
   * to this endpoint (it is a `POST /api/batches` upload-validation concern
   * only), so it is not read here.
   */
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

    return 'Could not load batches. Please try again.';
  }

  private static toRunCreationMessage(error: HttpErrorResponse): string {
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

    return 'Could not start reconciliation. Please try again.';
  }
}
