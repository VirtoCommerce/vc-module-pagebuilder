import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { Component, input, output, signal, ChangeDetectionStrategy, viewChild, contentChild, ElementRef } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { ContextMenuAction, ContextMenuActionType } from '@core/models';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-context-menu',
  templateUrl: './context-menu.component.html',
  styleUrls: ['./context-menu.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, NgStyle, CdkConnectedOverlay, CdkOverlayOrigin, IconComponent]
})
export class ContextMenuComponent {

  readonly actions = input<ContextMenuAction[] | null>(null);
  readonly visible = input(false);
  readonly getActions = input<(() => Promise<ContextMenuAction[]>) | null>(null);

  readonly overlay = viewChild.required(CdkConnectedOverlay);
  readonly customTrigger = contentChild<ElementRef>('contextMenuTrigger');

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
        .catch(() => { });
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

  gearClick(event: MouseEvent | KeyboardEvent) {
    const target = event.target as HTMLElement;
    const y = 'pageY' in event ? event.pageY : target.getBoundingClientRect().top + target.offsetHeight / 2;
    if (y > window.innerHeight / 2) {
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
