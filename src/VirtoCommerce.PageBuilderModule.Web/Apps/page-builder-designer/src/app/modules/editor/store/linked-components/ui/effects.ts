import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import { filter, map, of, switchMap, withLatestFrom } from 'rxjs';

import { ModalService } from '@core/services';
import { canOpenLinkedComponentUsagePage, isLinkedComponentReference } from '@editor/helpers';
import { AppConfig } from '@integration/services';
import { TemplateModel } from '@models/document';
import * as routingActions from '@shared/routing/actions';
import * as routingSelectors from '@shared/routing/selectors';
import * as sharedActions from '@shared/store/actions';
import * as sharedSelectors from '@shared/store/selectors';

import * as actions from '../../actions';
import * as selectors from '../../selectors';
import { BuilderState } from '../../state';

@Injectable({ providedIn: 'root' })
export class LinkedComponentsUiEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store<BuilderState>);
  private readonly appConfig = inject(AppConfig);
  private readonly modals = inject(ModalService);

  contextAction$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.executeContextMenuAction),
      filter(({ action }) =>
        ['save-as-linked-component', 'edit-linked-component', 'detach-linked-component'].includes(action),
      ),
      map(({ action, section }) => {
        if (action === 'save-as-linked-component') {
          return actions.saveSelectionAsLinkedComponent();
        }
        if (!section || !isLinkedComponentReference(section)) {
          return sharedActions.empty();
        }
        return action === 'edit-linked-component'
          ? actions.openLinkedComponent({ componentId: section.componentRef })
          : actions.detachLinkedComponent({ sectionId: section.id, componentId: section.componentRef });
      }),
    ),
  );

  openDocument$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.openLinkedComponent),
      filter(() => this.canInsertLinkedComponents()),
      withLatestFrom(this.store.select(routingSelectors.selectGroupIdParameter)),
      map(([{ componentId }, returnPageId]) =>
        routingActions.go({
          path: ['/pages'],
          queryParams: {
            linkedComponentId: componentId,
            type: 'linked-components',
            path: componentId,
            groupId: undefined,
            parent: undefined,
            [RETURN_PAGE_QUERY_PARAM]: returnPageId || undefined,
          },
        }),
      ),
    ),
  );

  closeDocument$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.closeLinkedComponent),
      withLatestFrom(
        this.store.select(routingSelectors.selectQueryParams),
        this.store.select(selectors.selectCurrentLinkedComponent),
        this.store.select(sharedSelectors.selectCurrentTemplateDirty),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
      ),
      switchMap(([, queryParams, component, isDirty, templateKey]) => {
        const discardLinkedDocument = shouldDiscardLinkedDocument(isDirty, templateKey);
        const requestedPageId = String(queryParams?.[RETURN_PAGE_QUERY_PARAM] || '').trim();
        const requestedPage = component?.usagePages?.find((page) => page.id === requestedPageId);
        const fallbackPage = component?.usagePages?.find(canOpenLinkedComponentUsagePage);
        const targetPage =
          requestedPageId && (!requestedPage || canOpenLinkedComponentUsagePage(requestedPage))
            ? requestedPage || {
                id: requestedPageId,
                cultureName: normalizeCultureName(queryParams?.['cultureName']),
              }
            : fallbackPage;

        const navigation = targetPage
          ? actions.openLinkedComponentUsagePage({
              pageId: targetPage.id,
              cultureName: targetPage.cultureName,
            })
          : routingActions.go({
              path: ['/pages'],
              queryParams: {
                type: undefined,
                path: undefined,
                groupId: undefined,
                parent: undefined,
                linkedComponentId: undefined,
                [RETURN_PAGE_QUERY_PARAM]: undefined,
              },
            });

        return this.confirmDiscard(discardLinkedDocument).pipe(
          switchMap((canLeave) =>
            canLeave ? [...getDiscardActions(discardLinkedDocument, templateKey), navigation] : [sharedActions.empty()],
          ),
        );
      }),
    ),
  );

  openUsagePage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.openLinkedComponentUsagePage),
      withLatestFrom(
        this.store.select(selectors.selectCurrentLinkedComponent),
        this.store.select(sharedSelectors.selectCurrentTemplateDirty),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
      ),
      filter(([{ pageId }, component]) => {
        const usagePage = component?.usagePages?.find((page) => page.id === pageId);
        return !usagePage || canOpenLinkedComponentUsagePage(usagePage);
      }),
      switchMap(([{ pageId, cultureName }, , isDirty, templateKey]) => {
        const discardLinkedDocument = shouldDiscardLinkedDocument(isDirty, templateKey);
        return this.confirmDiscard(discardLinkedDocument).pipe(
          switchMap((canLeave) =>
            canLeave
              ? [
                  ...getDiscardActions(discardLinkedDocument, templateKey),
                  routingActions.go({
                    path: ['/pages'],
                    queryParams: {
                      type: 'pages',
                      groupId: pageId,
                      cultureName: normalizeCultureName(cultureName),
                      path: undefined,
                      parent: undefined,
                      linkedComponentId: undefined,
                      [RETURN_PAGE_QUERY_PARAM]: undefined,
                    },
                  }),
                ]
              : [sharedActions.empty()],
          ),
        );
      }),
    ),
  );

  requestDetailsForSelectedInstance$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.editSectionAction, sharedActions.selectSection),
      withLatestFrom(this.store.select(selectors.selectCurrentTemplateModel)),
      map(([{ sectionId }, template]) => getDetailsRequest(template, sectionId)),
    ),
  );

  requestDetailsAfterTemplateLoad$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.loadTemplateModelSuccess),
      withLatestFrom(this.store.select(routingSelectors.selectSectionIdParameter)),
      map(([{ template }, sectionId]) => getDetailsRequest(template, sectionId)),
    ),
  );

  clearDetailsWhenEditorCloses$ = createEffect(() =>
    this.actions$.pipe(
      ofType(
        actions.closeEditItemPanel,
        actions.editBlockAction,
        actions.openLinkedComponent,
        actions.closeLinkedComponent,
      ),
      map(() => actions.clearLinkedComponentDetails()),
    ),
  );

  private canInsertLinkedComponents(): boolean {
    return this.appConfig.getValue('canInsertLinkedComponents') === true;
  }

  private confirmDiscard(isDirty: boolean) {
    return isDirty
      ? this.modals.confirm('Discard unsaved changes and leave this Shared Component?', {
          confirmText: 'Discard',
          declineText: 'Keep editing',
        })
      : of(true);
  }
}

const RETURN_PAGE_QUERY_PARAM = 'linkedComponentReturnPageId';
const LINKED_COMPONENT_TEMPLATE_PREFIX = 'linked-component::';

function normalizeCultureName(value: unknown): string | undefined {
  const cultureName = typeof value === 'string' ? value.trim() : '';
  return cultureName || undefined;
}

function getDetailsRequest(template: TemplateModel | null, sectionId: string): Action {
  const section = template?.content.find((item) => item.id === sectionId);
  return isLinkedComponentReference(section)
    ? actions.loadLinkedComponentDetails({ componentId: section.componentRef })
    : actions.clearLinkedComponentDetails();
}

function getDiscardActions(shouldDiscard: boolean, templateKey: string): Action[] {
  return shouldDiscard
    ? [
        sharedActions.setRootDirtyState({ templateKey, dirty: false }),
        actions.discardLinkedComponentChanges({ templateKey }),
      ]
    : [];
}

function shouldDiscardLinkedDocument(isDirty: boolean, templateKey: string): boolean {
  return isDirty && templateKey.startsWith(LINKED_COMPONENT_TEMPLATE_PREFIX);
}
