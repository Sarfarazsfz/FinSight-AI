import {
  extractValidationErrors,
  isProblemDetails,
  type ProblemDetails,
} from './problem-details.model';

describe('ProblemDetails', () => {
  describe('isProblemDetails', () => {
    it('accepts a real backend ProblemDetails body', () => {
      const body: ProblemDetails = {
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.1',
        title: 'Unauthorized',
        status: 401,
        detail: 'Invalid email or password.',
      };

      expect(isProblemDetails(body)).toBeTrue();
    });

    it('accepts a body carrying only one of the marker fields', () => {
      expect(isProblemDetails({ status: 500 })).toBeTrue();
      expect(isProblemDetails({ title: 'Bad Request' })).toBeTrue();
      expect(isProblemDetails({ detail: 'something' })).toBeTrue();
    });

    it('rejects values that are not ProblemDetails', () => {
      expect(isProblemDetails(null)).toBeFalse();
      expect(isProblemDetails(undefined)).toBeFalse();
      expect(isProblemDetails('Internal Server Error')).toBeFalse();
      expect(isProblemDetails(42)).toBeFalse();
      expect(isProblemDetails([])).toBeFalse();
      expect(isProblemDetails({ someOtherField: true })).toBeFalse();
    });
  });

  describe('extractValidationErrors', () => {
    it('returns the structured errors array unchanged', () => {
      const body: ProblemDetails = {
        title: 'Bad Request',
        status: 400,
        detail:
          'Batch validation failed:\nPayment row 2: payment_record_id - Required value is missing.',
        errors: [
          {
            source: 'Payment',
            rowNumber: 2,
            field: 'payment_record_id',
            message: 'Required value is missing.',
          },
        ],
      };

      const errors = extractValidationErrors(body);

      expect(errors.length).toBe(1);
      expect(errors[0].source).toBe('Payment');
      expect(errors[0].rowNumber).toBe(2);
      expect(errors[0].field).toBe('payment_record_id');
      expect(errors[0].message).toBe('Required value is missing.');
    });

    it('preserves a null rowNumber rather than coercing it', () => {
      const errors = extractValidationErrors({
        status: 400,
        errors: [
          { source: 'Bank', rowNumber: null, field: 'header', message: 'Missing column.' },
        ],
      } satisfies ProblemDetails);

      expect(errors[0].rowNumber).toBeNull();
    });

    it('NEVER parses `detail` to reconstruct errors', () => {
      // A 400 that carries a detail string but no errors[] must yield an
      // empty array. Reconstructing fields by parsing prose would be a
      // silent contract violation.
      const body: ProblemDetails = {
        title: 'Bad Request',
        status: 400,
        detail:
          'Batch validation failed:\nPayment row 2: payment_record_id - Required value is missing.',
      };

      expect(extractValidationErrors(body)).toEqual([]);
    });

    it('returns an empty array for non-ProblemDetails input', () => {
      expect(extractValidationErrors(null)).toEqual([]);
      expect(extractValidationErrors('boom')).toEqual([]);
    });
  });
});
