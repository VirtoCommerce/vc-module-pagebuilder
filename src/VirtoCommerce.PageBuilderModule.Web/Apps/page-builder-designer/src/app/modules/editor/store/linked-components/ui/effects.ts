import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import { filter, map, withLatestFrom } from 'rxjs';

import { isLinkedComponentReference } from '@editor/helpers';
import { AppConfig } from '@integration/services';
import { TemplateModel } from '@models/document';
import * as routingActions from '@shared/routing/actions';
import * as routingSelectors from '@shared/routing/selectors';
import * as sharedActions from '@shared/store/actions';

import * as actions from '../../actions';
import * as selectors from '../../selectors';
import { BuilderState } from '../../state';

@Injectable({ providedIn: 'root' })
export class LinkedComponentsUiEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store<BuilderState>);
  private readonly appConfig = inject(AppConfig);

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
      ),
      map(([, queryParams, component]) => {
        const returnPageId =
          String(queryParams?.[RETURN_PAGE_QUERY_PARAM] || '').trim() || component?.usagePages?.[0]?.id;
        return returnPageId
          ? actions.openLinkedComponentUsagePage({ pageId: returnPageId })
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
      }),
    ),
  );

  openUsagePage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.openLinkedComponentUsagePage),
      map(({ pageId }) =>
        routingActions.go({
          path: ['/pages'],
          queryParams: {
            type: 'pages',
            groupId: pageId,
            path: undefined,
            parent: undefined,
            linkedComponentId: undefined,
            [RETURN_PAGE_QUERY_PARAM]: undefined,
          },
        }),
      ),
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
}

const RETURN_PAGE_QUERY_PARAM = 'linkedComponentReturnPageId';

function getDetailsRequest(template: TemplateModel | null, sectionId: string): Action {
  const section = template?.content.find((item) => item.id === sectionId);
  return isLinkedComponentReference(section)
    ? actions.loadLinkedComponentDetails({ componentId: section.componentRef })
    : actions.clearLinkedComponentDetails();
}
