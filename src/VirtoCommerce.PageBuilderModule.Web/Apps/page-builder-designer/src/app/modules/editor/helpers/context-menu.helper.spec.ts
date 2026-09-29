import { TestBed } from '@angular/core/testing';

import { ContextMenuAction } from '@core/models';
import { AppConfig } from '@integration/services';
import { SectionModel } from '@models/document';

import { ContextMenuHelper } from './context-menu.helper';
import { createSharedComponentReference } from './shared-component.helpers';

describe('ContextMenuHelper Shared Components', () => {
    const appConfig = { getValue: vi.fn() };

    beforeEach(() => {
        appConfig.getValue.mockReset();
        appConfig.getValue.mockReturnValue(true);
        TestBed.configureTestingModule({
            providers: [
                ContextMenuHelper,
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

    it('keeps paste available without reading the clipboard when the menu opens', async () => {
        const helper = TestBed.inject(ContextMenuHelper);
        const page = await helper.getPageActions();
        const instance = await helper.getSectionsActions(createSharedComponentReference('component-1'), false);
        expect(findAction(page, 'paste-section').inactive).toBeFalsy();
        expect(findAction(instance, 'paste-before').inactive).toBeFalsy();
        expect(findAction(instance, 'paste-after').inactive).toBeFalsy();
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

describe('ContextMenuHelper without clipboard access', () => {
    it('returns available paste actions without a clipboard service', async () => {
            TestBed.configureTestingModule({
                providers: [
                    ContextMenuHelper,
                    { provide: AppConfig, useValue: { getValue: () => true } },
                ],
            });
            const helper = TestBed.inject(ContextMenuHelper);
            const page = await helper.getPageActions(true, true);
            const section = await helper.getSectionsActions({ id: 'section-1', type: 'text' } as SectionModel, false);
            const shared = await helper.getSectionsActions(createSharedComponentReference('component-1'), false);

            expect(page.filter(action => action !== '|').map(action => action.action)).toEqual([
                'paste-section', 'delete-selected', 'save-as-shared-component', 'reset-template', 'refresh-preview',
            ]);
            expect(findAction(page, 'paste-section').inactive).toBeFalsy();
            expect(findAction(page, 'save-as-shared-component').inactive).toBe(false);
            expect(findAction(page, 'delete-selected').inactive).toBe(false);
            expect(section.filter(action => action !== '|').map(action => action.action)).toEqual([
                'hide', 'copy', 'paste-before', 'paste-after', 'duplicate', 'delete',
            ]);
            for (const actions of [section, shared]) {
                expect(findAction(actions, 'paste-before').inactive).toBeFalsy();
                expect(findAction(actions, 'paste-after').inactive).toBeFalsy();
                expect(findAction(actions, 'copy').inactive).toBeFalsy();
                expect(findAction(actions, 'delete').inactive).toBeFalsy();
            }
            expect(findAction(section, 'duplicate').inactive).toBeFalsy();
            expect(findAction(shared, 'edit-shared-component').inactive).toBe(false);
            expect(findAction(shared, 'detach-shared-component').inactive).toBe(false);
    });
});

function findAction(actions: ContextMenuAction[], name: string): Exclude<ContextMenuAction, '|'> {
    const result = actions.find(action => action !== '|' && action.action === name);
    if (!result || result === '|') {
        throw new Error(`Context action '${name}' was not found.`);
    }
    return result;
}
