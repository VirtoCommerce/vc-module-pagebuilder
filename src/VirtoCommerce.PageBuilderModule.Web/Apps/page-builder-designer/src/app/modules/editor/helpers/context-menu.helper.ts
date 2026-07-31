import { Injectable, inject } from "@angular/core";

import { ClipboardService } from '@core/services';
import { ContextMenuAction } from '@core/models';
import { Dictionary, SectionModel } from '@models/index';
import { AppConfig } from '@integration/services';
import { canEditLinkedComponentOriginal, isLinkedComponentReference } from './linked-component.helpers';

@Injectable({
    providedIn: 'root'
})
export class ContextMenuHelper {
    private readonly clipboard = inject(ClipboardService);
    private readonly appConfig = inject(AppConfig);
    private readonly items: Dictionary<ContextMenuAction> = {
        '|': '|',
        'hide': {
            action: 'hide',
            title: 'Hide',
            icon: 'visibility',
            selected: false,
            inactive: false
        },
        'show': {
            action: 'show',
            title: 'Show',
            icon: 'visibility_off',
            selected: false,
            inactive: false
        },
        'copy': {
            action: 'copy',
            title: 'Copy',
            icon: 'content_copy',
            selected: false,
            inactive: false
        },
        'paste-before': {
            action: 'paste-before',
            title: 'Paste before',
            icon: 'content_paste',
            selected: false,
            inactive: false
        },
        'paste-after': {
            action: 'paste-after',
            title: 'Paste after',
            icon: 'content_paste',
            selected: false,
            inactive: false
        },
        'paste-section': {
            action: 'paste-section',
            title: 'Paste section',
            icon: 'content_paste',
            selected: false,
            inactive: false
        },
        'paste-block': {
            action: 'paste-block',
            title: 'Paste block',
            icon: 'content_paste',
            selected: false,
            inactive: false
        },
        'paste-template': {
            action: 'paste-template',
            title: 'Paste template',
            icon: 'content_paste',
            selected: false,
            inactive: false
        },
        'duplicate': {
            action: 'duplicate',
            title: 'Duplicate',
            icon: 'file_copy',
            selected: false,
            inactive: false
        },
        'delete': {
            action: 'delete',
            title: 'Delete',
            icon: 'delete_outline'
        },
        'delete-selected': {
            action: 'delete-selected',
            title: 'Delete selected',
            icon: 'delete_sweep'
        },
        'reset-template': {
            action: 'reset-template',
            title: 'Reset template',
            icon: 'restart_alt'
        },
        'refresh-preview': {
            action: 'refresh-preview',
            title: 'Refresh preview',
            icon: 'refresh'
        },
        'save-as-linked-component': {
            action: 'save-as-linked-component',
            title: 'Save selected as Shared Component',
            icon: 'link'
        },
        'edit-linked-component': {
            action: 'edit-linked-component',
            title: 'Edit original',
            icon: 'edit'
        },
        'detach-linked-component': {
            action: 'detach-linked-component',
            title: 'Detach',
            icon: 'link_off'
        }
    }

    getActions(actions: (string | [string, boolean])[]): ContextMenuAction[] {
        return actions.map(x => {
            if (typeof x === 'string') {
                return this.items[x];
            } else {
                const [alias, inactive] = x;
                const action = this.items[alias] as object;
                return <ContextMenuAction>{ ...action, inactive };
            }
        });
    }

    async getSectionsActions(item: SectionModel, canAddBlock: boolean): Promise<ContextMenuAction[]> {
        if (isLinkedComponentReference(item)) {
            const linkedClipboardEmpty = !(await this.hasClipboardData());
            const canOpenOriginal = this.can('canInsertLinkedComponents');
            const canEditOriginal = canEditLinkedComponentOriginal(this.appConfig);
            return this.getActions([
                ['edit-linked-component', !canOpenOriginal],
                ['detach-linked-component', !this.can('canInsertLinkedComponents')],
                '|',
                'copy',
                ['paste-before', linkedClipboardEmpty],
                ['paste-after', linkedClipboardEmpty],
                '|',
                'delete',
            ]).map(action => action !== '|' && action.action === 'edit-linked-component'
                ? { ...action, title: canEditOriginal ? 'Edit original' : 'View original' }
                : action);
        }
        const emptyClipboardData = !(await this.hasClipboardData());

        const result: (string | [string, boolean])[] = [
            item.hidden ? 'show' : 'hide',
            '|',
            'copy',
            ['paste-before', emptyClipboardData],
            ['paste-after', emptyClipboardData]
        ];
        if (canAddBlock) {
            result.push(['paste-block', emptyClipboardData]);
        }
        result.push(
            'duplicate',
            '|',
            'delete');

        return this.getActions(result);
    }

    async getPageActions(hasSelection = false, hasSelectedSections = false, allowSaveAsLinked = true): Promise<ContextMenuAction[]> {
        const emptyClipboardData = !(await this.hasClipboardData());
        const result: (string | [string, boolean])[] = [
            ['paste-section', emptyClipboardData],
            ['delete-selected', !hasSelection],
            [
                'save-as-linked-component',
                !allowSaveAsLinked
                    || !hasSelectedSections
                    || !this.can('canCreateLinkedComponents')
                    || !this.can('canInsertLinkedComponents'),
            ],
            '|',
            'reset-template', 'refresh-preview'
        ];
        return this.getActions(result);
    }

    private async hasClipboardData(): Promise<boolean> {
        const clipboardData = await this.clipboard.getData();
        return clipboardData != null;
    }

    private can(option: 'canInsertLinkedComponents' | 'canCreateLinkedComponents'): boolean {
        return this.appConfig.getValue(option) === true;
    }
}
