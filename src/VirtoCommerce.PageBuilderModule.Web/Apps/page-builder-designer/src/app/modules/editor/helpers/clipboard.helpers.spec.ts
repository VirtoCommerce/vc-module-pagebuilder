import { pasteDataIntoTemplate } from './clipboard.helpers';
import { createTemplate, createSection, createBlock, createSchema } from '@app/testing';
import * as actions from '@editor/store/actions';
import * as sharedActions from '@shared/store/actions';
import { createSharedComponentReference } from './shared-component.helpers';

function createContext(overrides: any = {}) {
    return {
        template: createTemplate({ content: [createSection({ id: 's1', type: 'hero', blocks: [createBlock({ id: 'b1', type: 'text' })] })] }),
        sectionsSchemas: { hero: { blocks: ['text', 'image'] } },
        templateKey: 'home',
        templateEntry: { name: 'Home', sections: [] },
        ...overrides,
    };
}

// ── pasteDataIntoTemplate ─────────────────────────────────────────

describe('pasteDataIntoTemplate', () => {

    // ── wrong data ────────────────────────────────────────────────

    it('shows error notification for wrongData', () => {
        const action = { value: { wrongData: true }, action: 'paste-after', source: 'list' };
        const result = pasteDataIntoTemplate(action, createContext());

        expect(result.length).toBe(2);
        expect(result[0]).toEqual(sharedActions.showNotification({ message: 'Incorrect data in clipboard', msgType: 'info' }));
        expect(result[1].type).toBe(actions.showClipboardModal.type);
    });

    // ── paste block: section context but no block, not paste-block ─

    it('shows notification when pasting block after section (ambiguous case)', () => {
        const action = {
            value: { type: 'block', content: { type: 'text' } },
            section: createSection({ id: 's1', type: 'hero' }),
            block: undefined,
            action: 'paste-after',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        expect(result.length).toBe(2);
        expect(result[0]).toEqual(sharedActions.showNotification({ message: 'Block can be inserted into section only', msgType: 'info' }));
        expect(result[1].type).toBe(actions.showClipboardModal.type);
    });

    it('does NOT show ambiguous notification when action is paste-block', () => {
        const action = {
            value: { type: 'block', content: { type: 'text' } },
            section: createSection({ id: 's1', type: 'hero' }),
            block: undefined,
            action: 'paste-block',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        // Should proceed to paste block, not show ambiguous notification
        expect(result[0].type).not.toBe(actions.showClipboardModal.type);
    });

    // ── paste block: accepted ─────────────────────────────────────

    it('pastes block into section when type is allowed', () => {
        const action = {
            value: { type: 'block', content: { type: 'text' } },
            section: createSection({ id: 's1', type: 'hero' }),
            block: createBlock({ id: 'b1' }),
            action: 'paste-after',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        const actionTypes = result.map(a => a.type);
        expect(actionTypes).toContain(actions.broadcastResolvedPreview.type);
        expect(actionTypes).toContain(actions.updateTemplateAction.type);
        expect(actionTypes).toContain(sharedActions.showNotification.type);
    });

    it('opens editor when source is "editor" after pasting block', () => {
        const action = {
            value: { type: 'block', content: { type: 'text' } },
            section: createSection({ id: 's1', type: 'hero' }),
            block: createBlock({ id: 'b1' }),
            action: 'paste-after',
            source: 'editor' as const,
        };

        const result = pasteDataIntoTemplate(action, createContext());

        const actionTypes = result.map(a => a.type);
        expect(actionTypes).toContain(actions.editBlockAction.type);
    });

    it('does not open editor when source is "list" after pasting block', () => {
        const action = {
            value: { type: 'block', content: { type: 'text' } },
            section: createSection({ id: 's1', type: 'hero' }),
            block: createBlock({ id: 'b1' }),
            action: 'paste-after',
            source: 'list' as const,
        };

        const result = pasteDataIntoTemplate(action, createContext());

        const actionTypes = result.map(a => a.type);
        expect(actionTypes).not.toContain(actions.editBlockAction.type);
    });

    // ── paste block: rejected (wrong block type) ──────────────────

    it('shows error when block type is not allowed in section', () => {
        const action = {
            value: { type: 'block', content: { type: 'video' } },
            section: createSection({ id: 's1', type: 'hero' }),
            block: createBlock({ id: 'b1' }),
            action: 'paste-after',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        expect(result[0]).toEqual(sharedActions.showNotification({
            message: 'Section hero cannot contain block video',
            msgType: 'error',
        }));
        expect(result[1].type).toBe(actions.showClipboardModal.type);
    });

    // ── paste section: accepted ───────────────────────────────────

    it('asks whether a copied Shared Component stays linked or becomes independent', () => {
        const action = {
            value: {
                type: 'section',
                content: createSharedComponentReference('component-1', 'source-placement'),
            },
            section: createSection({ id: 's1', type: 'hero' }),
            action: 'paste-after',
            source: 'list',
        };

        expect(pasteDataIntoTemplate(action, createContext())).toEqual([
            actions.chooseSharedComponentInsertionMode({
                componentId: 'component-1',
                insertIndex: 1,
                defaultMode: 'copy',
            }),
        ]);
    });

    it('pastes section into template', () => {
        const action = {
            value: { type: 'section', content: { type: 'banner' } },
            section: undefined,
            action: 'paste-after',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        const actionTypes = result.map(a => a.type);
        expect(actionTypes).toContain(actions.broadcastResolvedPreview.type);
        expect(actionTypes).toContain(actions.updateTemplateAction.type);
        expect(actionTypes).toContain(sharedActions.showNotification.type);
    });

    it('opens editor when source is "editor" after pasting section', () => {
        const action = {
            value: { type: 'section', content: { type: 'banner' } },
            section: undefined,
            action: 'paste-after',
            source: 'editor' as const,
        };

        const result = pasteDataIntoTemplate(action, createContext());

        const actionTypes = result.map(a => a.type);
        expect(actionTypes).toContain(actions.editSectionAction.type);
    });

    // ── paste section: rejected (not in allowed list) ─────────────

    it('shows error when section type not in templateEntry.sections', () => {
        const action = {
            value: { type: 'section', content: { type: 'banner' } },
            section: undefined,
            action: 'paste-after',
            source: 'list',
        };
        const context = createContext({
            templateEntry: { name: 'Home', sections: ['hero'] },
        });

        const result = pasteDataIntoTemplate(action, context);

        expect(result[0]).toEqual(sharedActions.showNotification({
            message: 'Template Home cannot contain section banner',
            msgType: 'error',
        }));
        expect(result[1].type).toBe(actions.showClipboardModal.type);
    });

    // ── direction mapping ─────────────────────────────────────────

    it('maps paste-before to direction 0', () => {
        const action = {
            value: { type: 'section', content: { type: 'banner' } },
            section: createSection({ id: 's1', type: 'hero' }),
            action: 'paste-before',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        // Section should be inserted before s1, so banner comes first
        const updateAction = result.find(a => a.type === actions.updateTemplateAction.type) as any;
        expect(updateAction).toBeTruthy();
        expect(updateAction.template.content[0].type).toBe('banner');
    });

    it('maps unknown action to direction -1 (append)', () => {
        const action = {
            value: { type: 'section', content: { type: 'banner' } },
            section: undefined,
            action: 'paste',
            source: 'list',
        };

        const result = pasteDataIntoTemplate(action, createContext());

        const updateAction = result.find(a => a.type === actions.updateTemplateAction.type) as any;
        expect(updateAction).toBeTruthy();
        // Appended at end
        expect(updateAction.template.content[updateAction.template.content.length - 1].type).toBe('banner');
    });
});
