import { TestBed } from '@angular/core/testing';
import { OverlayContainer } from '@angular/cdk/overlay';
import { ContextMenuComponent } from './context-menu.component';

describe('ContextMenuComponent keyboard actions', () => {
  it('loads actions before opening, disables unavailable actions and dismisses only itself on Escape', async () => {
    const fixture = TestBed.createComponent(ContextMenuComponent);
    fixture.componentRef.setInput('getActions', async () => [
      { action: 'edit', title: 'Edit', icon: 'edit' },
      { action: 'delete', title: 'Delete', icon: 'delete', inactive: true },
    ]);
    fixture.componentRef.setInput('accessibleLabel', 'Actions for Text');
    await fixture.whenStable();
    const trigger = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(trigger.getAttribute('aria-label')).toBe('Actions for Text');
    trigger.click();
    await fixture.whenStable();
    const overlay = TestBed.inject(OverlayContainer).getContainerElement();
    const buttons = overlay.querySelectorAll('button');
    expect(buttons.length).toBe(2);
    expect(buttons[1].disabled).toBe(true);
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    buttons[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    fixture.detectChanges();
    expect(fixture.componentInstance.isOpen()).toBe(false);
    expect(overlay.querySelector('.panel')).toBeNull();
  });
});
