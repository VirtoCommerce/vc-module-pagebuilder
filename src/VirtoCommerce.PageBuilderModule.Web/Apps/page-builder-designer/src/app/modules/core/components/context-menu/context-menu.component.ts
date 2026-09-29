import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { Component, input, output, signal, ChangeDetectionStrategy, viewChild, contentChild, ElementRef } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { ContextMenuAction, ContextMenuActionType } from '@core/models';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-context-menu',
  templateUrl: './context-menu.component.html',
  styleUrls: ['./context-menu.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, NgStyle, CdkConnectedOverlay, CdkOverlayOrigin, CdkTrapFocus, IconComponent]
})
export class ContextMenuComponent {

  readonly actions = input<ContextMenuAction[] | null>(null);
  readonly visible = input(false);
  readonly accessibleLabel = input('Actions');
  readonly getActions = input<(() => Promise<ContextMenuAction[]>) | null>(null);

  readonly overlay = viewChild.required(CdkConnectedOverlay);
  readonly customTrigger = contentChild<ElementRef>('contextMenuTrigger');

  readonly onAction = output<ContextMenuActionType>();

  private readonly _cachedActions = signal<ContextMenuAction[] | null>(null);
  readonly isOpen = signal(false);
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
    return this._cachedActions() || [];
  }

  async showActions() {
    try {
      const getActions = this.getActions();
      if (!this.actions() && getActions) {
        this._cachedActions.set(await getActions());
      }
      this.isOpen.set(true);
    } catch {
      this.isOpen.set(false);
    }
  }

  hideActions() {
    if (this.getActions()) {
      this._cachedActions.set(null);
    }
    this.isOpen.set(false);
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

  onOverlayKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.hideActions();
    }
  }

  raiseOnAction(action: ContextMenuAction) {
    if (action !== '|' && !this.evaluateFunction(action.inactive)) {
      this.onAction.emit(action);
      this.hideActions();
    }
  }
}
