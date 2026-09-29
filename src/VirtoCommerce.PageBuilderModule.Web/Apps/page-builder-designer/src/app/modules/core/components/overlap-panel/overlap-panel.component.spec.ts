import { TestBed } from '@angular/core/testing';
import { OverlapPanelComponent } from './overlap-panel.component';

describe('OverlapPanelComponent dismissal', () => {
  it('dismisses a labelled panel with Escape but leaves an already-handled Escape alone', async () => {
    const fixture = TestBed.createComponent(OverlapPanelComponent);
    fixture.componentRef.setInput('dismissible', true);
    fixture.componentRef.setInput('accessibleLabel', 'Add block');
    await fixture.whenStable();
    const dismissed = vi.fn();
    fixture.componentInstance.dismissed.subscribe(dismissed);
    const content = fixture.nativeElement.querySelector('[role="region"]') as HTMLElement;
    expect(content.getAttribute('aria-label')).toBe('Add block');
    content.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(dismissed).toHaveBeenCalledOnce();
    const handled = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    handled.preventDefault();
    content.dispatchEvent(handled);
    expect(dismissed).toHaveBeenCalledOnce();
  });

  it('lets an open ng-select consume Escape and dismisses after the dropdown is closed', async () => {
    const fixture = TestBed.createComponent(OverlapPanelComponent);
    fixture.componentRef.setInput('dismissible', true);
    await fixture.whenStable();
    const dismissed = vi.fn();
    fixture.componentInstance.dismissed.subscribe(dismissed);
    const select = document.createElement('ng-select');
    const input = document.createElement('input');
    select.append(input);
    fixture.nativeElement.querySelector('.content').append(select);

    select.classList.add('ng-select-opened');
    const first = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    first.preventDefault();
    input.dispatchEvent(first);
    expect(dismissed).not.toHaveBeenCalled();

    select.classList.remove('ng-select-opened');
    const second = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    second.preventDefault();
    input.dispatchEvent(second);
    expect(dismissed).toHaveBeenCalledOnce();
  });
});
