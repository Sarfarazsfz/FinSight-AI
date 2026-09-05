import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  CorruptionIntensity,
  DataGeneratorApiService,
  GeneratedDatasetMetadata,
  GenerationMode,
} from '../../core/api/data-generator-api.service';

interface ModeOption {
  label: string;
  value: GenerationMode;
  description: string;
}

interface IntensityOption {
  label: string;
  value: CorruptionIntensity;
}

type DownloadFile = 'payments' | 'bank' | 'settlements' | 'ground-truth';

@Component({
  selector: 'app-data-generator-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './data-generator-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // Angular renders this component's template inside its own host
    // element (<app-data-generator-page>), sitting directly inside
    // AppShell's <main class="... lg:h-screen lg:overflow-hidden flex flex-col">.
    // Without this, the host has no flex-grow and no min-height override,
    // so h-full on the template's outer div cannot resolve to the viewport
    // height — the page grows to content height and the inner overflow-y-auto
    // container never activates. Same pattern as BatchesPage and RunWorkspacePage.
    class: 'flex-1 min-h-0 flex flex-col',
  },
})
export class DataGeneratorPage {
  private readonly api = inject(DataGeneratorApiService);

  // -------------------------------------------------------------------------
  // Form state
  // -------------------------------------------------------------------------

  protected readonly sizes = [50, 100, 250, 500] as const;

  protected readonly modeOptions: readonly ModeOption[] = [
    {
      label: 'Mixed',
      value: GenerationMode.Mixed,
      description: 'Proportional mix of all corruption types',
    },
    {
      label: 'Random Chaos',
      value: GenerationMode.RandomChaos,
      description: '3–5 randomly selected corruption operators',
    },
    {
      label: 'Clean',
      value: GenerationMode.Clean,
      description: 'All records reconcile — 100 % match rate',
    },
    {
      label: 'Amount Mismatch',
      value: GenerationMode.AmountMismatch,
      description: 'Bank/settlement amount differs from payment',
    },
    {
      label: 'Date Mismatch',
      value: GenerationMode.DateMismatch,
      description: 'Bank/settlement date beyond tolerance window',
    },
    {
      label: 'Missing Bank',
      value: GenerationMode.MissingBank,
      description: 'Bank record absent for some transactions',
    },
    {
      label: 'Missing Settlement',
      value: GenerationMode.MissingSettlement,
      description: 'Settlement record absent for some transactions',
    },
    {
      label: 'Missing Payment',
      value: GenerationMode.MissingPayment,
      description: 'Payment absent — orphan bank + settlement',
    },
    {
      label: 'Duplicate',
      value: GenerationMode.Duplicate,
      description: 'Duplicate payment, bank, or settlement records',
    },
    {
      label: 'Unresolved',
      value: GenerationMode.Unresolved,
      description: 'REVERSED_FRAUD bank status triggers Unresolved',
    },
  ];

  protected readonly intensityOptions: readonly IntensityOption[] = [
    { label: 'Low (~10 %)',    value: CorruptionIntensity.Low    },
    { label: 'Medium (~20 %)', value: CorruptionIntensity.Medium },
    { label: 'High (~30 %)',   value: CorruptionIntensity.High   },
  ];

  protected selectedSize      = 100;
  protected selectedMode      = GenerationMode.Mixed;
  protected selectedIntensity = CorruptionIntensity.Medium;
  protected seedInput         = '';     // empty = new random seed

  // -------------------------------------------------------------------------
  // Page state
  // -------------------------------------------------------------------------

  protected readonly isGenerating = signal(false);
  protected readonly generationError = signal<string | null>(null);
  protected readonly lastResult = signal<GeneratedDatasetMetadata | null>(null);
  protected readonly downloadingFile = signal<DownloadFile | null>(null);

  protected get isClean(): boolean {
    return this.selectedMode === GenerationMode.Clean;
  }

  // -------------------------------------------------------------------------
  // Generate
  // -------------------------------------------------------------------------

  protected generate(): void {
    if (this.isGenerating()) return;

    const seed = this.seedInput.trim()
      ? parseInt(this.seedInput.trim(), 10)
      : null;

    if (this.seedInput.trim() && (isNaN(seed!) || seed! < 0)) {
      this.generationError.set('Seed must be a positive integer, or leave blank for a random seed.');
      return;
    }

    this.isGenerating.set(true);
    this.generationError.set(null);

    this.api
      .generate({
        size:      this.selectedSize,
        mode:      this.selectedMode,
        intensity: this.selectedIntensity,
        seed:      seed,
      })
      .subscribe({
        next: (response) => {
          this.lastResult.set(response.metadata);
          this.isGenerating.set(false);
        },
        error: () => {
          this.generationError.set(
            'Generation failed. Check that the FinSight API is running and try again.',
          );
          this.isGenerating.set(false);
        },
      });
  }

  // -------------------------------------------------------------------------
  // Downloads
  // -------------------------------------------------------------------------

  protected download(fileType: DownloadFile): void {
    const result = this.lastResult();
    if (!result || this.downloadingFile()) return;

    this.downloadingFile.set(fileType);

    this.api.downloadFile(result.generationId, fileType).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a   = document.createElement('a');
        a.href     = url;
        a.download  = `${fileType}.csv`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        this.downloadingFile.set(null);
      },
      error: () => {
        this.downloadingFile.set(null);
      },
    });
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  protected modeName(mode: GenerationMode): string {
    return (
      this.modeOptions.find((o) => o.value === mode)?.label ?? String(mode)
    );
  }

  protected modeDescription(mode: GenerationMode): string {
    return (
      this.modeOptions.find((o) => o.value === mode)?.description ?? ''
    );
  }

  protected intensityLabel(intensity: CorruptionIntensity | null): string {
    if (intensity === null) return '—';
    return (
      this.intensityOptions.find((o) => o.value === intensity)?.label ??
      String(intensity)
    );
  }

  protected distributionEntries(
    dist: Record<string, number>,
  ): Array<{ label: string; count: number }> {
    const order = ['Matched', 'Mismatched', 'Missing', 'Duplicate', 'Unresolved'];
    return order
      .filter((k) => k in dist)
      .map((k) => ({ label: k, count: dist[k] }));
  }

  protected formattedSeed(seed: number): string {
    return seed.toLocaleString('en-US');
  }

  protected readonly GenerationMode = GenerationMode;
}
