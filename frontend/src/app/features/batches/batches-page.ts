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
import { RouterLink } from '@angular/router';
import { BatchApi } from '../../core/api/batch-api.service';
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
})
export class BatchesPage implements OnInit {
  private readonly batchApi = inject(BatchApi);

  protected readonly state = signal<BatchesPageState>('loading');
  protected readonly items = signal<BatchResponse[]>([]);
  protected readonly pageNumber = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly errorMessage = signal<string | null>(null);

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
}
