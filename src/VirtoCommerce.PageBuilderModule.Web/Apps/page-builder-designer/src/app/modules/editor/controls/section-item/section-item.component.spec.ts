import { TestBed } from '@angular/core/testing';

import { ContextMenuHelper } from '@editor/helpers';
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
});
