import { TestBed } from '@angular/core/testing';

import { ClipboardService } from '@core/services';
import { ContextMenuAction } from '@core/models';
import { AppConfig } from '@integration/services';

import { ContextMenuHelper } from './context-menu.helper';
import { createSharedComponentReference } from './shared-component.helpers';

describe('ContextMenuHelper Shared Components', () => {
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

    it('offers edit, detach, copy and relative paste for a shared instance', async () => {
        const actions = await TestBed.inject(ContextMenuHelper).getSectionsActions(
            createSharedComponentReference('component-1', 'placement-1'),
            false,
        );

        expect(actions.filter(action => action !== '|').map(action => action.action)).toEqual([
            'edit-shared-component',
            'detach-shared-component',
            'copy',
            'paste-before',
            'paste-after',
            'delete',
        ]);
        expect(findAction(actions, 'edit-shared-component').title).toBe('Edit original');
        expect(findAction(actions, 'detach-shared-component').title).toBe('Detach');
        expect(findAction(actions, 'paste-after').inactive).toBe(false);
    });

    it('opens the original read-only and keeps detach available with read permission', async () => {
        appConfig.getValue.mockImplementation((option: string) => option === 'canInsertSharedComponents');

        const actions = await TestBed.inject(ContextMenuHelper).getSectionsActions(
            createSharedComponentReference('component-1', 'placement-1'),
            false,
        );

        expect(findAction(actions, 'edit-shared-component').inactive).toBe(false);
        expect(findAction(actions, 'edit-shared-component').title).toBe('View original');
        expect(findAction(actions, 'detach-shared-component').inactive).toBe(false);
    });

    it('does not expose the original without read permission', async () => {
        appConfig.getValue.mockImplementation((option: string) => option === 'canEditSharedComponents');

        const actions = await TestBed.inject(ContextMenuHelper).getSectionsActions(
            createSharedComponentReference('component-1', 'placement-1'),
            false,
        );

        expect(findAction(actions, 'edit-shared-component').inactive).toBe(true);
        expect(findAction(actions, 'edit-shared-component').title).toBe('View original');
        expect(findAction(actions, 'detach-shared-component').inactive).toBe(true);
    });
});

function findAction(actions: ContextMenuAction[], name: string): Exclude<ContextMenuAction, '|'> {
    const result = actions.find(action => action !== '|' && action.action === name);
    if (!result || result === '|') {
        throw new Error(`Context action '${name}' was not found.`);
    }
    return result;
}
