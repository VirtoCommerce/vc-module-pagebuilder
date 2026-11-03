import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogContent, MatDialogRef } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { take } from 'rxjs/operators';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { ModalService } from '@core/services';

import { PageVersion } from '@editor/models';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import { BuilderState } from '@editor/store/state';

/**
 * The open page's versions: what is published, what is not published yet, and what to do with either.
 *
 * Versions come from the content repository, which means edits made outside the builder appear here too —
 * that is the point of the panel. Continuing from one is forward only: the chosen content becomes a new
 * commit on this editor's own branch, so nothing anyone else did is overwritten and nothing drops out of
 * history.
 */
@Component({
    selector: 'app-page-history',
    templateUrl: './page-history.component.html',
    styleUrls: ['./page-history.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatDialogContent, IconComponent, IconButtonComponent]
})
export class PageHistoryComponent {

    private readonly store$ = inject(Store<BuilderState>);
    private readonly dialogRef = inject(MatDialogRef<PageHistoryComponent>);
    private readonly modals = inject(ModalService);
    private readonly data = inject(MAT_DIALOG_DATA);

    readonly history = toSignal(this.store$.select(selectors.selectPageHistory), { initialValue: null });

    /** Shas of the folded runs the reader has opened up. */
    readonly expanded = signal<string[]>([]);

    isExpanded(sha: string): boolean {
        return this.expanded().includes(sha);
    }

    toggle(sha: string) {
        this.expanded.update(open => open.includes(sha) ? open.filter(x => x !== sha) : [...open, sha]);
    }

    preview(version: PageVersion) {
        this.store$.dispatch(actions.previewVersion({ sha: version.sha }));
    }

    /**
     * Asks first, always. The editor is about to be moved onto different content, and while their current
     * draft is not lost — it stays in history as the commit before this one — that is not obvious from a
     * list of other people's versions.
     */
    restore(version: PageVersion) {
        if (this.history()?.hasDirty) {
            return;
        }

        this.modals.confirm(`Continue editing from version ${version.shortSha}? Your current draft stays in history as an earlier version.`)
            .pipe(take(1))
            .subscribe(confirmed => {
                if (confirmed) {
                    this.store$.dispatch(actions.restoreVersion({ templateKey: this.data.templateKey, sha: version.sha }));
                }
            });
    }

    loadMore() {
        this.store$.dispatch(actions.loadPageHistory({
            templateKey: this.data.templateKey,
            after: this.history()?.endCursor,
        }));
    }

    close() {
        this.dialogRef.close();
    }

    /** Who to name for a version: the platform login when the server recorded one, else the git author. */
    author(version: PageVersion): string {
        return version.vcUser || version.author?.login || version.author?.name || version.author?.email || 'unknown';
    }

    when(version: PageVersion): string {
        const at = Date.parse(version.date ?? '');
        if (!Number.isFinite(at)) {
            return '';
        }

        const minutes = Math.round((Date.now() - at) / 60000);
        if (minutes < 1) {
            return 'just now';
        }
        if (minutes < 60) {
            return `${minutes} min ago`;
        }
        const hours = Math.round(minutes / 60);
        if (hours < 24) {
            return `${hours} h ago`;
        }
        const days = Math.round(hours / 24);
        return days < 30 ? `${days} d ago` : new Date(at).toLocaleDateString();
    }

    /** Where the version lives, for a reader who wants to find it in git. */
    branches(version: PageVersion): string {
        const [first, ...rest] = version.branches ?? [];
        if (!first) {
            return '';
        }
        return rest.length > 0 ? `${first} +${rest.length}` : first;
    }
}
