import { ChangeDetectionStrategy, Component, inject, signal, computed, OnInit, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { firstValueFrom } from 'rxjs';
import { ActionsDropdownComponent } from '@core/components/actions-dropdown/actions-dropdown.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { ActionButtonDescriptor, ContextMenuAction, ContextMenuActionType } from '@core/models';
import { AppConfig, BuilderHttpClient } from '@integration/services';
import { BuilderState } from '@shared/store';
import * as actions from '@shared/store/actions';

interface PreviewAccount {
    id: string;
    name: string;
    photoUrl: string | null;
}

@Component({
    selector: 'app-impersonate-as',
    templateUrl: './impersonate-as.component.html',
    styleUrls: ['./impersonate-as.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ActionsDropdownComponent, ContextMenuComponent, IconComponent]
})
export class ImpersonateAsComponent implements OnInit {

    private readonly store = inject(Store<BuilderState>);
    private readonly appConfig = inject(AppConfig);
    private readonly http = inject(BuilderHttpClient);

    readonly view = input<'dropdown' | 'context-menu'>('dropdown');

    readonly selectedId = signal<string | null>(null);
    readonly loadedAccounts = signal<PreviewAccount[]>([]);

    readonly dropdownActions = computed<ActionButtonDescriptor[]>(() => {
        const users = this.loadedAccounts();
        if (!users.length) {
            return [];
        }
        const anonymous: ActionButtonDescriptor = {
            icon: 'visibility_off',
            title: 'Anonymous',
            alias: '',
        };
        const items: ActionButtonDescriptor[] = users.map(user => ({
            icon: 'person',
            title: user.name,
            alias: user.id,
        }));
        return [anonymous, ...items];
    });

    readonly contextMenuActions = computed<ContextMenuAction[]>(() => {
        const users = this.loadedAccounts();
        if (!users.length) {
            return [];
        }
        const currentId = this.selectedId();
        const anonymous: ContextMenuActionType = {
            icon: 'visibility_off',
            title: 'Anonymous',
            action: '',
            selected: !currentId,
        };
        const items: ContextMenuActionType[] = users.map(user => ({
            icon: 'person',
            title: user.name,
            action: user.id,
            imageUrl: user.photoUrl,
            selected: user.id === currentId,
        }));
        return [anonymous, '|', ...items];
    });

    readonly currentLabel = computed(() => {
        const id = this.selectedId();
        if (!id) {
            return 'Anonymous';
        }
        const user = this.loadedAccounts().find(u => u.id === id);
        return user?.name || id;
    });

    readonly hasAccounts = computed(() => {
        return this.loadedAccounts().length > 0;
    });

    ngOnInit() {
        this.loadAccounts();
    }

    onDropdownSelected(action: ActionButtonDescriptor) {
        const userId = action.alias || null;
        this.selectedId.set(userId);
        this.store.dispatch(actions.changePreviewAccount({ userId }));
    }

    onContextMenuSelected(action: ContextMenuActionType) {
        const userId = action.action || null;
        this.selectedId.set(userId);
        this.store.dispatch(actions.changePreviewAccount({ userId }));
    }

    private async loadAccounts() {
        const ids: string[] = this.appConfig.getValue('previewImpersonation' as any) || [];
        if (!ids.length) {
            return;
        }
        try {
            const requestConfig = this.appConfig.getValue('previewAccounts' as any);
            const request = this.http.generateRequest(requestConfig);
            const result = await firstValueFrom(this.http.doRequest<PreviewAccount[]>(request));
            if (result?.length) {
                this.loadedAccounts.set(result);
                this.restoreSelectedAccount(ids);
            }
        } catch (e) {
            console.error('Failed to load preview accounts:', e);
        }
    }

    private restoreSelectedAccount(validIds: string[]) {
        const savedId = localStorage.getItem('pb.previewAccountId');
        if (savedId && validIds.includes(savedId)) {
            this.selectedId.set(savedId);
            this.store.dispatch(actions.sendPreviewAuth({ userId: savedId }));
        }
    }
}
