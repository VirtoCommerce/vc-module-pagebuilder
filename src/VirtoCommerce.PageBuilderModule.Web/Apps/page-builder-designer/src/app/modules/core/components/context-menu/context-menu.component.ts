import { CdkConnectedOverlay, ConnectedPosition } from '@angular/cdk/overlay';
import { Component, input, output, signal, ChangeDetectionStrategy, viewChild } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { OverlayModule } from '@angular/cdk/overlay';
import { ContextMenuAction, ContextMenuActionType } from '@core/models';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-context-menu',
    templateUrl: './context-menu.component.html',
    styleUrls: ['./context-menu.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, NgStyle, OverlayModule, IconComponent]
})
export class ContextMenuComponent {

    readonly actions = input<ContextMenuAction[] | null>(null);
    readonly visible = input(false);
    readonly getActions = input<(() => Promise<ContextMenuAction[]>) | null>(null);

    readonly overlay = viewChild.required(CdkConnectedOverlay);

    readonly onAction = output<ContextMenuActionType>();

    private readonly _cachedActions = signal<ContextMenuAction[] | null>(null);
    isOpen = false;
    positions: ConnectedPosition[] = [];

    evaluateFunction(func: boolean | (() => boolean) | undefined): boolean {
        if (typeof func === 'function') {
            return func();
        }
        return !!func;
    }

    getActionsList(): ContextMenuAction[] {
        const staticActions = this.actions();
        if (staticActions) {
            return staticActions;
        }
        const getActionsFn = this.getActions();
        if (!this._cachedActions() && getActionsFn) {
            getActionsFn().then(actions => this._cachedActions.set(actions))
                         .catch(() => {});
        }
        return this._cachedActions() || [];
    }

    showActions() {
        this.isOpen = true;
    }

    hideActions() {
        if (this.getActions()) {
            this._cachedActions.set(null);
        }
        this.isOpen = false;
    }

    gearClick(event: MouseEvent) {
        if (event.pageY > window.innerHeight / 2) {
            this.positions = [
                {
                    originX: 'start',
                    originY: 'top',
                    overlayX: 'start',
                    overlayY: 'bottom',
                },
            ];
        } else {
            this.positions = [
                {
                    originX: 'start',
                    originY: 'bottom',
                    overlayX: 'start',
                    overlayY: 'top',
                },
            ];
        }
        event.stopPropagation();
        this.showActions();
    }

    outsideClick(event: MouseEvent) {
        event.stopPropagation();
        this.hideActions();
    }

    raiseOnAction(action: ContextMenuAction) {
        if (action !== '|' && !this.evaluateFunction(action.inactive)) {
            this.onAction.emit(action);
            this.hideActions();
        }
    }
}
