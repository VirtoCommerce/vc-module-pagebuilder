import { TestBed } from '@angular/core/testing';
import { OverlayContainer } from '@angular/cdk/overlay';
import { ContextMenuComponent } from './context-menu.component';
import { ContextMenuHelper } from '@editor/helpers';
import { ClipboardService } from '@core/services';
import { AppConfig } from '@integration/services';

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
    expect(buttons[1].getAttribute('aria-disabled')).toBe('true');
    expect(buttons[1].disabled).toBe(false);
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    const escapedToDocument = vi.fn();
    document.addEventListener('keydown', escapedToDocument);
    const escape = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    buttons[0].dispatchEvent(escape);
    document.removeEventListener('keydown', escapedToDocument);
    fixture.detectChanges();
    expect(escape.defaultPrevented).toBe(true);
    expect(escapedToDocument).not.toHaveBeenCalled();
    expect(fixture.componentInstance.isOpen()).toBe(false);
    expect(overlay.querySelector('.panel')).toBeNull();
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
