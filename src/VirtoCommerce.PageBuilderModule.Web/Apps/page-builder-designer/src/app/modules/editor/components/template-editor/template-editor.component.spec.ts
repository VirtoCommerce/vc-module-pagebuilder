import { TestBed } from '@angular/core/testing';
import { LiveAnnouncer } from '@angular/cdk/a11y';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { createSection, createSectionState, createTemplate, createTemplateState, createSchema } from '@app/testing';
import { SectionModel } from '@models/document';
import { AppConfig } from '@integration/services';
import { ContextMenuHelper } from '@editor/helpers';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import * as routingSelectors from '@shared/routing/selectors';
import { TemplateEditorComponent } from './template-editor.component';

type EditorContext = NonNullable<ReturnType<typeof selectors.editTemplateContext>>;

describe('TemplateEditorComponent', () => {
  let store: MockStore;
  let announce: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    announce = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [TemplateEditorComponent],
      providers: [
        provideMockStore({
          selectors: [
            { selector: selectors.editTemplateContext, value: undefined },
            { selector: selectors.selectCurrentTemplateState, value: {
              key: 'shared-component::component-1', template: null, isLoading: false,
              error: 'The component could not be fetched.',
            } },
            { selector: routingSelectors.selectSharedComponentIdParameter, value: 'component-1' },
            { selector: selectors.hoveredSectionId, value: null },
            { selector: selectors.selectCurrentTemplateName, value: 'Shared Component' },
          ],
        }),
        { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(true) } },
        { provide: ContextMenuHelper, useValue: { getPageActions: vi.fn().mockReturnValue([]) } },
        { provide: LiveAnnouncer, useValue: { announce } },
      ],
    });
    store = TestBed.inject(MockStore);
  });

  function createContext(content: SectionModel[], selected: string[] = []): EditorContext {
    return {
      template: createTemplate({ content }), templateState: { ...createTemplateState(), key: 'page-1' },
      settings: createSection({ id: 'settings', type: 'settings' }), settingsSchemas: { top: [], bottom: [] },
      sectionsSchemas: { text: createSchema({ type: 'text', name: 'Text', icon: 'description' }) }, blocksSchemas: {},
      sectionsState: Object.fromEntries(content.map(section => [section.id, createSectionState({ selected: selected.includes(section.id) })])),
      sharedComponents: {}, sharedComponentErrors: {}, currentSharedComponent: null, sharedComponentId: '',
      currentDragSection: [], selectMode: selected.length > 0, isSharedComponentDocument: false,
      selectedSectionsCount: selected.length, selectedBlocksCount: 0,
    };
  }

  async function renderPage(content = [
    createSection({ id: 'first', type: 'text' }), createSection({ id: 'last', type: 'text' }),
  ], selected: string[] = []) {
    const context = createContext(content, selected);
    store.overrideSelector(selectors.editTemplateContext, context);
    store.overrideSelector(routingSelectors.selectSharedComponentIdParameter, '');
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    await fixture.whenStable();
    const dispatch = vi.spyOn(store, 'dispatch');
    return { fixture, dispatch, context, content };
  }

  it('keeps a failed Shared Component load recoverable with Back and Retry actions', async () => {
    const dispatch = vi.spyOn(store, 'dispatch');
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    await fixture.whenStable();
    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    expect(alert.textContent).toContain('Could not load this Shared Component');
    expect(alert.textContent).toContain('The component could not be fetched.');
    buttons.find(button => button.textContent?.includes('Retry'))!.click();
    expect(dispatch).toHaveBeenCalledWith(actions.loadTemplateModel({ templateKey: 'shared-component::component-1' }));
    buttons.find(button => button.textContent?.includes('Back'))!.click();
    expect(dispatch).toHaveBeenCalledWith(actions.closeSharedComponent());
  });

  it('keeps the main Add block footer visible while the template is loading', async () => {
    const fixture = TestBed.createComponent(TemplateEditorComponent);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[footer-content]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-shared-components-library')).toBeNull();
  });

  it.each([0, 1, 2])('inserts at gap %s', async index => {
    const { fixture, dispatch } = await renderPage();
    fixture.nativeElement.querySelectorAll('.insert-section-button')[index].click();
    expect(dispatch).toHaveBeenLastCalledWith(actions.showBlankSections({ sectionId: null, positionIndex: index }));
  });

  it('appends from the footer', async () => {
    const { fixture, dispatch } = await renderPage();
    fixture.nativeElement.querySelector('[footer-content] button').click();
    expect(dispatch).toHaveBeenLastCalledWith(actions.showBlankSections({ sectionId: null, positionIndex: 2 }));
  });

  it.each([[0, -1], [1, 1]])('does not reorder beyond boundary %s with offset %s', async (index, offset) => {
    const { fixture, dispatch } = await renderPage();
    fixture.componentInstance.moveSection(index, offset);
    expect(dispatch).not.toHaveBeenCalled();
    expect(announce).not.toHaveBeenCalled();
  });

  it.each([
    { id: 'first', index: 0, key: 'ArrowDown', offset: 1 },
    { id: 'last', index: 1, key: 'ArrowUp', offset: -1 },
  ])('restores the moved section handle after $key', async ({ id, index, key, offset }) => {
    const { fixture, dispatch, context, content } = await renderPage();
    const handle = fixture.nativeElement.querySelector('[data-section-id="' + id + '"] button') as HTMLButtonElement;
    handle.focus();
    handle.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    expect(dispatch).toHaveBeenCalledWith(actions.sortItems({
      options: { item: content[index], previousIndex: index, currentIndex: index + offset },
    }));
    store.overrideSelector(selectors.editTemplateContext, { ...context, template: createTemplate({ content: [content[1], content[0]] }) });
    store.refreshState();
    await fixture.whenStable();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[data-section-id="' + id + '"] button'));
  });

  it('does not steal focus when it moves to another control before rendering', async () => {
    const { fixture } = await renderPage();
    const handle = fixture.nativeElement.querySelector('[data-section-id="last"] button') as HTMLButtonElement;
    handle.focus();
    fixture.componentInstance.moveSection(1, -1);
    const other = fixture.nativeElement.querySelector('[footer-content] button') as HTMLButtonElement;
    other.focus();
    await fixture.whenStable();
    expect(document.activeElement).toBe(other);
  });

  it('announces consecutive identical group moves through the shared LiveAnnouncer', async () => {
    const content = ['first', 'last', 'third'].map(id => createSection({ id, type: 'text' }));
    const { fixture } = await renderPage(content, ['first', 'last']);
    fixture.componentInstance.moveSection(0, 1);
    fixture.componentInstance.moveSection(0, 1);
    expect(announce.mock.calls).toEqual([['Selected sections moved down'], ['Selected sections moved down']]);
    expect(fixture.nativeElement.querySelector('[aria-live]')).toBeNull();
  });

  it('dispatches group reorder from the selected boundary', async () => {
    const content = ['first', 'last', 'third'].map(id => createSection({ id, type: 'text' }));
    const { fixture, dispatch } = await renderPage(content, ['first', 'last']);
    fixture.componentInstance.moveSection(0, 1);
    expect(dispatch).toHaveBeenCalledWith(actions.sortItems({
      options: { item: content[1], previousIndex: 1, currentIndex: 2 },
    }));
  });

  it('does not reorder an unselected section while a group is selected', async () => {
    const content = ['first', 'last', 'third'].map(id => createSection({ id, type: 'text' }));
    const { fixture, dispatch } = await renderPage(content, ['first', 'last']);
    fixture.componentInstance.moveSection(2, -1);
    expect(dispatch).not.toHaveBeenCalled();
  });
});
