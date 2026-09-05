import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import type { HttpErrorResponse } from '@angular/common/http';
import { ReconciliationApi } from '../../../core/api/reconciliation-api.service';
import { isProblemDetails } from '../../../core/models/problem-details.model';
import type { AuditEventType, AuditLogEntryResponse } from '../../../core/models/reconciliation.model';

const PAGE_SIZE = 10;

const EVENT_LABELS: Record<AuditEventType, string> = {
  BatchCreated: 'Batch created',
  BatchValidated: 'Batch validated',
  ReconciliationStarted: 'Reconciliation started',
  ReconciliationCompleted: 'Reconciliation completed',
  ReconciliationFailed: 'Reconciliation failed',
  ReconciliationDecisionRecorded: 'Reconciliation decision recorded',
  ExceptionCreated: 'Exception created',
  AiQuestionAsked: 'AI question asked',
  AiToolInvoked: 'AI tool invoked',
  AiExplanationRequested: 'AI explanation requested',
  AiExplanationFailed: 'AI explanation failed',
  AiAssistantFailed: 'AI assistant failed',
};

const FAILURE_EVENT_TYPES: ReadonlySet<AuditEventType> = new Set([
  'ReconciliationFailed',
  'AiExplanationFailed',
  'AiAssistantFailed',
]);

/**
 * Best-effort pretty-print of a raw JSON detail payload for display.
 * The backend never guarantees a fixed shape across event types, and this
 * viewer must never crash on data it cannot fully anticipate -- an
 * unparseable payload simply falls back to the raw string, unindented.
 */
function formatDetail(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

/**
 * Reads `duration_ms`/`records_per_second` out of a ReconciliationCompleted
 * event's detail payload, if present -- the exact same numbers
 * ReconciliationOrchestrator wrote into GET .../summary's `durationMs`/
 * `recordsPerSecond`. Never recomputed here, only read back. Returns null
 * for any other event type or if the payload doesn't parse or doesn't
 * carry these fields (an older or differently-shaped record).
 */
function extractThroughput(
  entry: AuditLogEntryResponse,
): { durationMs: number; recordsPerSecond: number } | null {
  if (entry.eventType !== 'ReconciliationCompleted') {
    return null;
  }

  try {
    const parsed = JSON.parse(entry.detail) as Record<string, unknown>;
    const durationMs = parsed['duration_ms'];
    const recordsPerSecond = parsed['records_per_second'];

    if (typeof durationMs === 'number' && typeof recordsPerSecond === 'number') {
      return { durationMs, recordsPerSecond };
    }
  } catch {
    // Falls through to null -- an unparseable payload is not a viewer bug.
  }

  return null;
}

interface AuditEntryView {
  readonly entry: AuditLogEntryResponse;
  readonly label: string;
  readonly isFailure: boolean;
  readonly formattedDetail: string;
  readonly throughput: { durationMs: number; recordsPerSecond: number } | null;
}

type PanelState = 'loading' | 'loaded' | 'error';

/**
 * Read-only audit evidence for one run, embedded in the Run Workspace.
 *
 * Every row comes verbatim from GET /api/reconciliation/runs/{runId}/audit,
 * which reads FinSight's existing audit_logs table -- the same store
 * BatchIngestionService, ReconciliationOrchestrator, AiExplanationService
 * and FinanceAssistantService already write to. There is no corresponding
 * write path anywhere in this API: this panel cannot create, edit, or
 * delete a single audit record, and none of its controls attempt to.
 *
 * This is evidence ABOUT the run's execution, never a second source of
 * financial truth -- match status, match rate, exception counts and
 * classification remain whatever the Reconciliation breakdown and Ground
 * Truth Verification report. See PART G in the P-1H implementation notes.
 */
@Component({
  selector: 'app-audit-evidence-panel',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './audit-evidence-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditEvidencePanel implements OnInit {
  private readonly reconciliationApi = inject(ReconciliationApi);

  readonly runId = input.required<string>();

  protected readonly state = signal<PanelState>('loading');
  protected readonly entries = signal<readonly AuditLogEntryResponse[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly pageNumber = signal(0);
  protected readonly loadingMore = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly isEmpty = computed(
    () => this.state() === 'loaded' && this.entries().length === 0,
  );

  protected readonly hasMore = computed(() => this.entries().length < this.totalCount());

  protected readonly views = computed<readonly AuditEntryView[]>(() =>
    this.entries().map((entry) => ({
      entry,
      label: EVENT_LABELS[entry.eventType] ?? entry.eventType,
      isFailure: FAILURE_EVENT_TYPES.has(entry.eventType),
      formattedDetail: formatDetail(entry.detail),
      throughput: extractThroughput(entry),
    })),
  );

  ngOnInit(): void {
    this.load(1);
  }

  protected retry(): void {
    this.load(1);
  }

  protected loadMore(): void {
    if (this.loadingMore() || !this.hasMore()) {
      return;
    }

    this.loadingMore.set(true);

    this.reconciliationApi.getAuditLog(this.runId(), this.pageNumber() + 1, PAGE_SIZE).subscribe({
      next: (page) => {
        this.entries.update((current) => [...current, ...page.items]);
        this.totalCount.set(page.totalCount);
        this.pageNumber.set(page.pageNumber);
        this.loadingMore.set(false);
      },
      error: () => {
        // Already-loaded entries stay visible; only the "load more"
        // affordance itself reports the failure, since the page's own
        // primary content loaded successfully.
        this.loadingMore.set(false);
      },
    });
  }

  private load(pageNumber: number): void {
    this.state.set('loading');
    this.errorMessage.set(null);

    this.reconciliationApi.getAuditLog(this.runId(), pageNumber, PAGE_SIZE).subscribe({
      next: (page) => {
        this.entries.set(page.items);
        this.totalCount.set(page.totalCount);
        this.pageNumber.set(page.pageNumber);
        this.state.set('loaded');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(AuditEvidencePanel.toMessage(error));
        this.state.set('error');
      },
    });
  }

  private static toMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Cannot reach the server. Check that the FinSight API is running and try again.';
    }

    const detail = isProblemDetails(error.error) ? error.error.detail : undefined;

    return detail ?? 'Could not load audit evidence for this run.';
  }
}
