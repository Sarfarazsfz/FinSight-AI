/**
 * Mirrors FinSight.Application.DTOs.Reconciliation.PagedResponse<T>.
 *
 * Pagination is 1-based and the backend caps `pageSize` at 100.
 * `totalPages` is 0 when `totalCount` is 0.
 */
export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
