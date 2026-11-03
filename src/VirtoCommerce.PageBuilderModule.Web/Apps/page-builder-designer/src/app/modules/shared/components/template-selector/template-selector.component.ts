import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';
import { MultipageSelectComponent } from '@core/components/multipage-select/multipage-select.component';

import { MultipageSelectDescriptor } from '@core/models';
import { TemplateEntryState, TemplateEntry } from '@shared/models';

import { BuilderState } from '@shared/store';
import * as fromState from '@shared/store';
import * as actions from '@shared/store/actions';

import { map } from 'rxjs';

@Component({
    selector: 'app-template-selector',
    templateUrl: './template-selector.component.html',
    styleUrls: ['./template-selector.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MultipageSelectComponent]
})
export class TemplateSelectorComponent {

    private readonly store$ = inject(Store<BuilderState>);

    defaultTemplate = { title: 'Choose template', type: '', path: '', templateKey: '' };

    readonly rootTemplates = toSignal(this.store$.select(fromState.selectTemplatesEntriesWithState).pipe(
        map(value => value?.map(x => this.convertTemplateToItem(x)) || [])
    ), { initialValue: [] as MultipageSelectDescriptor[] });
    readonly currentTemplate = toSignal(this.store$.select(fromState.selectCurrentTemplateEntry).pipe(
        map(value => value ? this.convertTemplateToItem({ entry: value, state: null }) : null)
    ), { initialValue: null });
    readonly currentFilter = toSignal(this.store$.select(fromState.selectCurrentFilter), { initialValue: null });
    readonly listTitle = toSignal(this.store$.select(fromState.selectRootTemplateTitle), { initialValue: null });
    readonly childrenItems = toSignal(this.store$.select(fromState.selectCurrentChildrenTemplatesEntriesWithState).pipe(
        map(value => value?.map(x => this.convertTemplateToItem(x)) || null)
    ), { initialValue: null });

    onTemplateSelected(item: MultipageSelectDescriptor) {
        if (item.hasChildren) {
            this.store$.dispatch(actions.switchToChildrenTemplates({ templateKey: item.templateKey }));
        } else {
            this.store$.dispatch(actions.selectTemplate({ templateKey: item.templateKey, templateType: item.type, path: item.path }));
        }
    }

    onFilterChanged(value: string) {
        this.store$.dispatch(actions.filterTemplates({ filter: value }));
    }

    onBackClick() {
        this.store$.dispatch(actions.displayRootTemplates());
    }

    private convertTemplateToItem(value: { entry: TemplateEntry, state: TemplateEntryState | null }): MultipageSelectDescriptor {
        return {
            title: value.entry.name,
            templateKey: this.generateTemplateKey(value.entry),
            type: value.entry.type || '',
            path: value.entry.path!,
            hasChildren: value.entry.hasChildren,
            isDirty: !!value.state?.isDirty
        };
    }

    private generateTemplateKey(value: TemplateEntry): string {
        return value.key || (value.type + "::" + value.path);
    }
}
