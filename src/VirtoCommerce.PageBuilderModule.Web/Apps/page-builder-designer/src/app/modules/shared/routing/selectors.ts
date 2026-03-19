import { createSelector } from '@ngrx/store';
import { BuilderState } from './state';
import { EditorModuleInfo } from '@models/modules';

import * as helpers from '@core/helpers';

export const selectFeature = (state: BuilderState) => state.router;

/*

used parameters
    common
        template - current template
        in - template group (pages, blog etc)
    theme settings
        groups - opened groups
        mode - settings from route config (edit-settings, or edit-section and so on, see <module-name>-routes.module.ts)
        preset - name of preset under preview
    template editor
        sectionId - section id
        blockId - block id
        settingsType - settings under preview
        preview-mode - preview mode, i.e. mobile, tablet, fullscreen, empty value = desktop

 */

// export const {
//     selectCurrentRoute,   // select the current route
//     selectFragment,       // select the current route fragment
//     selectQueryParams,    // select the current route query params
//     selectQueryParam,     // factory function to select a query param
//     selectRouteParams,    // select the current route params
//     selectRouteParam,     // factory function to select a route param
//     selectRouteData,      // select the current route data
//     selectUrl,            // select the current url
// } = getSelectors();

// function selectQueryParam(paramName) {

//     return createSelector(
//         selectRouterFeature,
//         route => route?.state?.queryParams[paramName]
//     )
// }

// function selectRouteParam(paramName) {
//     return createSelector(
//         selectRouterFeature,
//         route => route?.state?.params[paramName]
//     )
// }

export const selectQueryParams = createSelector(
    selectFeature,
    route => route?.state.queryParams
);

export const selectPathParams = createSelector(
    selectFeature,
    route => route?.state.params
);

export const selectDataParams = createSelector(
    selectFeature,
    route => route?.state?.data || {}
);

export const selectGroupsParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['groups'] || ''
);


export const getModeName = createSelector(
    selectDataParams,
    data => data['mode'] || null
);

export const selectPresetParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['preset'] || ''
);

export const selectPathParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['path'] || ''
);

export const selectGroupIdParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['groupId'] || ''
);

export const selectParentTemplateParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['parent'] || ''
);

export const selectTypeParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['type'] || ''
);

export const selectTemplateKeyParameter = createSelector(
    selectTypeParameter,
    selectPathParameter,
    selectGroupIdParameter,
    (type, path, groupId) => {
        if (!!type) {
            if (groupId) {
                return `${type}::${groupId}`
            }
            else {
                return `${type}::${path}`;
            }
        } else {
            if (groupId) {
                return groupId;
            }
            else {
                return path;
            }
        }
    }
);

export const selectSectionIdParameter = createSelector(
    selectPathParams,
    params => params && params['sectionId'] || ''
);

export const selectInsertIndexParameter = createSelector(
    selectPathParams,
    params => params && helpers.coreHelpers.parseIntOrDefault(params['insertIndex'], -1)
);

export const selectSettingsTypeParameter = createSelector(
    selectPathParams,
    getModeName,
    (params, mode) => mode === EditorModuleInfo.mode.editSettings ? (params && params['settingsType'] || '') : null
);

export const selectBlockIdParameter = createSelector(
    selectPathParams,
    params => params && params['blockId'] || ''
);

export const selectPreviewModeParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['preview-mode'] || ''
);

export const isFullscreenPreviewMode = createSelector(
    selectPreviewModeParameter,
    value => value === 'fullscreen'
);

export const isDesktop50 = createSelector(
    selectPreviewModeParameter,
    value => value === 'desktop-50'
);

export const isPresetPreviewMode = createSelector(
    selectPresetParameter,
    preset => !!preset
);

export const isEmpty = createSelector(
    selectFeature,
    state => state.state.isEmpty
);

export const selectUrl = createSelector(
    selectFeature,
    route => route?.state.url
);

export const selectPath = createSelector(
    selectUrl,
    url => url?.split('?')[0]
);

// export const getId = createSelector(
//     selectRouteParam('id'),
//     id => appHelpers.getNumber(id));

// export const getTypeId = createSelector(
//     selectRouteParam('typeId'),
//     id => appHelpers.getNumber(id));

// export const getTaskId = createSelector(
//     selectRouteParam('taskId'),
//     id => appHelpers.getNumber(id));

// export const getPage = createSelector(
//     selectQueryParam('page'),
//     page => appHelpers.getNumber(page, 1));

// export const getFilter = createSelector(
//     selectQueryParam('filter'),
//     filter => appHelpers.getNumber(filter)
// );

// export const getAtmId = createSelector(
//     selectQueryParam('atmId'),
//     atmId => appHelpers.getNumber(atmId)
// );

// export const getGroup = createSelector(
//     selectQueryParam('group'),
//     group => appHelpers.getNumber(group)
// );

// export const getQuery = createSelector(
//     selectQueryParam('query'),
//     query => query || null
// );

// export const getRegistratorId = createSelector(
//     selectQueryParam('registratorId'),
//     registratorId => appHelpers.getNumber(registratorId)
// );

// export const getCardNumber = createSelector(
//     selectQueryParam('cardNumber'),
//     cardNumber => cardNumber
// );

// export const getDateStart = createSelector(
//     selectQueryParam('dateStart'),
//     dateStart => dateStart
// );

// export const getDateEnd = createSelector(
//     selectQueryParam('dateEnd'),
//     dateEnd => dateEnd
// );

// export const getEventId = createSelector(
//     selectQueryParam('eventId'),
//     eventId => appHelpers.getNumber(eventId)
// );

// export const getModuleName = createSelector(
//     selectRouterFeature,
//     route => route?.state?.data?.module
// );
// export const getNeedNavigationInsideTheModule = createSelector(
//     selectRouterFeature,
//     route => route?.state?.data?.needNavigationInsideTheModule
// );
