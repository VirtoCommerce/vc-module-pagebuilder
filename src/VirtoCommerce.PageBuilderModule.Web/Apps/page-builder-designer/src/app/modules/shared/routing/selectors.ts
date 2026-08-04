import { createSelector } from '@ngrx/store';
import { BuilderState } from './state';
import { EditorModuleInfo } from '@models/modules';

import * as helpers from '@core/helpers';

export const selectFeature = (state: BuilderState) => state.router;

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

// Language of the page being edited, passed as `cultureName` when the designer is opened (VCST-5219).
// Forwarded to the storefront preview so it renders in the page's language.
export const selectCultureNameParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['cultureName'] || ''
);

export const selectSharedComponentIdParameter = createSelector(
    selectQueryParams,
    queryParams => queryParams && queryParams['sharedComponentId'] || ''
);

export const selectTemplateKeyParameter = createSelector(
    selectTypeParameter,
    selectPathParameter,
    selectGroupIdParameter,
    selectSharedComponentIdParameter,
    (type, path, groupId, sharedComponentId) => {
        if (sharedComponentId) {
            return `shared-component::${sharedComponentId}`;
        }
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
