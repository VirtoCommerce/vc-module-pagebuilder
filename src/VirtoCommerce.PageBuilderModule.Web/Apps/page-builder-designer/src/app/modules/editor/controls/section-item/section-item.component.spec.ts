import { TestBed } from '@angular/core/testing';

import { ContextMenuHelper, createSharedComponentReference } from '@editor/helpers';
import { SharedComponent } from '@editor/models';
import { SectionModel } from '@models/document';

import { SectionItemComponent } from './section-item.component';

describe('SectionItemComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SectionItemComponent],
      providers: [{ provide: ContextMenuHelper, useValue: { getSectionsActions: vi.fn().mockResolvedValue([]) } }],
    });
  });

  it('opens section actions on right click without opening the editor', async () => {
    vi.mocked(TestBed.inject(ContextMenuHelper).getSectionsActions).mockResolvedValue([
      { title: 'Copy', action: 'copy', icon: 'content_copy' },
    ]);
    const fixture = TestBed.createComponent(SectionItemComponent);
    fixture.componentRef.setInput('section', { id: 'section-1', type: 'text' } as SectionModel);
    fixture.componentRef.setInput('sectionSchema', { name: 'Text' });
    fixture.componentRef.setInput('hasContextMenu', true);
    await fixture.whenStable();
    const edit = vi.fn();
    fixture.componentInstance.itemClick.subscribe(edit);
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2 });
    fixture.nativeElement.querySelector('.section-edit-button').dispatchEvent(event);
    await fixture.whenStable();

    expect(event.defaultPrevented).toBe(true);
    expect(document.querySelector('.cdk-overlay-container .action-item')?.textContent).toContain('Copy');
    expect(edit).not.toHaveBeenCalled();
  });

  it('does not intercept right click when section actions are unavailable', async () => {
    const fixture = TestBed.createComponent(SectionItemComponent);
    fixture.componentRef.setInput('section', { id: 'section-1', type: 'text' } as SectionModel);
    await fixture.whenStable();
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2 });
    fixture.nativeElement.querySelector('.section-item').dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(TestBed.inject(ContextMenuHelper).getSectionsActions).not.toHaveBeenCalled();
  });

  it('emits hover enter and leave', async () => {
    const fixture = TestBed.createComponent(SectionItemComponent);
    fixture.componentRef.setInput('section', { id: 'section-1', type: 'text' } as SectionModel);
    await fixture.whenStable();
    const hoverStates: boolean[] = [];
    fixture.componentInstance.itemHover.subscribe((value) => hoverStates.push(value));

    fixture.componentInstance.onItemHover();
    fixture.componentInstance.onItemLeave();

    expect(hoverStates).toEqual([true, false]);
    expect(fixture.componentInstance.isHover()).toBe(false);
  });

  it('shows an explicit compact usage count for a Shared Component', async () => {
    const fixture = TestBed.createComponent(SectionItemComponent);
    const sharedComponent: SharedComponent = {
      id: 'component-1',
      storeId: 'store-1',
      name: 'USP bar',
      usageCount: 4,
      usagePages: [],
    };
    fixture.componentRef.setInput('section', createSharedComponentReference(sharedComponent.id, 'placement-1'));
    fixture.componentRef.setInput('sharedComponent', sharedComponent);

    await fixture.whenStable();

    const badge = fixture.nativeElement.querySelector('.shared-component-badge') as HTMLElement;
    expect(badge.textContent).toContain('Shared');
    expect(badge.querySelector('[aria-hidden="true"]')?.textContent).toBe('· 4');
    expect(badge.title).toBe('Used on 4 page(s)');
    expect(badge.querySelector('.sr-only')?.textContent).toBe('Used on 4 page(s)');
    expect(badge.querySelector('[aria-label]')).toBeNull();
    expect(fixture.nativeElement.querySelector('.section-edit-button').textContent).toContain('Used on 4 page(s)');
  });

  it('exposes a native edit button, named selection and actions without an undefined settings id', async () => {
    const fixture = TestBed.createComponent(SectionItemComponent);
    fixture.componentRef.setInput('section', {} as SectionModel);
    fixture.componentRef.setInput('sectionSchema', { name: 'Settings' });
    fixture.componentRef.setInput('hasContextMenu', true);
    await fixture.whenStable();
    const edit = fixture.nativeElement.querySelector('.section-edit-button') as HTMLButtonElement;
    const click = vi.fn();
    fixture.componentInstance.itemClick.subscribe(click);

    expect(edit.title).toBe('Settings');
    expect(edit.disabled).toBe(false);
    edit.click();
    expect(click).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('input[type="checkbox"]').getAttribute('aria-label')).toBe('Select Settings');
    expect(fixture.nativeElement.querySelector('app-context-menu button').getAttribute('aria-label')).toBe('Actions for Settings');
  });
});
