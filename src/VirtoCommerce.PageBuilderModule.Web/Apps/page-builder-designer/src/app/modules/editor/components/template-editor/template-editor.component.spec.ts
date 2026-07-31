import { TestBed } from '@angular/core/testing';
import { MockStore, provideMockStore } from '@ngrx/store/testing';

import { AppConfig } from '@integration/services';
import { ContextMenuHelper } from '@editor/helpers';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import * as routingSelectors from '@shared/routing/selectors';

import { TemplateEditorComponent } from './template-editor.component';

describe('TemplateEditorComponent', () => {
  let store: MockStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TemplateEditorComponent],
      providers: [
        provideMockStore({
          selectors: [
            { selector: selectors.editTemplateContext, value: undefined },
            {
              selector: selectors.selectCurrentTemplateState,
              value: {
                key: 'linked-component::component-1',
                template: null,
                isLoading: false,
                error: 'The component could not be fetched.',
              },
            },
            { selector: routingSelectors.selectLinkedComponentIdParameter, value: 'component-1' },
            { selector: selectors.hoveredSectionId, value: null },
            { selector: selectors.selectCurrentTemplateName, value: 'Shared Component' },
          ],
        }),
        { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(true) } },
        { provide: ContextMenuHelper, useValue: { getPageActions: vi.fn().mockReturnValue([]) } },
      ],
    });
    store = TestBed.inject(MockStore);
  });

  it('keeps a failed Shared Component load recoverable with Back and Retry actions', () => {
    const dispatch = vi.spyOn(store, 'dispatch');
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    const retry = buttons.find((button) => button.textContent?.includes('Retry'));
    const back = buttons.find((button) => button.textContent?.includes('Back'));

    expect(alert.textContent).toContain('Could not load this Shared Component');
    expect(alert.textContent).toContain('The component could not be fetched.');
    expect(retry).toBeTruthy();
    expect(back).toBeTruthy();

    retry!.click();
    expect(dispatch).toHaveBeenCalledWith(actions.loadTemplateModel({ templateKey: 'linked-component::component-1' }));

    back!.click();
    expect(dispatch).toHaveBeenCalledWith(actions.closeLinkedComponent());
  });

  it('keeps the main Add block footer visible while the template is loading', () => {
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[footer-content]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-linked-components-library')).toBeNull();
  });
});
