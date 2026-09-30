import { TestBed } from '@angular/core/testing';
import { DragHandleComponent } from './drag-handle.component';

describe('DragHandleComponent keyboard movement', () => {
  async function renderHandle(keyboardMove = true, disabled = false) {
    const fixture = TestBed.createComponent(DragHandleComponent);
    fixture.componentRef.setInput('info', 'Move Text');
    fixture.componentRef.setInput('keyboardMove', keyboardMove);
    fixture.componentRef.setInput('disabled', disabled);
    await fixture.whenStable();
    const move = vi.fn();
    fixture.componentInstance.move.subscribe(move);
    return { fixture, move, button: fixture.nativeElement.querySelector('button') as HTMLButtonElement };
  }

  it('preserves a non-focusable mouse-only handle for callers without keyboard movement', async () => {
    const { fixture } = await renderHandle(false);
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    const event = new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true });
    fixture.componentInstance.onKeydown(event);
    expect(event.defaultPrevented).toBe(false);
  });

  it.each([['ArrowUp', -1], ['ArrowDown', 1]])('moves with %s and prevents page scrolling', async (key, offset) => {
    const { button, move } = await renderHandle();
    expect(button.getAttribute('aria-label')).toBe('Move Text');
    const event = new KeyboardEvent('keydown', { key: key as string, bubbles: true, cancelable: true });
    button.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect(move).toHaveBeenCalledExactlyOnceWith(offset);
  });

  it('does not emit moves for a held arrow key', async () => {
    const { button, move } = await renderHandle();
    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', repeat: true, bubbles: true, cancelable: true }));
    expect(move).not.toHaveBeenCalled();
  });

  it('does not emit moves when disabled', async () => {
    const { button, move } = await renderHandle(true, true);
    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    expect(button.disabled).toBe(true);
    expect(move).not.toHaveBeenCalled();
  });
});
