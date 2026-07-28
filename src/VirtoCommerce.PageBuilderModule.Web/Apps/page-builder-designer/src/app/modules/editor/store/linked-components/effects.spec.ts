import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { Action } from '@ngrx/store';
import { filter, firstValueFrom, of, ReplaySubject, Subject, take, toArray } from 'rxjs';

import { createSection, createTemplate } from '@app/testing';
import { ModalService } from '@core/services';
import { createLinkedComponentReference, isLinkedComponentReference } from '@editor/helpers';
import { TemplateModel } from '@models/document';
import { LinkedComponentsService } from '@editor/services';
import { AppConfig } from '@integration/services';
import * as routingSelectors from '@shared/routing/selectors';
import * as routingActions from '@shared/routing/actions';
import * as sharedActions from '@shared/store/actions';

import * as actions from '../actions';
import * as selectors from '../selectors';
import { LinkedComponentsDataEffects } from './data/effects';
import { LinkedComponentsDomainEffects } from './domain/effects';
import { LinkedComponentsUiEffects } from './ui/effects';

describe('Linked Component effects', () => {
  let dataEffects: LinkedComponentsDataEffects;
  let domainEffects: LinkedComponentsDomainEffects;
  let uiEffects: LinkedComponentsUiEffects;
  let actions$: ReplaySubject<Action>;
  let store: MockStore;
  let service: {
    storeId: string;
    search: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    getContent: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
  };
  let modals: { show: ReturnType<typeof vi.fn> };

  const component = {
    id: 'component-1',
    storeId: 'store-1',
    name: 'Shared hero',
    usageCount: 2,
    usagePages: [],
  };
  const linkedContent = createTemplate({
    content: [createSection({ id: 'shared', blocks: [createSection({ id: 'shared-block' })] })],
  });
  const raw = createTemplate({
    content: [createSection({ id: 'before' }), createLinkedComponentReference(component.id, 'placement-1')],
  });

  beforeEach(() => {
    actions$ = new ReplaySubject<Action>(1);
    service = {
      storeId: 'store-1',
      search: vi.fn().mockReturnValue(of({ totalCount: 1, results: [component] })),
      get: vi.fn().mockReturnValue(of(component)),
      getContent: vi.fn().mockReturnValue(of(linkedContent)),
      create: vi.fn().mockReturnValue(of(component)),
    };
    modals = { show: vi.fn().mockReturnValue(of({ accept: true, name: component.name })) };

    TestBed.configureTestingModule({
      providers: [
        LinkedComponentsDataEffects,
        LinkedComponentsDomainEffects,
        LinkedComponentsUiEffects,
        provideMockActions(() => actions$),
        provideMockStore({
          selectors: [
            { selector: selectors.selectLinkedComponents, value: {} },
            { selector: selectors.selectLinkedComponentContents, value: {} },
            { selector: selectors.selectLinkedComponentErrors, value: {} },
            { selector: selectors.selectCurrentTemplateModel, value: raw },
            { selector: selectors.selectCurrentLinkedComponent, value: null },
            { selector: selectors.selectCheckedItems, value: ['before'] },
            { selector: routingSelectors.selectTemplateKeyParameter, value: 'page-1' },
            { selector: routingSelectors.selectLinkedComponentIdParameter, value: '' },
            { selector: routingSelectors.selectGroupIdParameter, value: 'page-1' },
            { selector: routingSelectors.selectSectionIdParameter, value: '' },
            { selector: routingSelectors.selectQueryParams, value: {} },
          ],
        }),
        { provide: LinkedComponentsService, useValue: service },
        { provide: ModalService, useValue: modals },
        { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(true) } },
      ],
    });

    dataEffects = TestBed.inject(LinkedComponentsDataEffects);
    domainEffects = TestBed.inject(LinkedComponentsDomainEffects);
    uiEffects = TestBed.inject(LinkedComponentsUiEffects);
    store = TestBed.inject(MockStore);
  });

  afterEach(() => store.resetSelectors());

  it('loads a bounded next page and marks the result for append', async () => {
    actions$.next(actions.searchLinkedComponents({ keyword: 'hero', skip: 20 }));

    expect(await firstValueFrom(dataEffects.search$)).toEqual(
      actions.searchLinkedComponentsSuccess({
        keyword: 'hero',
        result: { totalCount: 1, results: [component] },
        append: true,
      }),
    );
    expect(service.search).toHaveBeenCalledWith('hero', 20, 20);
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
    expect(isLinkedComponentReference(previewTemplate.content[1])).toBe(false);
    expect(previewTemplate.content[1].id).not.toBe('shared');
    expect(isLinkedComponentReference(raw.content[1])).toBe(true);
    expect(preview.msg['linkedComponentBoundaries']).toEqual([
      expect.objectContaining({
        placementId: 'placement-1',
        componentRef: component.id,
        name: component.name,
        usageCount: component.usageCount,
      }),
    ]);
  });

  it('opens a usage page and clears the linked-component route state', async () => {
    actions$.next(actions.openLinkedComponentUsagePage({ pageId: 'page-group-1' }));

    expect(await firstValueFrom(uiEffects.openUsagePage$)).toEqual(
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          type: 'pages',
          groupId: 'page-group-1',
          path: undefined,
          parent: undefined,
          linkedComponentId: undefined,
          linkedComponentReturnPageId: undefined,
        },
      }),
    );
  });

  it('remembers the source page when opening the original component', async () => {
    actions$.next(actions.openLinkedComponent({ componentId: component.id }));

    expect(await firstValueFrom(uiEffects.openDocument$)).toEqual(
      routingActions.go({
        path: ['/pages'],
        queryParams: {
          linkedComponentId: component.id,
          type: 'linked-components',
          path: component.id,
          groupId: undefined,
          parent: undefined,
          linkedComponentReturnPageId: 'page-1',
        },
      }),
    );
  });

  it('returns to the source page after a linked-component editor reload', async () => {
    store.overrideSelector(routingSelectors.selectQueryParams, {
      linkedComponentReturnPageId: 'source-page',
    });
    store.refreshState();
    actions$.next(actions.closeLinkedComponent());

    expect(await firstValueFrom(uiEffects.closeDocument$)).toEqual(
      actions.openLinkedComponentUsagePage({ pageId: 'source-page' }),
    );
  });

  it('falls back to the first usage page when a deep link has no return context', async () => {
    store.overrideSelector(selectors.selectCurrentLinkedComponent, {
      ...component,
      usagePages: [{ id: 'first-usage', name: 'Homepage' }],
    });
    store.refreshState();
    actions$.next(actions.closeLinkedComponent());

    expect(await firstValueFrom(uiEffects.closeDocument$)).toEqual(
      actions.openLinkedComponentUsagePage({ pageId: 'first-usage' }),
    );
  });

  it('requests full details when a linked instance is selected even if search metadata is cached', async () => {
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.refreshState();
    actions$.next(actions.editSectionAction({ sectionId: 'placement-1' }));

    expect(await firstValueFrom(uiEffects.requestDetailsForSelectedInstance$)).toEqual(
      actions.loadLinkedComponentDetails({ componentId: component.id }),
    );
  });

  it('loads complete where-used metadata for the selected instance', async () => {
    const details = {
      ...component,
      usagePages: [{ id: 'homepage', name: 'Homepage', cultureName: 'en-US' }],
    };
    service.get.mockReturnValue(of(details));
    actions$.next(actions.loadLinkedComponentDetails({ componentId: component.id }));

    expect(await firstValueFrom(dataEffects.loadDetails$)).toEqual(
      actions.loadLinkedComponentDetailsSuccess({ component: details }),
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

    actions$.next(actions.loadLinkedComponentDetails({ componentId: component.id }));
    actions$.next(actions.loadLinkedComponentDetails({ componentId: secondComponent.id }));
    first$.next(component);
    second$.next(secondComponent);

    expect(emitted).toEqual([actions.loadLinkedComponentDetailsSuccess({ component: secondComponent })]);
    subscription.unsubscribe();
  });

  it('cancels an in-flight details request when the editor closes', () => {
    const response$ = new Subject<typeof component>();
    service.get.mockReturnValue(response$);
    const emitted: Action[] = [];
    const subscription = dataEffects.loadDetails$.subscribe((action) => emitted.push(action));

    actions$.next(actions.loadLinkedComponentDetails({ componentId: component.id }));
    actions$.next(actions.clearLinkedComponentDetails());
    response$.next(component);

    expect(emitted).toEqual([]);
    subscription.unsubscribe();
  });

  it('asks how to paste a copied component and preserves the chosen position', async () => {
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.refreshState();
    modals.show.mockReturnValue(of({ accept: true, mode: 'copy' }));
    actions$.next(
      actions.chooseLinkedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 1,
        defaultMode: 'copy',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(
      actions.insertLinkedComponent({
        componentId: component.id,
        mode: 'copy',
        insertIndex: 1,
      }),
    );
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({ name: component.name, defaultMode: 'copy', allowLinked: true }),
      }),
    );
    expect(service.get).not.toHaveBeenCalled();
  });

  it('loads missing component metadata before showing the paste dialog', async () => {
    modals.show.mockReturnValue(of({ accept: false }));
    actions$.next(
      actions.chooseLinkedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 0,
        defaultMode: 'linked',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(sharedActions.empty());
    expect(service.get).toHaveBeenCalledWith(component.id);
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({ name: component.name, defaultMode: 'linked', allowLinked: true }),
      }),
    );
  });

  it('disables linked insertion in a linked document while keeping independent copy available', async () => {
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.overrideSelector(routingSelectors.selectLinkedComponentIdParameter, 'component-original');
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'linked-component::component-original');
    store.refreshState();
    modals.show.mockReturnValue(of({ accept: true, mode: 'copy' }));
    actions$.next(
      actions.chooseLinkedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 1,
        defaultMode: 'linked',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(
      actions.insertLinkedComponent({
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
          allowLinked: false,
          linkedDisabledReason: expect.stringContaining('cannot be nested'),
        }),
      }),
    );
  });

  it('disables cross-store linked insertion while keeping independent copy available', async () => {
    service.storeId = 'store-2';
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.refreshState();
    modals.show.mockReturnValue(of({ accept: true, mode: 'copy' }));
    actions$.next(
      actions.chooseLinkedComponentInsertionMode({
        componentId: component.id,
        insertIndex: 0,
        defaultMode: 'linked',
      }),
    );

    expect(await firstValueFrom(domainEffects.chooseInsertionMode$)).toEqual(expect.objectContaining({ mode: 'copy' }));
    expect(modals.show).toHaveBeenCalledWith(
      expect.any(Function),
      expect.objectContaining({
        data: expect.objectContaining({
          defaultMode: 'copy',
          allowLinked: false,
          linkedDisabledReason: expect.stringContaining('same store'),
        }),
      }),
    );
  });

  it.each(['page', 'reload'])('refreshes cached linked content for a %s preview', async (type) => {
    const staleContent = createTemplate({ content: [createSection({ id: 'stale', type: 'stale-type' })] });
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.overrideSelector(selectors.selectLinkedComponentContents, { [component.id]: staleContent });
    store.refreshState();
    actions$.next(actions.broadcastResolvedPreview({ msg: { type, template: raw } }));

    const preview = (await firstValueFrom(
      dataEffects.resolvePreview$.pipe(filter((action) => action.type === sharedActions.broadcastPreviewMessage.type)),
    )) as ReturnType<typeof sharedActions.broadcastPreviewMessage>;

    expect(service.get).toHaveBeenCalledWith(component.id);
    expect(service.getContent).toHaveBeenCalledWith(component.id);
    const previewTemplate = getPreviewTemplate(preview);
    expect(previewTemplate.content[1].type).toBe(linkedContent.content[0].type);
    expect(previewTemplate.content[1].type).not.toBe('stale-type');
    expect(previewTemplate.content[1].id).not.toContain('stale');
  });

  it('inserts a strict reference into raw editor state', async () => {
    actions$.next(actions.insertLinkedComponent({ componentId: component.id, mode: 'linked', insertIndex: 1 }));

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
    store.overrideSelector(selectors.selectLinkedComponentContents, { [component.id]: staleContent });
    store.refreshState();
    actions$.next(actions.insertLinkedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));

    const emitted = await firstValueFrom(domainEffects.insert$.pipe(take(4), toArray()));
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    const copy = update.template.content[1];

    expect(service.getContent).toHaveBeenCalledWith(component.id);
    expect(isLinkedComponentReference(copy)).toBe(false);
    expect(copy.type).toBe(linkedContent.content[0].type);
    expect(copy.id).not.toBe('shared');
    expect(copy.id).not.toContain('stale');
    expect(copy.blocks[0].id).not.toBe('shared-block');
  });

  it('inserts a delayed independent copy into the latest template without losing unrelated edits', async () => {
    const response$ = new Subject<TemplateModel>();
    service.getContent.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.insert$.pipe(take(4), toArray()));

    actions$.next(actions.insertLinkedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));
    const unrelated = createSection({ id: 'unrelated' });
    const latest = { ...raw, content: [...raw.content, unrelated] };
    store.overrideSelector(selectors.selectCurrentTemplateModel, latest);
    store.refreshState();
    response$.next(linkedContent);
    response$.complete();

    const emitted = await emittedPromise;
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    expect(update.template.content).toContain(unrelated);
    expect(update.template.content[1].type).toBe(linkedContent.content[0].type);
  });

  it('cancels a delayed insert after navigation instead of updating the new document', async () => {
    const response$ = new Subject<TemplateModel>();
    service.getContent.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.insert$);

    actions$.next(actions.insertLinkedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'page-2');
    store.overrideSelector(selectors.selectCurrentTemplateModel, createTemplate());
    store.refreshState();
    response$.next(linkedContent);
    response$.complete();

    const emitted = await emittedPromise;
    expect(emitted.type).toBe(sharedActions.showNotification.type);
    expect(emitted).toEqual(expect.objectContaining({ msgType: 'warning' }));
  });

  it('blocks a direct linked insert in a linked document but permits an independent copy', async () => {
    store.overrideSelector(routingSelectors.selectLinkedComponentIdParameter, 'component-original');
    store.overrideSelector(routingSelectors.selectTemplateKeyParameter, 'linked-component::component-original');
    store.refreshState();
    actions$.next(actions.insertLinkedComponent({ componentId: component.id, mode: 'linked', insertIndex: 1 }));

    const blocked = await firstValueFrom(domainEffects.insert$);
    expect(blocked.type).toBe(sharedActions.showNotification.type);
    expect(blocked).toEqual(expect.objectContaining({ message: expect.stringContaining('cannot be nested') }));

    actions$.next(actions.insertLinkedComponent({ componentId: component.id, mode: 'copy', insertIndex: 1 }));
    const copied = (await firstValueFrom(
      domainEffects.insert$.pipe(filter((action) => action.type === actions.updateTemplateAction.type)),
    )) as ReturnType<typeof actions.updateTemplateAction>;
    expect(isLinkedComponentReference(copied.template.content[1])).toBe(false);
  });

  it('creates a component from selected sections and replaces them with one marker', async () => {
    actions$.next(actions.saveSelectionAsLinkedComponent());

    const emitted = await firstValueFrom(domainEffects.saveSelection$.pipe(take(4), toArray()));
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;

    expect(service.create).toHaveBeenCalledWith(
      component.name,
      expect.objectContaining({
        content: [raw.content[0]],
      }),
    );
    expect(isLinkedComponentReference(update.template.content[0])).toBe(true);
    expect(update.template.content[1]).toBe(raw.content[1]);
  });

  it('applies a delayed create to the latest template without losing unrelated edits', async () => {
    const response$ = new Subject<typeof component>();
    service.create.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.saveSelection$.pipe(take(4), toArray()));

    actions$.next(actions.saveSelectionAsLinkedComponent());
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
    expect(isLinkedComponentReference(update.template.content[0])).toBe(true);
  });

  it('does not create a component when create is granted without linked-component read access', () => {
    const appConfig = TestBed.inject(AppConfig) as unknown as { getValue: ReturnType<typeof vi.fn> };
    appConfig.getValue.mockImplementation((option: string) => option !== 'canInsertLinkedComponents');
    const subscription = domainEffects.saveSelection$.subscribe();

    actions$.next(actions.saveSelectionAsLinkedComponent());

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

    actions$.next(actions.saveSelectionAsLinkedComponent());

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
    store.overrideSelector(selectors.selectLinkedComponentContents, { [component.id]: staleContent });
    store.refreshState();
    actions$.next(actions.detachLinkedComponent({ sectionId: 'placement-1', componentId: component.id }));

    const emitted = await firstValueFrom(domainEffects.detach$.pipe(take(4), toArray()));
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    const preview = emitted.find((action) => action.type === actions.broadcastResolvedPreview.type) as ReturnType<
      typeof actions.broadcastResolvedPreview
    >;
    const detached = update.template.content[1];

    expect(service.getContent).toHaveBeenCalledWith(component.id);
    expect(isLinkedComponentReference(detached)).toBe(false);
    expect(detached.id).not.toBe('shared');
    expect(detached.id).not.toContain('stale');
    expect(detached.blocks[0].id).not.toBe('shared-block');
    expect(preview.msg).toEqual({ type: 'reload', template: update.template });
  });

  it('applies a delayed detach to the latest template without losing unrelated edits', async () => {
    const response$ = new Subject<TemplateModel>();
    service.getContent.mockReturnValue(response$);
    const emittedPromise = firstValueFrom(domainEffects.detach$.pipe(take(4), toArray()));

    actions$.next(actions.detachLinkedComponent({ sectionId: 'placement-1', componentId: component.id }));
    const unrelated = createSection({ id: 'unrelated' });
    const latest = { ...raw, content: [unrelated, ...raw.content] };
    store.overrideSelector(selectors.selectCurrentTemplateModel, latest);
    store.refreshState();
    response$.next(linkedContent);
    response$.complete();

    const emitted = await emittedPromise;
    const update = emitted.find((action) => action.type === actions.updateTemplateAction.type) as ReturnType<
      typeof actions.updateTemplateAction
    >;
    expect(update.template.content[0]).toBe(unrelated);
    expect(update.template.content.some((section) => isLinkedComponentReference(section))).toBe(false);
  });

  it('refreshes where-used metadata after saving a page with a linked instance', async () => {
    const refreshed = {
      ...component,
      usageCount: 3,
      usagePages: [{ id: 'page-1', name: 'Homepage' }],
    };
    service.get.mockReturnValue(of(refreshed));
    actions$.next(actions.saveTemplateSuccess({ templateKey: 'page-1', template: raw }));

    expect(await firstValueFrom(dataEffects.refreshUsageAfterSave$)).toEqual(
      actions.cacheLinkedComponent({ component: refreshed }),
    );
    expect(service.get).toHaveBeenCalledWith(component.id);
  });

  it('finishes initial linked resolution before forwarding a later control message', () => {
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
    content$.next(linkedContent);
    content$.complete();

    const previewMessages = emitted
      .filter((action) => action.type === sharedActions.broadcastPreviewMessage.type)
      .map((action) => (action as ReturnType<typeof sharedActions.broadcastPreviewMessage>).msg);
    expect(previewMessages.map((message) => message.type)).toEqual(['page', 'select']);
    expect((previewMessages[0]['template'] as TemplateModel).content.some(isLinkedComponentReference)).toBe(false);
    expect(previewMessages[0]['linkedComponentBoundaries']).toEqual([
      expect.objectContaining({ placementId: 'placement-1', componentRef: component.id }),
    ]);

    subscription.unsubscribe();
  });

  it('falls back to a full reload for structural messages when references expand the template', async () => {
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.overrideSelector(selectors.selectLinkedComponentContents, { [component.id]: linkedContent });
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
    expect(isLinkedComponentReference(previewTemplate.content[1])).toBe(false);
    expect(preview.msg['currentIndex']).toBeUndefined();
    expect(preview.msg['newIndex']).toBeUndefined();
    expect(preview.msg['sectionIds']).toBeUndefined();
  });

  it('preserves selection of an ordinary section while resolving references', async () => {
    store.overrideSelector(selectors.selectLinkedComponents, { [component.id]: component });
    store.overrideSelector(selectors.selectLinkedComponentContents, { [component.id]: linkedContent });
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
    expect(isLinkedComponentReference(getPreviewTemplate(preview).content[1])).toBe(false);
    expect(preview.msg['linkedComponentBoundaries']).toEqual([
      expect.objectContaining({ name: component.name, usageCount: component.usageCount }),
    ]);
    expect(service.get).not.toHaveBeenCalled();
    expect(service.getContent).not.toHaveBeenCalled();
  });
});

function getPreviewTemplate(action: ReturnType<typeof sharedActions.broadcastPreviewMessage>): TemplateModel {
  return action.msg['template'] as TemplateModel;
}
