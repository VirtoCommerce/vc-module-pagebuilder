import { TestBed } from '@angular/core/testing';
import { DragHandleComponent } from './drag-handle.component';

describe('DragHandleComponent keyboard movement', () => {
  it('moves with arrow keys, prevents scrolling and respects read-only access', async () => {
    const fixture = TestBed.createComponent(DragHandleComponent);
    fixture.componentRef.setInput('info', 'Move Text');
    fixture.componentRef.setInput('keyboardMove', true);
    await fixture.whenStable();
    const move = vi.fn();
    fixture.componentInstance.move.subscribe(move);
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Move Text');
    for (const key of ['ArrowUp', 'ArrowDown']) {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
      button.dispatchEvent(event);
      expect(event.defaultPrevented).toBe(true);
    }
    expect(move.mock.calls).toEqual([[-1], [1]]);
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(button.disabled).toBe(true);
    expect(move).toHaveBeenCalledTimes(2);
  });
});
