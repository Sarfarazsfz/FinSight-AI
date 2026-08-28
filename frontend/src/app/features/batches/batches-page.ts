import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Entry point for the batch workflow.
 *
 * This page deliberately fetches nothing. Batch history integration has its
 * own phase; until then the page states honestly that there is nothing to
 * show rather than rendering a skeleton (which would imply a request is in
 * flight) or placeholder rows and counts (which would be fabricated data in
 * a financial tool).
 */
@Component({
  selector: 'app-batches-page',
  templateUrl: './batches-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BatchesPage {}
