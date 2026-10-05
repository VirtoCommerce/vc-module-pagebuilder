import { TestBed } from '@angular/core/testing';
import { OverlayContainer } from '@angular/cdk/overlay';
import { ContextMenuComponent } from './context-menu.component';
import { ContextMenuHelper } from '@editor/helpers';
import { ClipboardService } from '@core/services';
import { AppConfig } from '@integration/services';

describe('ContextMenuComponent keyboard actions', () => {
  async function openMenu() {
    const fixture = TestBed.createComponent(ContextMenuComponent);
    fixture.componentRef.setInput('actions', [
      { action: 'edit', title: 'Edit', icon: 'edit' },
      { action: 'delete', title: 'Delete', icon: 'delete', inactive: true },
    ]);
    fixture.componentRef.setInput('accessibleLabel', 'Actions for Text');
    await fixture.whenStable();
    const trigger = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    trigger.focus();
    trigger.click();
    await fixture.whenStable();
    const overlay = TestBed.inject(OverlayContainer).getContainerElement();
    return { fixture, trigger, overlay, buttons: overlay.querySelectorAll('button') };
  }

  it('labels its trigger and menu and focuses the first action', async () => {
    const { trigger, overlay, buttons } = await openMenu();
    expect(trigger.getAttribute('aria-label')).toBe('Actions for Text');
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(overlay.querySelector('[role="menu"]')?.getAttribute('aria-label')).toBe('Actions for Text');
    expect(document.activeElement).toBe(buttons[0]);
  });

  it('keeps unavailable actions discoverable with arrows without activating them', async () => {
    const { fixture, buttons } = await openMenu();
    const action = vi.fn();
    fixture.componentInstance.onAction.subscribe(action);
    buttons[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', keyCode: 40, bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(buttons[1]);
    expect(buttons[1].getAttribute('aria-disabled')).toBe('true');
    expect(buttons[1].disabled).toBe(false);
    buttons[1].click();
    expect(action).not.toHaveBeenCalled();
    expect(fixture.componentInstance.isOpen()).toBe(true);
  });

  it('dismisses exactly once through CDK Escape, contains the event and restores the opener', async () => {
    const { fixture, trigger, overlay, buttons } = await openMenu();
    const hide = vi.spyOn(fixture.componentInstance, 'hideActions');
    const escapedToDocument = vi.fn();
    document.addEventListener('keydown', escapedToDocument);
    const escape = new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true, cancelable: true });
    try {
      buttons[0].dispatchEvent(escape);
      await fixture.whenStable();
      expect(hide).toHaveBeenCalledOnce();
      expect(escape.defaultPrevented).toBe(true);
      expect(escapedToDocument).not.toHaveBeenCalled();
      expect(overlay.querySelector('.panel')).toBeNull();
      expect(document.activeElement).toBe(trigger);
    } finally {
      document.removeEventListener('keydown', escapedToDocument);
    }
  });

  it.each([false, true])('closes on Tab (shift=%s) without preventing native navigation', async shiftKey => {
    const { fixture, trigger, buttons } = await openMenu();
    const event = new KeyboardEvent('keydown', { key: 'Tab', keyCode: 9, shiftKey, bubbles: true, cancelable: true });
    buttons[0].dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
    expect(fixture.componentInstance.isOpen()).toBe(false);
    expect(document.activeElement).toBe(trigger);
    await fixture.whenStable();
    expect(document.activeElement).toBe(trigger);
  });

  it('restores a row opener when opened through its context menu', async () => {
    const { fixture } = await openMenu();
    fixture.componentInstance.hideActions();
    await fixture.whenStable();
    const row = document.createElement('button');
    document.body.append(row);
    try {
      row.focus();
      await fixture.componentInstance.showActions();
      await fixture.whenStable();
      fixture.componentInstance.hideActions();
      expect(document.activeElement).toBe(row);
    } finally {
      row.remove();
    }
  });

  it('does not reopen after a pending action load was dismissed', async () => {
    const fixture = TestBed.createComponent(ContextMenuComponent);
    let resolve!: (actions: []) => void;
    fixture.componentRef.setInput('getActions', () => new Promise<[]>(done => { resolve = done; }));
    await fixture.whenStable();
    const pending = fixture.componentInstance.showActions();
    fixture.componentInstance.hideActions();
    resolve([]);
    await pending;
    expect(fixture.componentInstance.isOpen()).toBe(false);
  });

  it('logs action load failures and keeps the menu closed', async () => {
    const error = new Error('Failed to load');
    const log = vi.spyOn(console, 'error').mockImplementation(() => {});
    const fixture = TestBed.createComponent(ContextMenuComponent);
    fixture.componentRef.setInput('getActions', () => Promise.reject(error));
    await fixture.whenStable();
    await fixture.componentInstance.showActions();
    expect(log).toHaveBeenCalledWith('Could not load context menu actions', error);
    expect(fixture.componentInstance.isOpen()).toBe(false);
  });

  it('opens Paste without waiting for a clipboard read that never settles', async () => {
    const getData = vi.fn(() => new Promise(() => {}));
    TestBed.configureTestingModule({
      providers: [
        ContextMenuHelper,
        { provide: ClipboardService, useValue: { getData } },
        { provide: AppConfig, useValue: { getValue: () => true } },
      ],
    });
    const helper = TestBed.inject(ContextMenuHelper);
    const fixture = TestBed.createComponent(ContextMenuComponent);
    fixture.componentRef.setInput('getActions', () => helper.getPageActions());
    await fixture.whenStable();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    expect(fixture.componentInstance.isOpen()).toBe(true);
    expect(TestBed.inject(OverlayContainer).getContainerElement().textContent).toContain('Paste section');
    expect(getData).not.toHaveBeenCalled();
  });
});
