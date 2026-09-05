import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import type { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReconciliationApi } from '../../../core/api/reconciliation-api.service';
import { isProblemDetails } from '../../../core/models/problem-details.model';
import type { GroundTruthComparisonResult } from '../../../core/models/reconciliation.model';
import {
  GROUND_TRUTH_COLUMNS,
  GroundTruthCsvError,
  parseGroundTruthCsv,
} from './ground-truth-csv';

type VerificationState = 'idle' | 'verifying' | 'complete' | 'error';

/** One expected-vs-actual row of the comparison table. */
interface ComparisonRow {
  readonly label: string;
  readonly expected: string;
  readonly actual: string;
  readonly agrees: boolean;
}

/**
 * Independent ground-truth verification.
 *
 * The operator supplies a ground-truth file; the backend compares it
 * against this run's persisted deterministic results and owns the entire
 * pass/fail decision. Nothing is compared, recomputed or second-guessed
 * here -- this page uploads a file and renders a verdict.
 *
 * Three things this page must never imply, because the endpoint does not
 * support them: that the verification was stored, that it has an id, or
 * that it happened at a recorded time. The comparison is stateless.
 *
 * It must also never describe the labels as self-generated proof. They are
 * produced independently of reconciliation (by FinSight.DataGenerator,
 * from the scenario plan, before any run exists) but they are still
 * supplied by whoever is standing at the keyboard, and the wording says so.
 */
@Component({
  selector: 'app-verification-page',
  imports: [RouterLink],
  templateUrl: './verification-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // Same containment fix as the other routed pages: without it the host
    // sizes to content instead of filling AppShell's <main>.
    class: 'flex-1 min-h-0 flex flex-col',
  },
})
export class VerificationPage {
  private readonly route = inject(ActivatedRoute);
  private readonly reconciliationApi = inject(ReconciliationApi);

  protected readonly runId =
    this.route.snapshot.paramMap.get('runId') ?? '';

  protected readonly expectedColumns = GROUND_TRUTH_COLUMNS;

  protected readonly state = signal<VerificationState>('idle');
  protected readonly fileName = signal<string | null>(null);
  protected readonly rowCount = signal(0);
  protected readonly fileError = signal<string | null>(null);
  protected readonly requestError = signal<string | null>(null);
  protected readonly result = signal<GroundTruthComparisonResult | null>(null);

  /** Parsed rows held in memory only; never persisted or logged. */
  private rows: import('../../../core/models/reconciliation.model').GroundTruthRow[] = [];

  protected readonly canVerify = computed(
    () =>
      this.rowCount() > 0 &&
      this.fileError() === null &&
      this.state() !== 'verifying',
  );

  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'verifying':
        return 'Comparing ground-truth labels against deterministic results.';
      case 'complete':
        return this.result()?.isSuccess
          ? 'Verification passed. Ground truth matches the reconciliation results.'
          : `Verification failed with ${this.result()?.failures.length ?? 0} discrepancies.`;
      case 'error':
        return this.requestError() ?? 'Verification could not be completed.';
      default:
        return '';
    }
  });

  /**
   * Expected vs actual, straight from the response. Every value is the
   * server's; `agrees` is only a display hint for the row, never a verdict
   * -- `isSuccess` is the verdict and it is the backend's alone.
   */
  protected readonly comparisonRows = computed<ComparisonRow[]>(() => {
    const r = this.result();

    if (r === null) {
      return [];
    }

    const row = (
      label: string,
      expected: number,
      actual: number,
      suffix = '',
    ): ComparisonRow => ({
      label,
      expected: `${expected}${suffix}`,
      actual: `${actual}${suffix}`,
      agrees: expected === actual,
    });

    return [
      row('Total units', r.expectedTotalUnits, r.actualTotalUnits),
      row('Matched', r.expectedMatched, r.actualMatched),
      row('Mismatched', r.expectedMismatched, r.actualMismatched),
      row('Missing', r.expectedMissing, r.actualMissing),
      row('Duplicate', r.expectedDuplicate, r.actualDuplicate),
      row('Unresolved', r.expectedUnresolved, r.actualUnresolved),
      row('Match rate', r.expectedMatchRate, r.actualMatchRate, '%'),
    ];
  });

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    this.clearResult();

    if (!file) {
      this.clearFile();
      return;
    }

    this.fileName.set(file.name);

    file
      .text()
      .then((text) => {
        try {
          this.rows = parseGroundTruthCsv(text);
          this.rowCount.set(this.rows.length);
          this.fileError.set(null);
        } catch (error) {
          this.rows = [];
          this.rowCount.set(0);
          this.fileError.set(
            error instanceof GroundTruthCsvError
              ? error.message
              : 'This file could not be read as a ground-truth CSV.',
          );
        }
      })
      .catch(() => {
        this.rows = [];
        this.rowCount.set(0);
        this.fileError.set('This file could not be read.');
      });

    // Allows re-selecting the same filename after a Remove.
    input.value = '';
  }

  protected clearFile(): void {
    this.rows = [];
    this.rowCount.set(0);
    this.fileName.set(null);
    this.fileError.set(null);
    this.clearResult();
  }

  protected verify(): void {
    if (!this.canVerify()) {
      return;
    }

    this.state.set('verifying');
    this.requestError.set(null);
    this.result.set(null);

    this.reconciliationApi.verifyGroundTruth(this.runId, this.rows).subscribe({
      next: (result) => {
        this.result.set(result);
        this.state.set('complete');
      },
      error: (error: HttpErrorResponse) => {
        this.requestError.set(VerificationPage.toMessage(error));
        this.state.set('error');
      },
    });
  }

  /** Retry after a transport/server failure, with the same parsed rows. */
  protected retry(): void {
    this.verify();
  }

  private clearResult(): void {
    this.result.set(null);
    this.requestError.set(null);

    if (this.state() !== 'idle') {
      this.state.set('idle');
    }
  }

  private static toMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    // ProblemDetails.detail is rendered as the backend wrote it -- never
    // parsed, and never replaced with a guess.
    const detail = isProblemDetails(error.error) ? error.error.detail : undefined;

    if (detail) {
      return detail;
    }

    if (error.status === 404) {
      return 'This reconciliation run no longer exists.';
    }

    if (error.status >= 500) {
      return 'The server could not complete the verification. Please try again.';
    }

    return 'Verification could not be completed. Please try again.';
  }
}
