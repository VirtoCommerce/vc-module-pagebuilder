import { TestBed } from '@angular/core/testing';
import { MockStore, provideMockStore } from '@ngrx/store/testing';

import { AppConfig } from '@integration/services';
import { ContextMenuHelper, helpers } from '@editor/helpers';
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
                key: 'shared-component::component-1',
                template: null,
                isLoading: false,
                error: 'The component could not be fetched.',
              },
            },
            { selector: routingSelectors.selectSharedComponentIdParameter, value: 'component-1' },
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
    expect(dispatch).toHaveBeenCalledWith(actions.loadTemplateModel({ templateKey: 'shared-component::component-1' }));

    back!.click();
    expect(dispatch).toHaveBeenCalledWith(actions.closeSharedComponent());
  });

  it('keeps the main Add block footer visible while the template is loading', () => {
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[footer-content]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-shared-components-library')).toBeNull();
  });

  it('inserts at each explicit gap, appends from the footer and limits keyboard reorder to the page', () => {
    const content = [{ id: 'first', type: 'text', hidden: false, blocks: [] }, { id: 'last', type: 'text', hidden: false, blocks: [] }];
    const context = {
      template: { content }, templateState: { key: 'page-1' },
      settings: {}, settingsSchemas: { top: [], bottom: [] },
      sectionsSchemas: { text: { name: 'Text', icon: 'description' } }, blocksSchemas: {},
      sectionsState: { first: { blocks: {} }, last: { blocks: {} } },
      sharedComponents: {}, sharedComponentErrors: {},
      currentDragSection: [], selectMode: false, isSharedComponentDocument: false,
    } as unknown as NonNullable<ReturnType<typeof selectors.editTemplateContext>>;
    store.overrideSelector(selectors.editTemplateContext, context);
    store.overrideSelector(routingSelectors.selectSharedComponentIdParameter, '');
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    fixture.detectChanges();
    const dispatch = vi.spyOn(store, 'dispatch');
    const insertions = fixture.nativeElement.querySelectorAll('.insert-section-button') as NodeListOf<HTMLButtonElement>;
    expect(insertions.length).toBe(3);
    insertions.forEach((button, index) => {
      button.click();
      expect(dispatch).toHaveBeenLastCalledWith(actions.showBlankSections({ sectionId: null, positionIndex: index }));
    });
    fixture.nativeElement.querySelector('[footer-content] button').click();
    expect(dispatch).toHaveBeenLastCalledWith(actions.showBlankSections({ sectionId: null, positionIndex: 2 }));
    dispatch.mockClear();
    fixture.componentInstance.moveSection(0, -1);
    fixture.componentInstance.moveSection(1, 1);
    expect(dispatch).not.toHaveBeenCalled();
    fixture.nativeElement.querySelector('app-drag-handle button').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    expect(dispatch).toHaveBeenCalledWith(actions.sortItems({
      options: { item: content[0], previousIndex: 0, currentIndex: 1 },
    }));
    expect(fixture.componentInstance.moveAnnouncement()).toBe('Section moved to position 2 of 2');

    const third = { id: 'third', type: 'text', hidden: false, blocks: [] };
    store.overrideSelector(selectors.editTemplateContext, {
      ...context,
      template: { ...context.template!, content: [...content, third] },
      sectionsState: {
        ...context.sectionsState,
        first: { ...context.sectionsState['first'], selected: true },
        last: { ...context.sectionsState['last'], selected: true },
        third: { ...context.sectionsState['last'], selected: false },
      },
      selectMode: true,
    });
    store.refreshState();
    fixture.detectChanges();
    dispatch.mockClear();
    fixture.componentInstance.moveSection(0, 1);
    const reorder = actions.sortItems({ options: { item: content[0], previousIndex: 0, currentIndex: 2 } });
    expect(dispatch).toHaveBeenCalledWith(reorder);
    const result = helpers.reorderSections({ content: [...content, third], settings: content[0] },
      reorder.options.currentIndex, reorder.options.previousIndex, ['first', 'last']);
    expect(result.content.map(section => section.id)).toEqual(['third', 'first', 'last']);
    dispatch.mockClear();
    fixture.componentInstance.moveSection(2, -1);
    expect(dispatch).not.toHaveBeenCalled();
  });
});
