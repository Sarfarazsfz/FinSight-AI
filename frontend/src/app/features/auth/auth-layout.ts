import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Shared chrome for every authentication screen (sign in, sign up, forgot
 * password, reset password).
 *
 * Extracted so the four pages cannot drift into four slightly different
 * authentication designs -- the header, ambient background, card geometry,
 * trust footnote and footer are defined exactly once here, and each page
 * projects only its own form into the card.
 */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterLink],
  templateUrl: './auth-layout.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthLayout {
  readonly heading = input.required<string>();
  readonly subheading = input.required<string>();
}
