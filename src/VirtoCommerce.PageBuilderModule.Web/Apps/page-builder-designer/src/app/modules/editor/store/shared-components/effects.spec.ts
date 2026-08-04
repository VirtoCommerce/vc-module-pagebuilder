import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { Action } from '@ngrx/store';
import { filter, firstValueFrom, of, ReplaySubject, Subject, take, throwError, toArray } from 'rxjs';

import { createSection, createTemplate } from '@app/testing';
import { ModalService } from '@core/services';
import { createSharedComponentReference, isSharedComponentReference } from '@editor/helpers';
import { TemplateModel } from '@models/document';
import { SharedComponentsService } from '@editor/services';
import { AppConfig } from '@integration/services';
import * as routingSelectors from '@shared/routing/selectors';
import * as routingActions from '@shared/routing/actions';
import * as sharedActions from '@shared/store/actions';
import * as sharedSelectors from '@shared/store/selectors';

import * as actions from '../actions';
import * as selectors from '../selectors';
import { SharedComponentsDataEffects } from './data/effects';
import { SharedComponentsDomainEffects } from './domain/effects';
import { SharedComponentsUiEffects } from './ui/effects';

describe('Shared Component effects', () => {
  let dataEffects: SharedComponentsDataEffects;
  let domainEffects: SharedComponentsDomainEffects;
  let uiEffects: SharedComponentsUiEffects;
  let actions$: ReplaySubject<Action>;
  let store: MockStore;
  let service: {
    storeId: string;
    search: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    getContent: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
  };
  let modals: { show: ReturnType<typeof vi.fn>; confirm: ReturnType<typeof vi.fn> };

  const component = {
    id: 'component-1',
    storeId: 'store-1',
    name: 'Shared hero',
    usageCount: 2,
    usagePages: [],
  };
  const sharedComponentContent = createTemplate({
    content: [createSection({ id: 'shared', blocks: [createSection({ id: 'shared-block' })] })],
  });
  const raw = createTemplate({
    content: [createSection({ id: 'before' }), createSharedComponentReference(component.id, 'placement-1')],
  });

  beforeEach(() => {
    actions$ = new ReplaySubject<Action>(1);
    service = {
      storeId: 'store-1',
      search: vi.fn().mockReturnValue(of({ totalCount: 1, results: [component] })),
      get: vi.fn().mockReturnValue(of(component)),
      getContent: vi.fn().mockReturnValue(of(sharedComponentContent)),
      create: vi.fn().mockReturnValue(of(component)),
    };
    modals = {
      show: vi.fn().mockReturnValue(of({ accept: true, name: component.name })),
      confirm: vi.fn().mockReturnValue(of(true)),
    };

    TestBed.configureTestingModule({
      providers: [
        SharedComponentsDataEffects,
        SharedComponentsDomainEffects,
        SharedComponentsUiEffects,
        provideMockActions(() => actions$),
        provideMockStore({
          selectors: [
            { selector: selectors.selectSharedComponents, value: {} },
            { selector: selectors.selectSharedComponentContents, value: {} },
            { selector: selectors.selectSharedComponentErrors, value: {} },
            { selector: selectors.selectSharedComponentUsageRefreshIdsByTemplate, value: {} },
            {
              selector: selectors.selectSharedComponentsSearchState,
              value: {
                keyword: '',
                resultIds: [],
                optimisticResultIds: [],
                loadedCount: 0,
                totalCount: 0,
                loading: false,
                rebasePending: false,
                error: null,
              },
            },
            { selector: selectors.selectCurrentTemplateModel, value: raw },
            { selector: selectors.selectCurrentSharedComponent, value: null },
            { selector: selectors.selectCheckedItems, value: ['before'] },
            { selector: routingSelectors.selectTemplateKeyParameter, value: 'page-1' },
            { selector: routingSelectors.selectSharedComponentIdParameter, value: '' },
            { selector: routingSelectors.selectGroupIdParameter, value: 'page-1' },
            { selector: routingSelectors.selectSectionIdParameter, value: '' },
            { selector: routingSelectors.selectQueryParams, value: {} },
            { selector: sharedSelectors.selectCurrentTemplateDirty, value: false },
          ],
        }),
        { provide: SharedComponentsService, useValue: service },
        { provide: ModalService, useValue: modals },
        { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(true) } },
      ],
    });

    dataEffects = TestBed.inject(SharedComponentsDataEffects);
    domainEffects = TestBed.inject(SharedComponentsDomainEffects);
    uiEffects = TestBed.inject(SharedComponentsUiEffects);
    store = TestBed.inject(MockStore);
  });

  afterEach(() => store.resetSelectors());

  it('loads a bounded next page and marks the result for append', async () => {
    actions$.next(actions.searchSharedComponents({ keyword: 'hero', skip: 20 }));

    expect(await firstValueFrom(dataEffects.loadMore$)).toEqual(
      actions.searchSharedComponentsSuccess({
        keyword: 'hero',
        result: { totalCount: 1, results: [component] },
        append: true,
      }),
    );
    expect(service.search).toHaveBeenCalledWith('hero', 20, 20);
  });

  it('debounces a search burst but repeats an explicit search for the same normalized keyword', async () => {
    vi.useFakeTimers();
    const emitted: Action[] = [];
    const subscription = dataEffects.search$.subscribe((action) => emitted.push(action));

    actions$.next(actions.searchSharedComponents({ keyword: 'hero' }));
    actions$.next(actions.searchSharedComponents({ keyword: '  hero  ' }));
    await vi.advanceTimersByTimeAsync(250);
    actions$.next(actions.searchSharedComponents({ keyword: 'hero' }));
    await vi.advanceTimersByTimeAsync(250);

    expect(service.search).toHaveBeenCalledTimes(2);
    expect(service.search).toHaveBeenNthCalledWith(1, 'hero', 0, 20);
    expect(service.search).toHaveBeenNthCalledWith(2, 'hero', 0, 20);
    expect(emitted).toHaveLength(2);

    subscription.unsubscribe();
    vi.useRealTimers();
  });

  it('schedules a silent first-page rebase after optimistically caching a matching created component', async () => {
    actions$.next(actions.cacheSharedComponent({ component, addToSearchResults: true }));

    expect(await firstValueFrom(dataEffects.refreshSearchAfterCreate$)).toEqual(
      actions.refreshSharedComponentsSearch({ keyword: '' }),
    );
  });

  it('refreshes an optimistic search from the first page and marks the response as a rebase', async () => {
    actions$.next(actions.refreshSharedComponentsSearch({ keyword: 'hero' }));

    expect(await firstValueFrom(dataEffects.search$)).toEqual(
      actions.searchSharedComponentsSuccess({
        keyword: 'hero',
        result: { totalCount: 1, results: [component] },
        append: false,
        rebase: true,
      }),
    );
    expect(service.search).toHaveBeenCalledWith('hero', 0, 20);
  });

  it('cancels an older retry when an optimistic search rebase starts', async () => {
    const retryResponse = new Subject<{ totalCount: number; results: (typeof component)[] }>();
    const refreshResponse = new Subject<{ totalCount: number; results: (typeof component)[] }>();
    service.search.mockReturnValueOnce(retryResponse).mockReturnValueOnce(refreshResponse);
    const results: Action[] = [];
    const subscription = dataEffects.search$.subscribe((action) => results.push(action));

    actions$.next(actions.retrySharedComponentsSearch({ keyword: 'hero' }));
    actions$.next(actions.refreshSharedComponentsSearch({ keyword: 'hero' }));
    refreshResponse.next({ totalCount: 1, results: [component] });
    refreshResponse.complete();
    retryResponse.next({ totalCount: 0, results: [] });
    retryResponse.complete();

    expect(results).toEqual([
      actions.searchSharedComponentsSuccess({
        keyword: 'hero',
        result: { totalCount: 1, results: [component] },
        append: false,
        rebase: true,
      }),
    ]);
    subscription.unsubscribe();
  });

  it('loads references and broadcasts only the resolved preview template', async () => {
    actions$.next(actions.broadcastResolvedPreview({ msg: { type: 'page', template: raw } }));

    const emitted = await firstValueFrom(dataEffects.resolvePreview$.pipe(take(3), toArray()));
    const preview = emitted.find((action) => action.type === sharedActions.broadcastPreviewMessage.type) as ReturnType<
      typeof sharedActions.broadcastPreviewMessage
    >;

    expect(service.getContent).toHaveBeenCalledWith(component.id);
    const previewTemplate = getPreviewTemplate(preview);
    expect(previewTemplate.content).toHaveLength(2);
    expect(isSharedComponentReference(previewTemplate.content[1])).toBe(false);
    expect(previewTemplate.content[1].id).not.toBe('shared');
    expect(isSharedComponentReference(raw.content[1])).toBe(true);
    expect(preview.msg['sharedComponentBoundaries']).toEqual([
      {
        placementId: 'placement-1',
        componentRef: component.id,
        startIndex: 1,
        count: 1,
        label: 'Shared · Used on 2 pages',
      },
    ]);
  });

  it('opens a usage page in its own culture and clears the shared-component route state', async () => {
    actions$.next(actions.openSharedComponentUsagePage({ pageId: 'page-group-1', cultureName: 'de-DE' }));

    expect(await firstValueFrom(uiEffects.openUsagePage$)).toEqual(
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          type: 'pages',
          groupId: 'page-group-1',
          cultureName: 'de-DE',
          path: undefined,
          parent: undefined,
          sharedComponentId: undefined,
          sharedComponentReturnPageId: undefined,
        },
      }),
    );
  });

  it('clears the previous culture when the target usage page has no culture', async () => {
    actions$.next(actions.openSharedComponentUsagePage({ pageId: 'page-group-1', cultureName: null }));

    expect(await firstValueFrom(uiEffects.openUsagePage$)).toEqual(
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          type: 'pages',
          groupId: 'page-group-1',
          cultureName: undefined,
          path: undefined,
          parent: undefined,
          sharedComponentId: undefined,
          sharedComponentReturnPageId: undefined,
        },
      }),
    );
  });

  it('navigates from a dirty page without showing the Shared Component discard dialog', async () => {
    store.overrideSelector(sharedSelectors.selectCurrentTemplateDirty, true);
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'page-1');
    store.refreshState();

    actions$.next(actions.openSharedComponentUsagePage({ pageId: 'page-group-1', cultureName: 'en-US' }));

    expect(await firstValueFrom(uiEffects.openUsagePage$)).toEqual(
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          type: 'pages',
          groupId: 'page-group-1',
          cultureName: 'en-US',
          path: undefined,
          parent: undefined,
          sharedComponentId: undefined,
          sharedComponentReturnPageId: undefined,
        },
      }),
    );
    expect(modals.confirm).not.toHaveBeenCalled();
  });

  it('keeps editing when dirty-navigation discard is cancelled', async () => {
    store.overrideSelector(sharedSelectors.selectCurrentTemplateDirty, true);
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'shared-component::component-1');
    store.refreshState();
    modals.confirm.mockReturnValue(of(false));

    actions$.next(actions.openSharedComponentUsagePage({ pageId: 'page-group-1', cultureName: 'en-US' }));

    expect(await firstValueFrom(uiEffects.openUsagePage$)).toEqual(sharedActions.empty());
    expect(modals.confirm).toHaveBeenCalledWith(
      'Discard unsaved changes and leave this Shared Component?',
      { confirmText: 'Discard', declineText: 'Keep editing' },
    );
  });

  it('clears synthetic dirty and template state before leaving a dirty original', async () => {
    const templateKey = 'shared-component::component-1';
    store.overrideSelector(sharedSelectors.selectCurrentTemplateDirty, true);
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, templateKey);
    store.refreshState();

    const emittedPromise = firstValueFrom(uiEffects.openUsagePage$.pipe(take(3), toArray()));
    actions$.next(actions.openSharedComponentUsagePage({ pageId: 'page-group-1', cultureName: 'en-US' }));

    expect(await emittedPromise).toEqual([
      sharedActions.setRootDirtyState({ templateKey, dirty: false }),
      actions.discardSharedComponentChanges({ templateKey }),
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          type: 'pages',
          groupId: 'page-group-1',
          cultureName: 'en-US',
          path: undefined,
          parent: undefined,
          sharedComponentId: undefined,
          sharedComponentReturnPageId: undefined,
        },
      }),
    ]);
  });

  it('does not open an archived usage page', () => {
    store.overrideSelector(selectors.selectCurrentSharedComponent, {
      ...component,
      usagePages: [{ id: 'archived-page', name: 'Archived', status: 'Archived' }],
    });
    store.refreshState();
    const emitted: Action[] = [];
    const subscription = uiEffects.openUsagePage$.subscribe((action) => emitted.push(action));

    actions$.next(actions.openSharedComponentUsagePage({ pageId: 'archived-page', cultureName: 'en-US' }));

    expect(emitted).toEqual([]);
    subscription.unsubscribe();
  });

  it('remembers the source page when opening the original component', async () => {
    actions$.next(actions.openSharedComponent({ componentId: component.id }));

    expect(await firstValueFrom(uiEffects.openDocument$)).toEqual(
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          sharedComponentId: component.id,
          type: 'shared-components',
          path: component.id,
          groupId: undefined,
          parent: undefined,
          sharedComponentReturnPageId: 'page-1',
        },
      }),
    );
  });

  it('returns to the source page after a shared-component editor reload', async () => {
    store.overrideSelector(selectors.selectCurrentSharedComponent, {
      ...component,
      usagePages: [{ id: 'source-page', name: 'Homepage', cultureName: 'fr-FR' }],
    });
    store.overrideSelector(routingSelectors.selectQueryParams, {
      sharedComponentReturnPageId: 'source-page',
      cultureName: 'en-US',
    });
    store.refreshState();
    actions$.next(actions.closeSharedComponent());

    expect(await firstValueFrom(uiEffects.closeDocument$)).toEqual(
      actions.openSharedComponentUsagePage({ pageId: 'source-page', cultureName: 'fr-FR' }),
    );
  });

  it('keeps the dirty original open when Back discard is cancelled', async () => {
    store.overrideSelector(sharedSelectors.selectCurrentTemplateDirty, true);
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'shared-component::component-1');
    store.refreshState();
    modals.confirm.mockReturnValue(of(false));
    actions$.next(actions.closeSharedComponent());

    expect(await firstValueFrom(uiEffects.closeDocument$)).toEqual(sharedActions.empty());
    expect(modals.confirm).toHaveBeenCalledWith(
      'Discard unsaved changes and leave this Shared Component?',
      { confirmText: 'Discard', declineText: 'Keep editing' },
    );
  });

  it('falls back to the first usage page when a deep link has no return context', async () => {
    store.overrideSelector(selectors.selectCurrentSharedComponent, {
      ...component,
      usagePages: [{ id: 'first-usage', name: 'Homepage', cultureName: 'es-ES' }],
    });
    store.refreshState();
    actions$.next(actions.closeSharedComponent());

    expect(await firstValueFrom(uiEffects.closeDocument$)).toEqual(
      actions.openSharedComponentUsagePage({ pageId: 'first-usage', cultureName: 'es-ES' }),
    );
  });

  it('skips unavailable pages when choosing a where-used fallback', async () => {
    store.overrideSelector(selectors.selectCurrentSharedComponent, {
      ...component,
      usagePages: [
        { id: 'archived', name: 'Archived', status: 'Archived' },
        { id: null, name: 'Missing identity', status: 'Draft' },
        { id: 'available', name: 'Available', cultureName: 'it-IT', status: 'Draft' },
      ],
    });
    store.refreshState();
    actions$.next(actions.closeSharedComponent());

    expect(await firstValueFrom(uiEffects.closeDocument$)).toEqual(
      actions.openSharedComponentUsagePage({ pageId: 'available', cultureName: 'it-IT' }),
    );
  });

  it('requests full details when a shared instance is selected even if search metadata is cached', async () => {
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.refreshState();
    actions$.next(actions.editSectionAction({ sectionId: 'placement-1' }));

    expect(await firstValueFrom(uiEffects.requestDetailsForSelectedInstance$)).toEqual(
      actions.loadSharedComponentDetails({ componentId: component.id }),
    );
  });

  it('loads complete where-used metadata for the selected instance', async () => {
    const details = {
      ...component,
      usagePages: [{ id: 'homepage', name: 'Homepage', cultureName: 'en-US' }],
    };
    service.get.mockReturnValue(of(details));
    actions$.next(actions.loadSharedComponentDetails({ componentId: component.id }));

    expect(await firstValueFrom(dataEffects.loadDetails$)).toEqual(
      actions.loadSharedComponentDetailsSuccess({ component: details }),
    );
    expect(service.get).toHaveBeenCalledWith(component.id);
  });

  it('cancels a stale details request when another instance is selected', () => {
    const first$ = new Subject<typeof component>();
    const second$ = new Subject<typeof component>();
    const secondComponent = { ...component, id: 'component-2', name: 'Second component' };
    service.get.mockImplementation((componentId: string) => (componentId === component.id ? first$ : second$));
    const emitted: Action[] = [];
    const subscription = dataEffects.loadDetails$.subscribe((action) => emitted.push(action));

    actions$.next(actions.loadSharedComponentDetails({ componentId: component.id }));
    actions$.next(actions.loadSharedComponentDetails({ componentId: secondComponent.id }));
    first$.next(component);
    second$.next(secondComponent);

    expect(emitted).toEqual([actions.loadSharedComponentDetailsSuccess({ component: secondComponent })]);
    subscription.unsubscribe();
  });

  it('cancels an in-flight details request when the editor closes', () => {
    const response$ = new Subject<typeof component>();
    service.get.mockReturnValue(response$);
    const emitted: Action[] = [];
    const subscription = dataEffects.loadDetails$.subscribe((action) => emitted.push(action));

    actions$.next(actions.loadSharedComponentDetails({ componentId: component.id }));
    actions$.next(actions.clearSharedComponentDetails());
    response$.next(component);

    expect(emitted).toEqual([]);
    subscription.unsubscribe();
  });

  it('asks how to paste a copied component and preserves the chosen position', async () => {
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.refreshState();
    modals.show.mockReturnValue(of({ accept: true, mode: 'copy' }));
    actions$.next(
      actions.chooseSharedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 1,
        defaultMode: 'copy',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(
      actions.insertSharedComponent({
        componentId: component.id,
        mode: 'copy',
        insertIndex: 1,
      }),
    );
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({ name: component.name, defaultMode: 'copy', allowShared: true }),
      }),
    );
    expect(service.get).not.toHaveBeenCalled();
  });

  it('loads missing component metadata before showing the paste dialog', async () => {
    modals.show.mockReturnValue(of({ accept: false }));
    actions$.next(
      actions.chooseSharedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 0,
        defaultMode: 'shared',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(sharedActions.empty());
    expect(service.get).toHaveBeenCalledWith(component.id);
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({ name: component.name, defaultMode: 'shared', allowShared: true }),
      }),
    );
  });

  it('disables shared insertion in a shared document while keeping independent copy available', async () => {
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.overrideSelector(routingSelectors.selectSharedComponentIdParameter, 'component-original');
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'shared-component::component-original');
    store.refreshState();
    modals.show.mockReturnValue(of({ accept: true, mode: 'copy' }));
    actions$.next(
      actions.chooseSharedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 1,
        defaultMode: 'shared',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(
      actions.insertSharedComponent({
        componentId: component.id,
        mode: 'copy',
        insertIndex: 1,
      }),
    );
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({
          defaultMode: 'copy',
          allowShared: false,
          sharedComponentDisabledReason: expect.stringContaining('cannot be nested'),
        }),
      }),
    );
  });

  it('disables cross-store shared insertion while keeping independent copy available', async () => {
    service.storeId = 'store-2';
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.refreshState();
    modals.show.mockReturnValue(of({ accept: true, mode: 'copy' }));
    actions$.next(
      actions.chooseSharedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 0,
        defaultMode: 'shared',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(expect.objectContaining({ mode: 'copy' }));
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({
          defaultMode: 'copy',
          allowShared: false,
          sharedComponentDisabledReason: expect.stringContaining('same store'),
        }),
      }),
    );
  });

  it.each(['page', 'reload'])('refreshes cached shared content for a %s preview', async (type) => {
    const staleContent = createTemplate({ content: [createSection({ id: 'stale', type: 'stale-type' })] });
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.overrideSelector(selectors.selectSharedComponentContents, { [component.id]: staleContent });
    store.refreshState();
    actions$.next(actions.broadcastResolvedPreview({ msg: { type, template: raw } }));

    const preview = (await firstValueFrom(
      dataEffects.resolvePreview$.pipe(filter((action) => action.type === sharedActions.broadcastPreviewMessage.type)),
    )) as ReturnType<typeof sharedActions.broadcastPreviewMessage>;

    expect(service.get).toHaveBeenCalledWith(component.id);
    expect(service.getContent).toHaveBeenCalledWith(component.id);
    const previewTemplate = getPreviewTemplate(preview);
    expect(previewTemplate.content[1].type).toBe(sharedComponentContent.content[0].type);
    expect(previewTemplate.content[1].type).not.toBe('stale-type');
    expect(previewTemplate.content[1].id).not.toContain('stale');
  });

  it('inserts a strict reference into raw editor state', async () => {
    actions$.next(actions.insertSharedComponent({ componentId: component.id, mode: 'shared', insertIndex: 1 }));

    const emitted = await firstValueFrom(domainEffects.insert$.pipe(take(3), toArray()));
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    const preview = emitted.find((action) => action.type === actions.broadcastResolvedPreview.type) as ReturnType<
      typeof actions.broadcastResolvedPreview
    >;

    expect(update.template.content[1]).toEqual(
      expect.objectContaining({
        type: 'componentRef',
        componentRef: component.id,
      }),
    );
    expect(Object.keys(update.template.content[1]).sort()).toEqual(['componentRef', 'id', 'type']);
    expect(preview.msg).toEqual({ type: 'reload', template: update.template });
  });

  it('inserts an independent copy from fresh server content with regenerated ids', async () => {
    const staleContent = createTemplate({
      content: [createSection({ id: 'stale', blocks: [createSection({ id: 'stale-block' })] })],
    });
    store.overrideSelector(selectors.selectSharedComponentContents, { [component.id]: staleContent });
    store.refreshState();
    actions$.next(actions.insertSharedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));

    const emitted = await firstValueFrom(domainEffects.insert$.pipe(take(4), toArray()));
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    const copy = update.template.content[1];

    expect(service.getContent).toHaveBeenCalledWith(component.id);
    expect(isSharedComponentReference(copy)).toBe(false);
    expect(copy.type).toBe(sharedComponentContent.content[0].type);
    expect(copy.id).not.toBe('shared');
    expect(copy.id).not.toContain('stale');
    expect(copy.blocks[0].id).not.toBe('shared-block');
  });

  it('inserts a delayed independent copy into the latest template without losing unrelated edits', async () => {
    const response$ = new Subject<TemplateModel>();
    service.getContent.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.insert$.pipe(take(4), toArray()));

    actions$.next(actions.insertSharedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));
    const unrelated = createSection({ id: 'unrelated' });
    const latest = { ...raw, content: [...raw.content, unrelated] };
    store.overrideSelector(selectors.selectCurrentTemplateModel, latest);
    store.refreshState();
    response$.next(sharedComponentContent);
    response$.complete();

    const emitted = await emittedPromise;
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    expect(update.template.content).toContain(unrelated);
    expect(update.template.content[1].type).toBe(sharedComponentContent.content[0].type);
  });

  it('cancels a delayed insert after navigation instead of updating the new document', async () => {
    const response$ = new Subject<TemplateModel>();
    service.getContent.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.insert$);

    actions$.next(actions.insertSharedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'page-2');
    store.overrideSelector(selectors.selectCurrentTemplateModel, createTemplate());
    store.refreshState();
    response$.next(sharedComponentContent);
    response$.complete();

    const emitted = await emittedPromise;
    expect(emitted.type).toBe(sharedActions.showNotification.type);
    expect(emitted).toEqual(expect.objectContaining({ msgType: 'warning' }));
  });

  it('blocks a direct shared insert in a shared document but permits an independent copy', async () => {
    store.overrideSelector(routingSelectors.selectSharedComponentIdParameter, 'component-original');
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'shared-component::component-original');
    store.refreshState();
    actions$.next(actions.insertSharedComponent({ componentId: component.id, mode: 'shared', insertIndex: 1 }));

    const blocked = await firstValueFrom(domainEffects.insert$);
    expect(blocked.type).toBe(sharedActions.showNotification.type);
    expect(blocked).toEqual(expect.objectContaining({ message: expect.stringContaining('cannot be nested') }));

    actions$.next(actions.insertSharedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));
    const copied = (await firstValueFrom(
      domainEffects.insert$.pipe(filter((action) => action.type === actions.updateTemplateAction.type)),
    )) as ReturnType<typeof actions.updateTemplateAction>;
    expect(isSharedComponentReference(copied.template.content[1])).toBe(false);
  });

  it('creates a component, replaces the selected sections, and clears their selection state', async () => {
    actions$.next(actions.saveSelectionAsSharedComponent());

    const emitted = await firstValueFrom(domainEffects.saveSelection$.pipe(take(5), toArray()));
    const cache = emitted.find((action) => action.type === actions.cacheSharedComponent.type) as ReturnType<
      typeof actions.cacheSharedComponent
    >;
    const deselect = emitted.find((action) => action.type === actions.sectionStateChangedAction.type);
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;

    expect(service.create).toHaveBeenCalledWith(
      component.name,
      expect.objectContaining({
        content: [raw.content[0]],
      }),
    );
    expect(cache.addToSearchResults).toBe(true);
    expect(deselect).toEqual(
      actions.sectionStateChangedAction({
        sectionId: 'before',
        templateKey: 'page-1',
        state: { selected: false },
      }),
    );
    expect(isSharedComponentReference(update.template.content[0])).toBe(true);
    expect(update.template.content[1]).toBe(raw.content[1]);
  });

  it('applies a delayed create to the latest template without losing unrelated edits', async () => {
    const response$ = new Subject<typeof component>();
    service.create.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.saveSelection$.pipe(take(5), toArray()));

    actions$.next(actions.saveSelectionAsSharedComponent());
    const unrelated = createSection({ id: 'unrelated' });
    const latest = { ...raw, content: [...raw.content, unrelated] };
    store.overrideSelector(selectors.selectCurrentTemplateModel, latest);
    store.refreshState();
    response$.next(component);
    response$.complete();

    const emitted = await emittedPromise;
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    expect(update.template.content).toContain(unrelated);
    expect(isSharedComponentReference(update.template.content[0])).toBe(true);
  });

  it('does not create a component when create is granted without shared-component read access', () => {
    const appConfig = TestBed.inject(AppConfig) as unknown as { getValue: ReturnType<typeof vi.fn> };
    appConfig.getValue.mockImplementation((option: string) => option !== 'canInsertSharedComponents');
    const subscription = domainEffects.saveSelection$.subscribe();

    actions$.next(actions.saveSelectionAsSharedComponent());

    expect(modals.show).not.toHaveBeenCalled();
    expect(service.create).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });

  it('rejects non-adjacent sections before opening the naming dialog', async () => {
    const page = createTemplate({
      content: [createSection({ id: 'first' }), createSection({ id: 'middle' }), createSection({ id: 'last' })],
    });
    store.overrideSelector(selectors.selectCurrentTemplateModel, page);
    store.overrideSelector(selectors.selectCheckedItems, ['first', 'last']);
    store.refreshState();

    actions$.next(actions.saveSelectionAsSharedComponent());

    const result = await firstValueFrom(domainEffects.saveSelection$);
    expect(result).toEqual(
      sharedActions.showNotification({
        message: 'Select adjacent sections to create a Shared Component',
        msgType: 'warning',
        top: true,
      }),
    );
    expect(modals.show).not.toHaveBeenCalled();
    expect(service.create).not.toHaveBeenCalled();
  });

  it('detaches from fresh server content with regenerated ids', async () => {
    const staleContent = createTemplate({ content: [createSection({ id: 'stale' })] });
    store.overrideSelector(selectors.selectSharedComponentContents, { [component.id]: staleContent });
    store.refreshState();
    actions$.next(actions.detachSharedComponent({ sectionId: 'placement-1', componentId: component.id }));

    const emitted = await firstValueFrom(domainEffects.detach$.pipe(take(4), toArray()));
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    const preview = emitted.find((action) => action.type === actions.broadcastResolvedPreview.type) as ReturnType<
      typeof actions.broadcastResolvedPreview
    >;
    const detached = update.template.content[1];

    expect(service.getContent).toHaveBeenCalledWith(component.id);
    expect(isSharedComponentReference(detached)).toBe(false);
    expect(detached.id).not.toBe('shared');
    expect(detached.id).not.toContain('stale');
    expect(detached.blocks[0].id).not.toBe('shared-block');
    expect(preview.msg).toEqual({ type: 'reload', template: update.template });
  });

  it('applies a delayed detach to the latest template without losing unrelated edits', async () => {
    const response$ = new Subject<TemplateModel>();
    service.getContent.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.detach$.pipe(take(4), toArray()));

    actions$.next(actions.detachSharedComponent({ sectionId: 'placement-1', componentId: component.id }));
    const unrelated = createSection({ id: 'unrelated' });
    const latest = { ...raw, content: [unrelated, ...raw.content] };
    store.overrideSelector(selectors.selectCurrentTemplateModel, latest);
    store.refreshState();
    response$.next(sharedComponentContent);
    response$.complete();

    const emitted = await emittedPromise;
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    expect(update.template.content[0]).toBe(unrelated);
    expect(update.template.content.some((section) => isSharedComponentReference(section))).toBe(false);
  });

  it('refreshes where-used metadata after saving a page with a shared instance', async () => {
    const refreshed = {
      ...component,
      usageCount: 3,
      usagePages: [{ id: 'page-1', name: 'Homepage' }],
    };
    service.get.mockReturnValue(of(refreshed));
    actions$.next(actions.saveTemplateSuccess({ templateKey: 'page-1', template: raw }));

    expect(await firstValueFrom(dataEffects.refreshUsageAfterSave$)).toEqual(
      actions.cacheSharedComponent({ component: refreshed }),
    );
    expect(service.get).toHaveBeenCalledWith(component.id);
  });

  it('refreshes usage metadata after the last shared instance is removed and saved', async () => {
    const detached = createTemplate({ content: [createSection({ id: 'independent' })] });
    const refreshed = { ...component, usageCount: 0, usagePages: [] };
    service.get.mockReturnValue(of(refreshed));
    store.overrideSelector(selectors.selectSharedComponentUsageRefreshIdsByTemplate, {
      'page-1': [component.id],
    });
    store.refreshState();
    actions$.next(actions.saveTemplateSuccess({ templateKey: 'page-1', template: detached }));

    const emitted = await firstValueFrom(dataEffects.refreshUsageAfterSave$.pipe(take(2), toArray()));

    expect(emitted).toEqual([
      actions.cacheSharedComponent({ component: refreshed }),
      actions.clearSharedComponentUsageRefresh({ templateKey: 'page-1' }),
    ]);
    expect(service.get).toHaveBeenCalledWith(component.id);
  });

  it('keeps a pending usage refresh after a transient metadata failure', () => {
    const detached = createTemplate({ content: [createSection({ id: 'independent' })] });
    service.get.mockReturnValue(throwError(() => new Error('temporary failure')));
    store.overrideSelector(selectors.selectSharedComponentUsageRefreshIdsByTemplate, {
      'page-1': [component.id],
    });
    store.refreshState();
    const emitted: Action[] = [];
    const subscription = dataEffects.refreshUsageAfterSave$.subscribe((action) => emitted.push(action));

    actions$.next(actions.saveTemplateSuccess({ templateKey: 'page-1', template: detached }));

    expect(emitted).toEqual([]);
    expect(service.get).toHaveBeenCalledWith(component.id);
    subscription.unsubscribe();
  });

  it('finishes initial shared resolution before forwarding a later control message', () => {
    const component$ = new Subject<typeof component>();
    const content$ = new Subject<TemplateModel>();
    service.get.mockReturnValue(component$);
    service.getContent.mockReturnValue(content$);
    const emitted: Action[] = [];
    const subscription = dataEffects.resolvePreview$.subscribe((action) => emitted.push(action));

    actions$.next(actions.broadcastResolvedPreview({ msg: { type: 'page', template: raw } }));
    actions$.next(actions.broadcastResolvedPreview({ msg: { type: 'select', sectionId: 'before' } }));
    component$.next(component);
    component$.complete();
    content$.next(sharedComponentContent);
    content$.complete();

    const previewMessages = emitted
      .filter((action) => action.type === sharedActions.broadcastPreviewMessage.type)
      .map((action) => (action as ReturnType<typeof sharedActions.broadcastPreviewMessage>).msg);
    expect(previewMessages.map((message) => message.type)).toEqual(['page', 'select']);
    expect((previewMessages[0]['template'] as TemplateModel).content.some(isSharedComponentReference)).toBe(false);
    expect(previewMessages[0]['sharedComponentBoundaries']).toEqual([
      expect.objectContaining({ placementId: 'placement-1', componentRef: component.id }),
    ]);

    subscription.unsubscribe();
  });

  it('falls back to a full reload for structural messages when references expand the template', async () => {
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.overrideSelector(selectors.selectSharedComponentContents, { [component.id]: sharedComponentContent });
    store.refreshState();
    actions$.next(
      actions.broadcastResolvedPreview({
        msg: {
          type: 'swap',
          template: raw,
          currentIndex: 0,
          newIndex: 1,
          sectionIds: ['placement-1'],
        },
      }),
    );

    const preview = (await firstValueFrom(
      dataEffects.resolvePreview$.pipe(filter((action) => action.type === sharedActions.broadcastPreviewMessage.type)),
    )) as ReturnType<typeof sharedActions.broadcastPreviewMessage>;

    expect(preview.msg.type).toBe('reload');
    const previewTemplate = getPreviewTemplate(preview);
    expect(previewTemplate.content).toHaveLength(2);
    expect(isSharedComponentReference(previewTemplate.content[1])).toBe(false);
    expect(preview.msg['currentIndex']).toBeUndefined();
    expect(preview.msg['newIndex']).toBeUndefined();
    expect(preview.msg['sectionIds']).toBeUndefined();
  });

  it('preserves selection of an ordinary section while resolving references', async () => {
    store.overrideSelector(selectors.selectSharedComponents, { [component.id]: component });
    store.overrideSelector(selectors.selectSharedComponentContents, { [component.id]: sharedComponentContent });
    store.refreshState();
    actions$.next(
      actions.broadcastResolvedPreview({
        msg: {
          type: 'select',
          template: raw,
          sectionId: 'before',
          section: raw.content[0],
        },
      }),
    );

    const preview = (await firstValueFrom(
      dataEffects.resolvePreview$.pipe(filter((action) => action.type === sharedActions.broadcastPreviewMessage.type)),
    )) as ReturnType<typeof sharedActions.broadcastPreviewMessage>;

    expect(preview.msg.type).toBe('select');
    expect(preview.msg['sectionId']).toBe('before');
    expect(preview.msg['section']).toBe(raw.content[0]);
    expect(isSharedComponentReference(getPreviewTemplate(preview).content[1])).toBe(false);
    expect(preview.msg['sharedComponentBoundaries']).toEqual([
      expect.objectContaining({ label: 'Shared · Used on 2 pages' }),
    ]);
    expect(service.get).not.toHaveBeenCalled();
    expect(service.getContent).not.toHaveBeenCalled();
  });
});

function getPreviewTemplate(action: ReturnType<typeof sharedActions.broadcastPreviewMessage>): TemplateModel {
  return action.msg['template'] as TemplateModel;
}
