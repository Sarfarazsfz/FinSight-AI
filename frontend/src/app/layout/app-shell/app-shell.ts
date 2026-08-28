import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/state/auth-store';

interface NavItem {
  readonly label: string;
  readonly link: string;
}

/**
 * The authenticated operations workspace.
 *
 * Navigation lists only destinations that exist. Run-scoped areas
 * (reconciliation, exceptions, assistant, verification) are omitted
 * entirely rather than rendered disabled -- there is no run and no data
 * behind them yet, and a greyed-out item is a dead item.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppShell {
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly userEmail = this.authStore.userEmail;

  protected readonly navItems: readonly NavItem[] = [
    { label: 'Batches', link: '/batches' },
  ];

  /**
   * There is no server logout endpoint, so clearing the local session is
   * the whole of signing out.
   */
  protected logout(): void {
    this.authStore.clearSession();
    void this.router.navigateByUrl('/login');
  }
}
