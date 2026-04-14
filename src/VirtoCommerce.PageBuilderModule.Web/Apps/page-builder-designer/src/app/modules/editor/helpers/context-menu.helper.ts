import { Injectable, inject } from "@angular/core";

import { ClipboardService } from '@core/services';
import { ContextMenuAction } from '@core/models';
import { Dictionary, SectionModel } from '@models/index';

@Injectable({
    providedIn: 'root'
})
export class ContextMenuHelper {
    private readonly clipboard = inject(ClipboardService);
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

    async getPageActions(hasSelection = false): Promise<ContextMenuAction[]> {
        const emptyClipboardData = !(await this.hasClipboardData());
        const result: (string | [string, boolean])[] = [
            ['paste-section', emptyClipboardData],
            ['delete-selected', !hasSelection],
            '|',
            'reset-template', 'refresh-preview'
        ];
        return this.getActions(result);
    }

    private async hasClipboardData(): Promise<boolean> {
        const clipboardData = await this.clipboard.getData();
        return clipboardData != null;
    }
}
