import { DEFAULT_POST_LOGIN_ROUTE, safeReturnUrl } from './return-url';

const BACKSLASH = String.fromCharCode(92);

describe('safeReturnUrl', () => {
  it('accepts root-relative application paths', () => {
    expect(safeReturnUrl('/batches')).toBe('/batches');
    expect(safeReturnUrl('/runs/abc/exceptions')).toBe('/runs/abc/exceptions');
    expect(safeReturnUrl('/batches?pageNumber=2')).toBe('/batches?pageNumber=2');
  });

  it('falls back when nothing usable is supplied', () => {
    expect(safeReturnUrl(null)).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl(undefined)).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl('')).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl('   ')).toBe(DEFAULT_POST_LOGIN_ROUTE);
  });

  it('rejects absolute URLs (open redirect)', () => {
    expect(safeReturnUrl('https://evil.test')).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl('http://evil.test/batches')).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl('javascript:alert(1)')).toBe(DEFAULT_POST_LOGIN_ROUTE);
  });

  it('rejects protocol-relative authority forms', () => {
    expect(safeReturnUrl('//evil.test')).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl('//evil.test/batches')).toBe(DEFAULT_POST_LOGIN_ROUTE);
  });

  it('rejects backslash-smuggled authority forms', () => {
    expect(safeReturnUrl('/' + BACKSLASH + 'evil.test')).toBe(
      DEFAULT_POST_LOGIN_ROUTE,
    );
    expect(safeReturnUrl('/' + BACKSLASH + BACKSLASH + 'evil.test')).toBe(
      DEFAULT_POST_LOGIN_ROUTE,
    );
  });

  it('rejects paths containing control characters', () => {
    expect(safeReturnUrl('/batches' + String.fromCharCode(10) + 'x')).toBe(
      DEFAULT_POST_LOGIN_ROUTE,
    );
    expect(safeReturnUrl('/batches' + String.fromCharCode(0))).toBe(
      DEFAULT_POST_LOGIN_ROUTE,
    );
  });

  it('rejects relative paths that are not root-relative', () => {
    expect(safeReturnUrl('batches')).toBe(DEFAULT_POST_LOGIN_ROUTE);
    expect(safeReturnUrl('../admin')).toBe(DEFAULT_POST_LOGIN_ROUTE);
  });
});
