/**
 * Default destination after a successful login when no usable returnUrl
 * was supplied.
 */
export const DEFAULT_POST_LOGIN_ROUTE = '/batches';

const BACKSLASH = String.fromCharCode(92);

/** True when the string contains a C0 control character or DEL. */
function hasControlCharacter(value: string): boolean {
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);

    if (code <= 31 || code === 127) {
      return true;
    }
  }

  return false;
}

/**
 * Validates a `returnUrl` query parameter before navigating to it.
 *
 * An unvalidated returnUrl is an open redirect: an attacker sends a victim
 * to `/login?returnUrl=https://evil.test`, the victim authenticates, and the
 * application forwards them off-site with the login flow appearing entirely
 * legitimate. Only same-site, root-relative paths are accepted.
 *
 * Rejected: absolute URLs, protocol-relative `//host` paths, backslash
 * variants that some browsers normalise to `//`, control characters, and
 * anything not starting with a single `/`.
 */
export function safeReturnUrl(candidate: string | null | undefined): string {
  if (!candidate) {
    return DEFAULT_POST_LOGIN_ROUTE;
  }

  const value = candidate.trim();

  if (value.length === 0) {
    return DEFAULT_POST_LOGIN_ROUTE;
  }

  // Must be root-relative.
  if (!value.startsWith('/')) {
    return DEFAULT_POST_LOGIN_ROUTE;
  }

  // Reject protocol-relative authority forms: "//evil.test".
  if (value.startsWith('//')) {
    return DEFAULT_POST_LOGIN_ROUTE;
  }

  // Reject backslash-smuggled authority forms: "/\evil.test".
  if (value.charAt(1) === BACKSLASH) {
    return DEFAULT_POST_LOGIN_ROUTE;
  }

  if (hasControlCharacter(value)) {
    return DEFAULT_POST_LOGIN_ROUTE;
  }

  return value;
}
