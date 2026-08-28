import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FileSlot } from './file-slot';

describe('FileSlot', () => {
  let fixture: ComponentFixture<FileSlot>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FileSlot] });
    fixture = TestBed.createComponent(FileSlot);
    fixture.componentRef.setInput('label', 'Payment file');
    fixture.detectChanges();
  });

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function input(): HTMLInputElement {
    return el().querySelector<HTMLInputElement>('[data-testid="file-slot-input"]')!;
  }

  function makeFile(name = 'payments.csv'): File {
    return new File(['payment_record_id,amount\nPAY-000001,10.00\n'], name, {
      type: 'text/csv',
    });
  }

  function selectViaInput(file: File): void {
    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(file);
    input().files = dataTransfer.files;
    input().dispatchEvent(new Event('change'));
  }

  it('renders the dropzone when no file is selected', () => {
    expect(el().querySelector('[data-testid="file-slot-dropzone"]')).toBeTruthy();
    expect(el().querySelector('[data-testid="file-slot-selected"]')).toBeFalsy();
  });

  it('emits fileSelected when a file is chosen via the input (Browse/keyboard path)', () => {
    let emitted: File | undefined;
    fixture.componentInstance.fileSelected.subscribe((f) => (emitted = f));

    selectViaInput(makeFile('payments.csv'));

    expect(emitted?.name).toBe('payments.csv');
  });

  it('renders the selected file name and size once chosen', () => {
    fixture.componentInstance.fileSelected.subscribe(() => {
      fixture.componentRef.setInput('file', makeFile('payments.csv'));
    });

    selectViaInput(makeFile('payments.csv'));
    fixture.detectChanges();

    const selected = el().querySelector('[data-testid="file-slot-selected"]')!;
    expect(selected.textContent).toContain('payments.csv');
    expect(selected.querySelector('[title="payments.csv"]')).toBeTruthy();
  });

  it('emits fileSelected on a native drop', () => {
    let emitted: File | undefined;
    fixture.componentInstance.fileSelected.subscribe((f) => (emitted = f));

    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(makeFile('dropped.csv'));
    const dropzone = el().querySelector('[data-testid="file-slot-dropzone"]')!;
    dropzone.dispatchEvent(new DragEvent('drop', { dataTransfer, bubbles: true, cancelable: true }));

    expect(emitted?.name).toBe('dropped.csv');
  });

  it('emits fileRemoved when Remove is clicked', () => {
    fixture.componentRef.setInput('file', makeFile('payments.csv'));
    fixture.detectChanges();

    let removed = false;
    fixture.componentInstance.fileRemoved.subscribe(() => (removed = true));

    el().querySelector<HTMLButtonElement>('[data-testid="file-slot-selected"] button')!.click();

    expect(removed).toBeTrue();
  });

  it('allows replacing an already-selected file via the same input', () => {
    fixture.componentRef.setInput('file', makeFile('payments.csv'));
    fixture.detectChanges();

    let emitted: File | undefined;
    fixture.componentInstance.fileSelected.subscribe((f) => (emitted = f));

    selectViaInput(makeFile('payments-corrected.csv'));

    expect(emitted?.name).toBe('payments-corrected.csv');
  });

  it('ignores drag-over and drop while disabled', () => {
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    let emitted = false;
    fixture.componentInstance.fileSelected.subscribe(() => (emitted = true));

    const dropzone = el().querySelector('[data-testid="file-slot-dropzone"]')!;
    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(makeFile());
    dropzone.dispatchEvent(new DragEvent('drop', { dataTransfer, bubbles: true, cancelable: true }));

    expect(emitted).toBeFalse();
    expect(dropzone.classList.contains('pointer-events-none')).toBeTrue();
  });

  it('disables the Remove button while disabled', () => {
    fixture.componentRef.setInput('file', makeFile('payments.csv'));
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    const removeButton = el().querySelector<HTMLButtonElement>(
      '[data-testid="file-slot-selected"] button',
    )!;
    expect(removeButton.disabled).toBeTrue();
  });

  it('associates the visible label with the real input for keyboard/AT operation', () => {
    const labelSpan = el().querySelector('span')!;
    const fileInput = input();

    expect(fileInput.getAttribute('aria-labelledby')).toBe(labelSpan.id);
    expect(labelSpan.textContent).toBe('Payment file');

    const dropzoneLabel = el().querySelector<HTMLLabelElement>('[data-testid="file-slot-dropzone"]')!;
    expect(dropzoneLabel.getAttribute('for')).toBe(fileInput.id);
  });
});
