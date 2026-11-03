import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NgSelectComponent } from '@ng-select/ng-select';
import { OverlapPanelComponent } from './overlap-panel.component';

@Component({
  imports: [OverlapPanelComponent, NgSelectComponent],
  template: '<app-overlap-panel [dismissible]="true" (dismissed)="dismissed = dismissed + 1"><ng-select [items]="items" /></app-overlap-panel>',
})
class SelectPanelFixture {
  items = ['One', 'Two'];
  dismissed = 0;
}

describe('OverlapPanelComponent dismissal', () => {
  async function renderPanel() {
    const fixture = TestBed.createComponent(OverlapPanelComponent);
    fixture.componentRef.setInput('dismissible', true);
    fixture.componentRef.setInput('accessibleLabel', 'Add block');
    await fixture.whenStable();
    const dismissed = vi.fn();
    fixture.componentInstance.dismissed.subscribe(dismissed);
    return { fixture, dismissed, content: fixture.nativeElement.querySelector('[role="region"]') as HTMLElement };
  }

  it('labels an editing region without trapping focus', async () => {
    const { fixture, content } = await renderPanel();
    expect(content.getAttribute('aria-label')).toBe('Add block');
    expect(fixture.nativeElement.querySelector('[cdkTrapFocus]')).toBeNull();
  });

  it('focuses a nonmodal region and restores the opener when removed', async () => {
    const opener = document.createElement('button');
    document.body.append(opener);
    try {
      opener.focus();
      const { fixture, content } = await renderPanel();
      expect(document.activeElement).toBe(content);
      fixture.destroy();
      expect(document.activeElement).toBe(opener);
    } finally {
      opener.remove();
    }
  });

  it('does not steal focus from another control when removed', async () => {
    const other = document.createElement('button');
    document.body.append(other);
    try {
      const { fixture } = await renderPanel();
      other.focus();
      fixture.destroy();
      expect(document.activeElement).toBe(other);
    } finally {
      other.remove();
    }
  });

  it('dismisses and contains Escape from its content', async () => {
    const { content, dismissed } = await renderPanel();
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    content.dispatchEvent(event);
    expect(dismissed).toHaveBeenCalledOnce();
    expect(event.defaultPrevented).toBe(true);
  });

  it('dismisses when Escape is pressed on the panel width button', async () => {
    const { fixture, dismissed } = await renderPanel();
    fixture.nativeElement.querySelector('.expand').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(dismissed).toHaveBeenCalledOnce();
  });

  it('leaves an Escape consumed by a nested control alone', async () => {
    const { content, dismissed } = await renderPanel();
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    event.preventDefault();
    content.dispatchEvent(event);
    expect(dismissed).not.toHaveBeenCalled();
  });

  it('lets the real ng-select consume the first Escape and dismisses on the second', async () => {
    const fixture = TestBed.createComponent(SelectPanelFixture);
    await fixture.whenStable();
    const select = fixture.debugElement.query(By.directive(NgSelectComponent)).componentInstance as NgSelectComponent;
    select.open();
    await fixture.whenStable();
    const input = fixture.nativeElement.querySelector('ng-select input') as HTMLInputElement;
    input.focus();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(select.isOpen()).toBe(false);
    expect(fixture.componentInstance.dismissed).toBe(0);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(fixture.componentInstance.dismissed).toBe(1);
  });
});
