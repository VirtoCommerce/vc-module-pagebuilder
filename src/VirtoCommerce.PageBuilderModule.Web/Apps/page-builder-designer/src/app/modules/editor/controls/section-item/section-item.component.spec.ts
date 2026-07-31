import { TestBed } from '@angular/core/testing';

import { ContextMenuHelper, createLinkedComponentReference } from '@editor/helpers';
import { LinkedComponent } from '@editor/models';
import { SectionModel } from '@models/document';

import { SectionItemComponent } from './section-item.component';

describe('SectionItemComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SectionItemComponent],
      providers: [{ provide: ContextMenuHelper, useValue: { getSectionsActions: vi.fn().mockResolvedValue([]) } }],
    });
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
    const linkedComponent: LinkedComponent = {
      id: 'component-1',
      storeId: 'store-1',
      name: 'USP bar',
      usageCount: 4,
      usagePages: [],
    };
    fixture.componentRef.setInput('section', createLinkedComponentReference(linkedComponent.id, 'placement-1'));
    fixture.componentRef.setInput('linkedComponent', linkedComponent);

    await fixture.whenStable();

    const badge = fixture.nativeElement.querySelector('.linked-badge') as HTMLElement;
    expect(badge.textContent?.replace(/\s+/g, ' ').trim()).toBe('Shared · 4');
    expect(badge.title).toBe('Used on 4 page(s)');
    expect(badge.querySelector('.linked-usage')?.getAttribute('aria-label')).toBe('Used on 4 page(s)');
  });
});
