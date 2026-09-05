import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/state/auth-store';

interface NavItem {
  readonly label: string;
  readonly link: string;
  /** Identifies which SVG icon to render in the sidebar/mobile nav. */
  readonly icon: 'batches' | 'data-generator';
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
    { label: 'Batches',        link: '/batches',        icon: 'batches'        },
    { label: 'Synthetic Data', link: '/data-generator', icon: 'data-generator' },
  ];

  /**
   * Desktop (>= lg) sidebar collapse state only -- the mobile top bar/tab
   * strip is a completely separate, unaffected layout (see template).
   * Deliberately in-memory only, not persisted: this is a page-session
   * display preference, not an authentication or account setting, and
   * the existing app has no established UI-preference storage mechanism
   * to extend. Defaults to expanded.
   */
  protected readonly isSidebarCollapsed = signal(false);

  protected toggleSidebar(): void {
    this.isSidebarCollapsed.update((collapsed) => !collapsed);
  }

  protected readonly isLogoutDialogOpen = signal(false);

  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancelButton');
  private readonly confirmButton = viewChild<ElementRef<HTMLButtonElement>>('confirmButton');
  private triggerElement: HTMLElement | null = null;

  protected openLogoutDialog(event: Event): void {
    this.triggerElement = event.currentTarget as HTMLElement;
    this.isLogoutDialogOpen.set(true);
    setTimeout(() => {
      this.cancelButton()?.nativeElement.focus();
    });
  }

  protected cancelLogout(): void {
    this.isLogoutDialogOpen.set(false);
    this.triggerElement?.focus();
    this.triggerElement = null;
  }

  protected confirmLogout(): void {
    this.isLogoutDialogOpen.set(false);
    this.authStore.clearSession();
    void this.router.navigateByUrl('/login');
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.cancelLogout();
    }
  }

  protected onDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelLogout();
      return;
    }

    if (event.key === 'Tab') {
      const cancelEl = this.cancelButton()?.nativeElement;
      const confirmEl = this.confirmButton()?.nativeElement;

      if (!cancelEl || !confirmEl) {
        return;
      }

      if (event.shiftKey && document.activeElement === cancelEl) {
        event.preventDefault();
        confirmEl.focus();
      } else if (!event.shiftKey && document.activeElement === confirmEl) {
        event.preventDefault();
        cancelEl.focus();
      }
    }
  }
}
