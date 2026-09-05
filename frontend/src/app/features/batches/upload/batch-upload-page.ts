import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { BatchApi } from '../../../core/api/batch-api.service';
import { AuthStore } from '../../../core/state/auth-store';
import {
  extractValidationErrors,
  isProblemDetails,
  type IngestionValidationError,
} from '../../../core/models/problem-details.model';
import type { BatchIngestionResult } from '../../../core/models/batch.model';
import { FileSlot } from './file-slot';

type UploadState = 'idle' | 'submitting' | 'success' | 'error';

function groupBySource(
  errors: IngestionValidationError[],
): Record<string, IngestionValidationError[]> {
  const groups: Record<string, IngestionValidationError[]> = {};

  for (const error of errors) {
    (groups[error.source] ??= []).push(error);
  }

  return groups;
}

const MAX_ERRORS_SHOWN_PER_GROUP = 8;

/**
 * Real batch upload against `POST /api/batches`.
 *
 * Single screen, no wizard: the three file slots and the label are all
 * visible together, and the selected filename in each slot already serves
 * as the "review" step. `createdBy` is never a field the user fills in --
 * it comes straight from the authenticated session, because letting a
 * logged-in user type an arbitrary identity into a financial audit field
 * would be a real integrity problem, not a UX nicety.
 *
 * No file-content validity is ever claimed before the real response
 * arrives -- a file is only ever "selected", never "valid". The backend's
 * `POST /api/batches` can fail in two structurally different 400 shapes
 * (row-level `errors[]`, or a plain `detail` sentence for a structural CSV
 * problem like a missing column) -- both are handled here without ever
 * parsing one shape to reconstruct the other.
 */
@Component({
  selector: 'app-batch-upload-page',
  imports: [FileSlot, RouterLink],
  templateUrl: './batch-upload-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // See BatchesPage for the full rationale: without an explicit flex/
    // min-height binding, this component's host sizes to its content
    // instead of filling AppShell's <main>, which is what let the page
    // render with no vertical containment at all (zero padding, content
    // flush against the sidebar). The template's own root div owns the
    // scroll behavior (page-level, since a form is naturally document-flow);
    // this binding only establishes correct height participation.
    class: 'flex-1 min-h-0 flex flex-col',
  },
})
export class BatchUploadPage {
  private readonly batchApi = inject(BatchApi);
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly maxErrorsShownPerGroup = MAX_ERRORS_SHOWN_PER_GROUP;

  protected readonly batchLabel = signal('');
  protected readonly paymentsFile = signal<File | null>(null);
  protected readonly bankFile = signal<File | null>(null);
  protected readonly settlementsFile = signal<File | null>(null);

  protected readonly state = signal<UploadState>('idle');
  protected readonly result = signal<BatchIngestionResult | null>(null);
  protected readonly errorGroups = signal<Record<string, IngestionValidationError[]>>({});
  protected readonly errorDetail = signal<string | null>(null);
  protected readonly expandedSources = signal<ReadonlySet<string>>(new Set());

  private readonly resultHeading = viewChild<ElementRef<HTMLElement>>('resultHeading');

  protected readonly createdBy = computed(() => this.authStore.userEmail() ?? '');

  protected readonly canSubmit = computed(
    () =>
      this.batchLabel().trim().length > 0 &&
      this.paymentsFile() !== null &&
      this.bankFile() !== null &&
      this.settlementsFile() !== null &&
      this.state() !== 'submitting',
  );

  protected readonly hasStructuredErrors = computed(
    () => Object.keys(this.errorGroups()).length > 0,
  );

  protected readonly errorGroupEntries = computed(() => Object.entries(this.errorGroups()));

  protected readonly liveRegionText = computed(() => {
    switch (this.state()) {
      case 'submitting':
        return 'Uploading batch…';
      case 'success':
        return 'Batch uploaded successfully.';
      case 'error':
        return this.hasStructuredErrors()
          ? 'The batch could not be validated. See the errors below.'
          : (this.errorDetail() ?? 'Upload failed.');
      default:
        return '';
    }
  });

  protected onLabelInput(event: Event): void {
    this.batchLabel.set((event.target as HTMLInputElement).value);
  }

  protected isExpanded(source: string): boolean {
    return this.expandedSources().has(source);
  }

  protected visibleErrors(
    source: string,
    errors: IngestionValidationError[],
  ): IngestionValidationError[] {
    return this.isExpanded(source) ? errors : errors.slice(0, MAX_ERRORS_SHOWN_PER_GROUP);
  }

  protected expand(source: string): void {
    this.expandedSources.set(new Set([...this.expandedSources(), source]));
  }

  /**
   * Bound to the form's native `(submit)` event, not `(ngSubmit)` -- this
   * component uses neither `FormsModule` nor `ReactiveFormsModule` (a
   * single plain text field plus three custom file slots doesn't warrant
   * either), so no directive exists to supply `ngSubmit` and absorb the
   * browser's default action. Without an explicit `preventDefault()` here,
   * clicking the submit button would trigger a real native form submission
   * -- a full page reload -- instead of this handler.
   */
  protected onSubmit(event: Event): void {
    event.preventDefault();
    this.submit();
  }

  private submit(): void {
    if (!this.canSubmit()) {
      return;
    }

    this.state.set('submitting');
    this.errorGroups.set({});
    this.errorDetail.set(null);

    this.batchApi
      .createBatch(
        this.batchLabel().trim(),
        this.createdBy(),
        this.paymentsFile()!,
        this.bankFile()!,
        this.settlementsFile()!,
      )
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.state.set('success');
          this.restoreFocus();
        },
        error: (error: HttpErrorResponse) => {
          this.applyError(error);
          this.state.set('error');
          this.restoreFocus();
        },
      });
  }

  protected reset(): void {
    this.batchLabel.set('');
    this.paymentsFile.set(null);
    this.bankFile.set(null);
    this.settlementsFile.set(null);
    this.result.set(null);
    this.errorGroups.set({});
    this.errorDetail.set(null);
    this.expandedSources.set(new Set());
    this.state.set('idle');
  }

  protected goToHistory(): void {
    void this.router.navigateByUrl('/batches');
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.resultHeading()?.nativeElement.focus());
  }

  /**
   * Applies a failed response to `errorGroups`/`errorDetail`, never both.
   *
   * Shape A (structured `errors[]`) and Shape B (`detail` only) are kept
   * strictly separate: `errors[]` is rendered grouped by source exactly as
   * the backend sent it, and `detail` is rendered as one plain sentence --
   * neither is ever parsed to reconstruct the other.
   */
  private applyError(error: HttpErrorResponse): void {
    if (isProblemDetails(error.error)) {
      const errors = extractValidationErrors(error.error);

      if (errors.length > 0) {
        this.errorGroups.set(groupBySource(errors));
        this.errorDetail.set(null);
        return;
      }

      this.errorGroups.set({});
      this.errorDetail.set(error.error.detail ?? BatchUploadPage.genericMessage(error));
      return;
    }

    this.errorGroups.set({});
    this.errorDetail.set(BatchUploadPage.genericMessage(error));
  }

  private static genericMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    if (error.status === 413) {
      return 'One or more files are too large for the server to accept.';
    }

    if (error.status >= 500) {
      return 'The server could not complete the request. Please try again.';
    }

    return 'Upload failed. Please try again.';
  }
}
