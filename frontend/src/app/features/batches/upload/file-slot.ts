import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';

/**
 * One file-selection slot (Payment, Bank, or Settlement) on the batch
 * upload page.
 *
 * This component holds no validity judgement of its own -- a file is only
 * ever `empty` or `selected` here. Whether the file's *content* is valid is
 * something only the real `POST /api/batches` response can answer; this
 * component never claims otherwise.
 *
 * The native `<input type="file">` stays the single real control for both
 * mouse and keyboard: it is visually hidden (`sr-only`, not `display:none`,
 * so it keeps focus and screen-reader visibility) and a visible `<label>`
 * sibling drives its focus ring via Tailwind's `peer` utility. Native
 * drag-and-drop is additive on top of that same label -- it never replaces
 * the click/keyboard path.
 */
@Component({
  selector: 'app-file-slot',
  templateUrl: './file-slot.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FileSlot {
  private static nextId = 0;

  protected readonly inputId = `file-slot-${FileSlot.nextId++}`;

  readonly label = input.required<string>();
  readonly hint = input('');
  readonly file = input<File | null>(null);
  readonly disabled = input(false);

  readonly fileSelected = output<File>();
  readonly fileRemoved = output<void>();

  protected readonly isDragOver = signal(false);

  protected onInputChange(event: Event): void {
    const target = event.target as HTMLInputElement;
    const picked = target.files?.[0];

    if (picked) {
      this.fileSelected.emit(picked);
    }

    // Allow picking the same filename again later (e.g. after Remove).
    target.value = '';
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();

    if (this.disabled()) {
      return;
    }

    this.isDragOver.set(true);
  }

  protected onDragLeave(): void {
    this.isDragOver.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);

    if (this.disabled()) {
      return;
    }

    const dropped = event.dataTransfer?.files?.[0];

    if (dropped) {
      this.fileSelected.emit(dropped);
    }
  }

  protected remove(): void {
    this.fileRemoved.emit();
  }

  protected formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }

    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
