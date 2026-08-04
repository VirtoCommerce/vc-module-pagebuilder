import { Action } from "@ngrx/store";

import * as editorHelpers from './editor.helpers';
import * as actions from "@editor/store/actions";
import * as sharedActions from "@shared/store/actions";
import { isSharedComponentReference } from './shared-component.helpers';
import { TemplateModel } from '@models/document';

export function pasteDataIntoTemplate(
    action: any, // actions.pasteFromClipboard
    context: any // selectors.changeTemplateContext
): Action[] {
    const value = action.value;
    const { template, sectionsSchemas, templateKey, templateEntry } = context;
    const directionMap: Record<string, number> = { 'paste-after': 1, 'paste-before': 0 };
    const direction = directionMap[action.action] ?? -1; // -1 = to end of list
    if (value.wrongData !== true) {

        // case when we paste block after or before section
        // it's a wrong case, therefore we must display paste window
        if (!!action.section && !action.block && value.type === 'block' && action.action !== 'paste-block') {
            return [
                sharedActions.showNotification({
                    message: `Block can be inserted into section only`,
                    msgType: 'info'
                }),
                actions.showClipboardModal({ ...action })
            ];
        }
        // paste block after or before
        else if (!!action.section && value.type === 'block') {
            return pasteBlockIntoSection(action, sectionsSchemas, template, templateKey, value, direction);
        }
        // paste section after or before
        if (value.type === 'section' && isSharedComponentReference(value.content)) {
            return [actions.chooseSharedComponentInsertionMode({
                componentId: value.content.componentRef,
                insertIndex: getSectionInsertIndex(template, action.section?.id, direction),
                defaultMode: 'copy',
            })];
        }
        if (value.type === 'section') {
            return pasteSectionIntoTemplate(action, sectionsSchemas, templateEntry, template, templateKey, value, direction);
        }
    }
    return [
        sharedActions.showNotification({
            message: `Incorrect data in clipboard`,
            msgType: 'info'
        }),
        actions.showClipboardModal({ ...action })
    ];
}

function getSectionInsertIndex(template: TemplateModel, sectionId: string | undefined, direction: number): number {
    if (!sectionId || direction < 0) {
        return template.content.length;
    }

    const sectionIndex = template.content.findIndex(section => section.id === sectionId);
    return sectionIndex < 0 ? template.content.length : sectionIndex + direction;
}

function pasteBlockIntoSection(
    action: any,
    sectionsSchemas: any,
    template: any,
    templateKey: string,
    value: any,
    direction: number
): Action[] {
    let accept = false;
    try {
        const blocks = sectionsSchemas[action.section.type].blocks;
        accept = !!(blocks && blocks.includes(value.content.type));
    } catch (error) {
        console.log(error);
    }

    if (accept) {
        const changedTemplate = editorHelpers.insertBlock(template!, action.section.id, action.block?.id || null, value.content, direction);
        return [
            actions.broadcastResolvedPreview({
                msg: {
                    type: 'changed',
                    template: changedTemplate.template,
                    section: changedTemplate.template.content.find(x => x.id === changedTemplate.sectionId),
                    sectionId: changedTemplate.sectionId,
                }
            }),
            actions.updateTemplateAction({
                template: changedTemplate.template,
                templateKey: templateKey
            }),
            sharedActions.showNotification({
                message: 'Block pasted',
                msgType: 'info'
            }),
            ...action.source === 'editor'
                ? [actions.editBlockAction({ sectionId: changedTemplate.sectionId, blockId: changedTemplate.blockId! })]
                : []
        ];
    } else {
        return [
            sharedActions.showNotification({
                message: `Section ${action.section.type} cannot contain block ${value.content.type}`,
                msgType: 'error'
            }),
            actions.showClipboardModal({ ...action })
        ];
    }
}

function pasteSectionIntoTemplate(
    action: any,
    sectionsSchemas: any,
    templateEntry: any,
    template: any,
    templateKey: string,
    value: any,
    direction: number
): Action[] {
    if ((!action.section || !!sectionsSchemas[action.section.type]) &&
        (!templateEntry.sections || !templateEntry.sections.length || templateEntry.sections?.includes(value.content.type))) {
        const changedTemplate = editorHelpers.insertSection(template!, action.section?.id || null, value.content, direction);
        return [
            actions.broadcastResolvedPreview({
                msg: {
                    type: 'add',
                    template: changedTemplate.template,
                    section: changedTemplate.template.content.find(x => x.id === changedTemplate.sectionId),
                    sectionId: changedTemplate.sectionId,
                    index: changedTemplate.template.content.findIndex(x => x.id === changedTemplate.sectionId),
                }
            }),
            actions.updateTemplateAction({
                template: changedTemplate.template,
                templateKey: templateKey
            }),
            sharedActions.showNotification({
                message: 'Section pasted',
                msgType: 'info'
            }),
            ...action.source === 'editor'
                ? [actions.editSectionAction({ sectionId: changedTemplate.sectionId })]
                : []

        ];
    } else {
        return [
            sharedActions.showNotification({
                message: `Template ${templateEntry.name} cannot contain section ${value.content.type}`,
                msgType: 'error'
            }),
            actions.showClipboardModal({ ...action })
        ];
    }
}
