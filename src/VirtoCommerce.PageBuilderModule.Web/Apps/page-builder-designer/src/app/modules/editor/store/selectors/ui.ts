import { createSelector } from "@ngrx/store";

import { ActionButtonDescriptor } from '@core/models';
import {
    selectTemplateUIState,
    selectCurrentSectionsFilter
} from './common';

import { SectionStatesList, SectionState } from '@editor/models';
import { EditorModuleInfo } from '@models/modules';

import * as fromRoute from '@shared/routing';
import * as fromDomain from "./domain";
import * as fromData from "./data";
import * as fromShared from '@shared/store';

import { helpers } from "@editor/helpers";
import { appHelpers } from "@integration/helpers";

export const selectAddItemTitle = createSelector(
    fromData.selectSectionModelFromRoute,
    fromData.selectCurrentSchemaForEdit,
    (section, schema) => {
        if (!section)
            return 'Add block';
        // return 'Add section';
        const name = helpers.getSectionName(section, schema || null, 'section');
        const result = `Add block to '${name}'`;
        return result;
    }
);

export const hoveredSectionId = createSelector(
    selectTemplateUIState,
    state => state.hoveredSectionId
);

export const isLoading = createSelector(
    selectTemplateUIState,
    state => !!(state?.isTemplateLoading || state?.isSchemasLoading)
);

export const selectAddSectionPaneGroupStates = createSelector(
    selectTemplateUIState,
    state => state.addSectionPaneStates
);

export const selectPreviewItemType = createSelector(
    selectTemplateUIState,
    state => state.previewItemType
);

export const selectCurrentDragSections = createSelector(
    selectTemplateUIState,
    state => state.dragSectionIds
);

function getSelectedIds(states: SectionStatesList): string[] {
    const result = [];
    for (const key in states) {
        if (states[key].selected) {
            result.push(key);
        }
        if (result.length === 0) {
            for (const blockKey in states[key].blocks) {
                if (states[key].blocks[blockKey].selected) {
                    result.push(blockKey);
                }
            }
            if (result.length > 0) {
                break;
            }
        }
    }
    return result;
}

export const selectCheckedItems = createSelector(
    fromDomain.selectCurrentTemplateState,
    state => getSelectedIds(state?.sections || {})
);

const hasSelectedSection = createSelector(
    fromDomain.selectCurrentTemplateState,
    state => !!state?.sections && Object.values(state.sections).some(x => x.selected)
);

const selectKeyOfSectionWithSelectedBlock = createSelector(
    fromDomain.selectCurrentTemplateState,
    state => Object.keys(state?.sections || {}).find(x => Object.values(state?.sections[x].blocks || {}).some(b => b.selected))
);

export const selectSectionsState = createSelector(
    fromDomain.selectCurrentTemplateState,
    fromData.selectCurrentTemplateModel,
    fromData.selectSectionsSchemas,
    selectCurrentDragSections,
    hasSelectedSection,
    selectKeyOfSectionWithSelectedBlock,
    (state, model, schemas, dragSectionIds, hasSelectedSection, sectionKeyWithSelectedBlock) => {
        const result = (schemas && model?.content.filter(x => x.type && x.id).reduce((result, section) => {
            const canHaveChildren = (schemas[section.type]?.blocks?.length || 0) > 0;
            return <SectionStatesList>{
                ...result,
                [section.id]: <SectionState>{
                    expanded: canHaveChildren,
                    canHaveChildren,
                    isDragging: dragSectionIds.indexOf(section.id) !== -1,
                    selectable: !sectionKeyWithSelectedBlock,
                    ...state?.sections[section.id],
                    blocks: canHaveChildren ? section.blocks?.reduce((res, v) => ({
                        ...res,
                        [v.id]: {
                            isDragging: dragSectionIds.indexOf(v.id) !== -1,
                            selected: res[v.id]?.selected || false,
                            selectable: !hasSelectedSection && (!sectionKeyWithSelectedBlock || sectionKeyWithSelectedBlock === section.id),
                        }
                    }), state?.sections[section.id]?.blocks || {}) : {},
                }
            };
        }, <SectionStatesList>{})) || <SectionStatesList>{};
        return result;
    }
);

export const editTemplateContext = createSelector(
    fromData.selectCurrentTemplateModel,
    fromDomain.selectCurrentTemplateState,
    selectSectionsState,
    fromData.selectSectionsSchemas,
    fromData.selectBlocksSchemas,
    fromData.selectTemplateSettings,
    fromData.selectCurrentTemplateSettingsSchemas,
    selectCurrentDragSections,
    (template, templateState, sectionsState, sectionsSchemas, blocksSchemas, settings, settingsSchemas, currentDragSection) => {
        const selectedSectionsCount = Object.values(sectionsState).filter(x => x.selected).length;
        const selectedBlocksCount = Object.values(sectionsState).reduce((acc, value) => acc + Object.values(value.blocks || {}).filter(x => x.selected).length, 0);
        const result = template && sectionsSchemas && blocksSchemas
            ? {
                template, templateState, sectionsState, sectionsSchemas, blocksSchemas, settings, settingsSchemas,
                selectedSectionsCount, selectedBlocksCount,
                currentDragSection,
                selectMode: selectedSectionsCount > 0 || selectedBlocksCount > 0
            }
            : null;
        return result;
    }
);

export const selectAddItemContext = createSelector(
    fromData.selectGroupedSectionSchemas,
    selectAddSectionPaneGroupStates,
    selectPreviewItemType,
    selectCurrentSectionsFilter,
    fromData.selectSectionModelFromRoute,
    ({ groups, items }, states, previewItemType, filter, section) => ({
        groups,
        items,
        parentSection: section,
        states: {
            groups: groups.reduce((acc, value) => ({
                ...acc,
                [value.name]: <any>{
                    ...states[value.name],
                    opened: states[value.name]?.opened || !!filter
                },
            }), <any>{}),
            previewItemType
        },
    })
);

const isEditSettings = createSelector(
    fromRoute.getModeName,
    mode => mode === EditorModuleInfo.mode.editSettings // means page (or template) settings, static block
);

export const selectCurrentItemName = createSelector(
    fromData.selectBlockModelFromRoute,
    fromData.selectSectionModelFromRoute,
    fromData.selectSettingsFromRoute,
    fromData.selectCurrentSchemaForEdit,
    (block, section, settings, schema) => {
        const defaultName = !!settings
            ? (<string>schema?.['name'] || 'settings')
            : (block ? 'current block' : 'current section');
        const name = helpers.getSectionName(block || section || settings || null, schema || null, defaultName);
        return 'Edit ' + name;
    }
);

export const selectEditSectionContext = createSelector(
    fromData.selectBlockModelFromRoute,
    fromData.selectSectionModelFromRoute,
    fromData.selectBlockSchemaFromRoute,
    fromData.selectSectionSchemaFromRoute,
    fromData.selectCurrentItemForEdit,
    fromData.selectCurrentSchemaForEdit,
    fromData.selectCurrentTemplateModel,
    fromData.selectObjectsSchemas,
    isEditSettings,
    (block, section, blockSchema, sectionSchema, model, schema, template, objects, isSettings) => !!schema && !!model
        ? <any>{
            block, section, blockSchema, sectionSchema, schema, model,
            isEditSettings: isSettings,
            editContext: {
                model, // current item under editing, can be block, section or settings
                block, // current block or null
                section, // current section, useful in block
                template, // current template
                schema: schema,
                sectionSchema,
                blockSchema,
                objects,
                utils: appHelpers,
                page: template?.content,
                settings: template?.settings,
            }
        }
        : null
);

export const changeTemplateContext = createSelector(
    fromData.selectCurrentTemplateModel,
    fromData.selectSectionModelFromRoute,
    fromData.selectBlockModelFromRoute,
    fromData.selectSectionsSchemas,
    fromData.selectBlocksSchemas,
    fromRoute.selectTemplateKeyParameter,
    fromRoute.selectSectionIdParameter,
    fromRoute.selectBlockIdParameter,
    fromRoute.selectInsertIndexParameter,
    fromShared.selectCurrentTemplateEntry,
    (template, section, block, sectionsSchemas, blocksSchemas, templateKey, sectionId, blockId, insertIndex, templateEntry) =>
        ({ template, section, block, sectionsSchemas, blocksSchemas, templateKey, sectionId, blockId, insertIndex, templateEntry })
);

export const selectToolbarButtonsState = (context: { useTheme: boolean, useDrafts: boolean, useExternalPreview: boolean }) => createSelector(
    // fromDomain.selectCurrentTemplateState,
    fromShared.hasDirty,
    fromDomain.selectCurrentTemplateState,
    // todo: undo
    // todo: redo
    // todo: have settings
    (hasDirty, state) => {
        const result = <ActionButtonDescriptor[][]>[];
        if (context.useTheme) {
            result.push([
                {
                    icon: 'settings',
                    alias: 'theme-settings',
                    title: 'Theme settings',
                    type: 'outline'
                }
            ]);
        }

        if (context.useExternalPreview) {
            result.push([
                {
                    canAction: !hasDirty,
                    icon: 'visibility',
                    alias: 'external-preview',
                    title: 'Preview',
                    type: 'outline'
                }
            ]);
        }

        if (context.useDrafts && !state?.isLoading && !state?.error) {
            result.push([
                {
                    canAction: !hasDirty && state?.published && !state?.hasChanges,
                    icon: 'unpublished',
                    alias: 'unpublish',
                    title: 'Unpublish',
                    type: 'outline'
                },
                {
                    canAction: !hasDirty && state?.hasChanges,
                    icon: 'publish',
                    alias: 'publish',
                    title: 'Publish',
                    type: 'outline'
                },
            ]);
        }

        // [
        //     {
        //         alias: 'preview',
        //         title: 'Preview',
        //         type: 'outline'
        //     }
        // ],
        // [
        //     {
        //         canAction: false,
        //         icon: 'undo',
        //         alias: 'undo'
        //     },
        //     {
        //         canAction: false,
        //         icon: 'redo',
        //         alias: 'redo'
        //     }
        // ],

        result.push([
            {
                canAction: hasDirty,
                title: 'Save',
                alias: 'save',
                type: 'primary'
            }
        ]);

        return result;
    }
);
