import { TestBed } from '@angular/core/testing';
import { provideMockStore } from '@ngrx/store/testing';

import { ContextMenuHelper } from '@editor/helpers';
import * as selectors from '@editor/store/selectors';
import { AppConfig } from '@integration/services';
import * as routingSelectors from '@shared/routing/selectors';

import { EditSectionComponent } from './edit-section.component';

describe('EditSectionComponent Shared Component permissions', () => {
  function createFixture(canEdit: boolean) {
    TestBed.configureTestingModule({
      imports: [EditSectionComponent],
      providers: [
        provideMockStore({
          selectors: [
            {
              selector: selectors.selectEditSectionContext,
              value: {
                editContext: {},
                schema: { settings: [], blocks: [] },
                model: { id: 'section-1', type: 'hero' },
                section: null,
                block: null,
                isEditSettings: false,
              },
            },
            { selector: selectors.selectSharedComponentInstanceFromRoute, value: null },
            {
              selector: selectors.selectCurrentSharedComponent,
              value: { id: 'component-1', name: 'Hero', usageCount: 2, usagePages: [] },
            },
            { selector: selectors.selectCurrentItemName, value: 'Hero' },
            { selector: routingSelectors.isDesktop50, value: false },
            { selector: routingSelectors.selectSharedComponentIdParameter, value: 'component-1' },
          ],
        }),
        {
          provide: AppConfig,
          useValue: {
            getValue: vi.fn((option: string) => option === 'canInsertSharedComponents' || canEdit),
          },
        },
        { provide: ContextMenuHelper, useValue: { getSectionsActions: vi.fn().mockReturnValue([]) } },
      ],
    });

    const fixture = TestBed.createComponent(EditSectionComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('makes the dynamic editor inert for View original access', () => {
    const boundary = createFixture(false).nativeElement.querySelector('.dynamic-form-boundary') as HTMLElement;

    expect(boundary.hasAttribute('inert')).toBe(true);
    expect(boundary.getAttribute('aria-disabled')).toBe('true');
  });

  it('keeps the dynamic editor interactive with update permission', () => {
    const boundary = createFixture(true).nativeElement.querySelector('.dynamic-form-boundary') as HTMLElement;

    expect(boundary.hasAttribute('inert')).toBe(false);
    expect(boundary.hasAttribute('aria-disabled')).toBe(false);
  });
});
