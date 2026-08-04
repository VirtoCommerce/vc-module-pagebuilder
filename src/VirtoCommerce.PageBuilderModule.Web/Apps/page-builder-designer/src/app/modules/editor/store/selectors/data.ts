import { TemplateEntry } from '@shared/models';
import { SectionModel, SectionSchema } from '@models/index';
import { createSelector } from '@ngrx/store';

import { selectTemplateKeyParameter } from '@shared/routing';
import { selectTemplateDataState, selectCurrentSectionsFilter } from './common';

import { SharedComponent, SharedComponentInstanceView, SectionsSchemasList } from '@editor/models';
import { appHelpers } from '@integration/helpers';
import { coreHelpers } from '@core/helpers';
import { helpers, isSharedComponentReference } from '@editor/helpers';

import * as fromRoute from '@shared/routing/selectors';
import * as fromShared from '@shared/store/selectors';

export const selectLoadedTemplates = createSelector(
    selectTemplateDataState,
    state => state.templates
);

export const selectCurrentTemplateModel = createSelector(
    selectLoadedTemplates,
    selectTemplateKeyParameter,
    (templates, templateKey) => templateKey ? templates[templateKey] : null
);

export const selectSharedComponents = createSelector(
    selectTemplateDataState,
    state => state.sharedComponents,
);

export const selectSharedComponentContents = createSelector(
    selectTemplateDataState,
    state => state.sharedComponentContents,
);

export const selectSharedComponentErrors = createSelector(
    selectTemplateDataState,
    state => state.sharedComponentErrors,
);

export const selectSharedComponentUsageRefreshIdsByTemplate = createSelector(
    selectTemplateDataState,
    state => state.sharedComponentUsageRefreshIdsByTemplate,
);

export const selectSharedComponentDetailsState = createSelector(
    selectTemplateDataState,
    state => state.sharedComponentDetails,
);

export const selectSharedComponentsSearchState = createSelector(
    selectTemplateDataState,
    state => state.sharedComponentsSearch,
);

export const selectSharedComponentsSearchView = createSelector(
    selectSharedComponentsSearchState,
    selectSharedComponents,
    (search, components) => ({
        ...search,
        results: search.resultIds
            .map(id => components[id])
            .filter((component): component is SharedComponent => !!component),
    }),
);

export const selectCurrentSharedComponent = createSelector(
    selectSharedComponents,
    fromRoute.selectSharedComponentIdParameter,
    (components, componentId) => componentId ? components[componentId] || null : null,
);

export const selectFileName = createSelector(
    fromRoute.selectPathParameter,
    path => path.split('/').pop().split('.').slice(0, -1).join('.')
);

export const selectCurrentTemplateName = createSelector(
    selectCurrentTemplateModel,
    selectFileName,
    selectCurrentSharedComponent,
    (model, fileName, sharedComponent) => sharedComponent?.name || model?.settings?.['displayName'] || model?.settings?.['name'] || fileName || '[no name]'
);

export const selectAllSchemas = createSelector(
    selectTemplateDataState,
    state => state.schemas
);

export const selectSectionsSchemas = createSelector(
    selectTemplateDataState,
    state => state.schemas
        && <SectionsSchemasList>appHelpers.spreadPropertyByOther(state.schemas.sections, 'group', 'groupIcon', 'groupSort')
        || {}
);

export const selectObjectsSchemas = createSelector(
    selectTemplateDataState,
    state => state.schemas?.objects || {}
);

export const selectChangedTemplates = createSelector(
    selectLoadedTemplates, // models
    fromShared.selectChangedTemplates, // entries
    (templates, entries) => entries.map(x => ({ entry: x.entry, info: x, content: templates[x.key]! })).filter(x => !!x.content)
);

export const selectSectionsSchemasList = createSelector(
    selectSectionsSchemas,
    sections => Object.keys(sections).map(key => ({ ...sections[key], key }))
);

export const selectBlocksSchemas = createSelector(
    selectTemplateDataState,
    state => state.schemas?.blocks || {}
);

export const selectSharedSchemas = createSelector(
    selectTemplateDataState,
    state => state.schemas?.shared || {}
);

export const selectTemplateSettings = createSelector(
    selectCurrentTemplateModel,
    template => template?.settings || <SectionModel>{}
);

export const selectBlocksSchemasList = createSelector(
    selectBlocksSchemas,
    blocks => Object.keys(blocks).map(key => blocks[key])
);

const selectCurrentTemplateAllSectionsSchemasUnsorted = createSelector(
    selectSectionsSchemas,
    fromShared.selectCurrentTemplateEntry,
    (schemas, entry) => entry?.sections
        ? entry.sections.map(type => ({ ...schemas[type], type })).filter(x => !!x)
        : (appHelpers.toList(schemas, 'type') as SectionSchema[])
            .filter(x => !x.targetTemplates || x.targetTemplates.find(t => t === entry?.key))
);

function compareSchemas(a: { sort?: number, name: string, type?: string }, b: { sort?: number, name: string, type?: string }): number {
    const isEmpty = (x: { sort?: number, name: string, type?: string }) => x.sort === undefined || x.sort === null;

    if (!isEmpty(a) && !isEmpty(b) && a.sort !== b.sort) {
        return a.sort! - b.sort!;
    }

    if (isEmpty(a) && isEmpty(b) || a.sort === b.sort) {
        const aName = a.name || a.type || '';
        const bName = b.name || b.type || '';
        return aName.localeCompare(bName);
    }

    if (isEmpty(a)) {
        return 1;
    }
    if (isEmpty(b)) {
        return -1;
    }
    return 0;
}

const selectCurrentTemplateAllSectionsSchemas = createSelector(
    selectCurrentTemplateAllSectionsSchemasUnsorted,
    list => [...list].sort(compareSchemas)
);

const selectCurrentTemplateEmbeddedSettingsSchemas = createSelector(
    fromShared.selectCurrentTemplateEntry,
    (entry: TemplateEntry) => entry?.settings && entry.settings.length
        ? <SectionSchema>{
            // todo: should it be in config?
            icon: 'construction',
            type: '',
            name: 'Settings',
            displayField: 'default',
            settings: entry.settings
        }
        : null
);

export const selectCurrentTemplateSettingsSchemas = createSelector(
    selectCurrentTemplateAllSectionsSchemas,
    selectCurrentTemplateEmbeddedSettingsSchemas,
    (schemas, entry) => ({
        top: [entry, ...schemas.filter(x => x.static === true || x.static === 'top')].filter(x => !!x),
        bottom: schemas.filter(x => x.static === 'bottom')
    })
);

export const selectCurrentTemplateSectionsSchemas = createSelector(
    selectCurrentTemplateAllSectionsSchemas,
    schemas => schemas.filter(x => !x.static)
);

export const selectSettingsFromRoute = createSelector(
    fromRoute.selectSettingsTypeParameter,
    selectCurrentTemplateModel,
    (settingsType, template) => settingsType === null
        ? null
        : template?.settings
);

export const selectSettingsSchemaFromRoute = createSelector(
    fromRoute.selectSettingsTypeParameter,
    selectSectionsSchemas,
    selectCurrentTemplateEmbeddedSettingsSchemas,
    (settingsType, schemas, settingsSchema) => {
        if (settingsType === null) return null;
        return settingsType === '' ? settingsSchema : schemas[settingsType];
    }
);

export const selectSectionModelFromRoute = createSelector(
    fromRoute.selectSectionIdParameter,
    selectCurrentTemplateModel,
    (sectionId, template) => sectionId
        ? template?.content.find(x => x.id == sectionId)
        : null
);

export const selectSharedComponentInstanceFromRoute = createSelector(
    selectSectionModelFromRoute,
    selectSharedComponents,
    selectSharedComponentErrors,
    selectSharedComponentDetailsState,
    (section, components, errors, details): SharedComponentInstanceView | null => {
        if (!isSharedComponentReference(section)) {
            return null;
        }

        const isCurrentDetailsRequest = details.componentId === section.componentRef;

        return {
            reference: section,
            component: components[section.componentRef] ?? null,
            error: errors[section.componentRef] ?? null,
            detailsLoading: isCurrentDetailsRequest && details.loading,
            detailsError: isCurrentDetailsRequest ? details.error : null,
        };
    },
);

export const selectSectionSchemaFromRoute = createSelector(
    selectSectionModelFromRoute,
    selectSectionsSchemas,
    selectSharedSchemas,
    selectObjectsSchemas,
    fromShared.selectCurrentTemplateEntry,
    (section, schemas, shared, objects, entry) =>
        section &&
        schemas[section.type] &&
        helpers.prepareSchema(schemas[section.type], shared, objects, '_sections', entry?.controls)
);

export const selectBlockModelFromRoute = createSelector(
    fromRoute.selectBlockIdParameter,
    selectSectionModelFromRoute,
    (blockId, section) => section && blockId
        ? section.blocks.find(x => x.id == blockId)
        : null
);

export const selectBlockSchemaFromRoute = createSelector(
    selectBlockModelFromRoute,
    selectBlocksSchemas,
    selectSectionsSchemas,
    selectSharedSchemas,
    selectObjectsSchemas,
    fromShared.selectCurrentTemplateEntry,
    (model, blocksSchemas, sectionsSchemas, shared, objects, entry) =>
        model &&
        (blocksSchemas[model.type] || sectionsSchemas[model.type]) &&
        (helpers.prepareSchema(blocksSchemas[model.type] || sectionsSchemas[model.type], shared, objects, '_blocks', entry?.controls))
);

export const selectCurrentItemForEdit = createSelector(
    selectBlockModelFromRoute,
    selectSectionModelFromRoute,
    selectSettingsFromRoute,
    (block, section, settings) => block || section || settings
);

export const selectCurrentSchemaForEdit = createSelector(
    selectBlockSchemaFromRoute,
    selectSectionSchemaFromRoute,
    selectSettingsSchemaFromRoute,
    (block, section, settings) => block || section || settings
);

const selectSectionBlockSchemasFromRoute = createSelector(
    selectSectionModelFromRoute,
    selectBlocksSchemas,
    selectSectionsSchemas,
    (section, blocksSchemas, sectionsSchemas) => section
        ? sectionsSchemas[section.type].blocks?.map(type => ({ ...blocksSchemas[type], type })).sort(compareSchemas)
        : null
);

const selectCurrentItemsSchemas = createSelector(
    selectCurrentTemplateSectionsSchemas,
    selectSectionBlockSchemasFromRoute,
    (sectionSchemas, blockSchemas) => blockSchemas || sectionSchemas
);

export const selectCurrentFilteredSectionSchemas = createSelector(
    selectCurrentItemsSchemas,
    selectCurrentSectionsFilter,
    (sections, filter) => filter
        ? sections.filter(x => x.type.toUpperCase().indexOf(filter.toUpperCase()) !== -1)
        : sections
);

export const selectGroupedSectionSchemas = createSelector(
    selectCurrentFilteredSectionSchemas,
    sections => {
        const groups = coreHelpers.groupSections(sections);
        return {
            groups: groups.filter(x => x.items.length && !x.noname).sort(compareSchemas),
            items: groups.find(x => x.noname)?.items || []
        }
    }
);

export const selectRunActionContext = createSelector(
    fromRoute.selectTemplateKeyParameter,
    fromShared.selectCurrentTemplateEntry,
    fromRoute.selectPathParameter,
    fromRoute.selectTypeParameter,
    fromRoute.selectGroupIdParameter,
    (templateKey, entry, path, type, groupId) => ({ templateKey, entry, path, type, groupId })
);
