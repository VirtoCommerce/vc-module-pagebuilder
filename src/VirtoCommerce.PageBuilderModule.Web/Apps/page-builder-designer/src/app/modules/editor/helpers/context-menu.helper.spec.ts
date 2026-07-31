import { TestBed } from '@angular/core/testing';

import { ClipboardService } from '@core/services';
import { ContextMenuAction } from '@core/models';
import { AppConfig } from '@integration/services';

import { ContextMenuHelper } from './context-menu.helper';
import { createLinkedComponentReference } from './linked-component.helpers';

describe('ContextMenuHelper Linked Components', () => {
    const clipboard = { getData: vi.fn() };
    const appConfig = { getValue: vi.fn() };

    beforeEach(() => {
        clipboard.getData.mockReset();
        clipboard.getData.mockResolvedValue({ type: 'section' });
        appConfig.getValue.mockReset();
        appConfig.getValue.mockReturnValue(true);
        TestBed.configureTestingModule({
            providers: [
                ContextMenuHelper,
                { provide: ClipboardService, useValue: clipboard },
                { provide: AppConfig, useValue: appConfig },
            ],
        });
    });

    it('offers edit, detach, copy and relative paste for a linked instance', async () => {
        const actions = await TestBed.inject(ContextMenuHelper).getSectionsActions(
            createLinkedComponentReference('component-1', 'placement-1'),
            false,
        );

        expect(actions.filter(action => action !== '|').map(action => action.action)).toEqual([
            'edit-linked-component',
            'detach-linked-component',
            'copy',
            'paste-before',
            'paste-after',
            'delete',
        ]);
        expect(findAction(actions, 'edit-linked-component').title).toBe('Edit original');
        expect(findAction(actions, 'detach-linked-component').title).toBe('Detach');
        expect(findAction(actions, 'paste-after').inactive).toBe(false);
    });

    it('opens the original read-only and keeps detach available with read permission', async () => {
        appConfig.getValue.mockImplementation((option: string) => option === 'canInsertLinkedComponents');

        const actions = await TestBed.inject(ContextMenuHelper).getSectionsActions(
            createLinkedComponentReference('component-1', 'placement-1'),
            false,
        );

        expect(findAction(actions, 'edit-linked-component').inactive).toBe(false);
        expect(findAction(actions, 'edit-linked-component').title).toBe('View original');
        expect(findAction(actions, 'detach-linked-component').inactive).toBe(false);
    });

    it('does not expose the original without read permission', async () => {
        appConfig.getValue.mockImplementation((option: string) => option === 'canEditLinkedComponents');

        const actions = await TestBed.inject(ContextMenuHelper).getSectionsActions(
            createLinkedComponentReference('component-1', 'placement-1'),
            false,
        );

        expect(findAction(actions, 'edit-linked-component').inactive).toBe(true);
        expect(findAction(actions, 'edit-linked-component').title).toBe('View original');
        expect(findAction(actions, 'detach-linked-component').inactive).toBe(true);
    });
});

function findAction(actions: ContextMenuAction[], name: string): Exclude<ContextMenuAction, '|'> {
    const result = actions.find(action => action !== '|' && action.action === name);
    if (!result || result === '|') {
        throw new Error(`Context action '${name}' was not found.`);
    }
    return result;
}
