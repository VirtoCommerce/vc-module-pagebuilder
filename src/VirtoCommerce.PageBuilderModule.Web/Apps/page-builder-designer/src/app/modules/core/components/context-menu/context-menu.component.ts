import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { Component, input, output, signal, ChangeDetectionStrategy, viewChild, contentChild, ElementRef } from '@angular/core';
import { NgClass } from '@angular/common';
import { ContextMenuAction, ContextMenuActionType } from '@core/models';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-context-menu',
  templateUrl: './context-menu.component.html',
  styleUrls: ['./context-menu.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, CdkConnectedOverlay, CdkOverlayOrigin, CdkTrapFocus, IconComponent]
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
  private loadId = 0;
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
    const loadId = ++this.loadId;
    try {
      const getActions = this.getActions();
      if (!this.actions() && getActions) {
        const actions = await getActions();
        if (loadId !== this.loadId) {
          return;
        }
        this._cachedActions.set(actions);
      }
      this.isOpen.set(true);
    } catch (error) {
      if (loadId === this.loadId) {
        console.error('Could not load context menu actions', error);
        this.isOpen.set(false);
      }
    }
  }

  hideActions() {
    this.loadId++;
    if (this.getActions()) {
      this._cachedActions.set(null);
    }
    this.isOpen.set(false);
  }

  gearClick(event: MouseEvent | KeyboardEvent) {
    const target = event.target as HTMLElement;
    const y = event instanceof MouseEvent && event.detail > 0
      ? event.clientY
      : target.getBoundingClientRect().top + target.offsetHeight / 2;
    if (y > window.innerHeight / 2) {
      this.positions = [
        {
          originX: 'start',
          originY: 'top',
          overlayX: 'start',
          overlayY: 'bottom',
        },
        {
          originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top',
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
        {
          originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom',
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
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Home' || event.key === 'End') {
      const menu = (event.target as HTMLElement).closest('.panel');
      const items = Array.from(menu?.querySelectorAll<HTMLButtonElement>('[role="menuitem"]') || []);
      if (!items.length) {
        return;
      }
      const index = items.indexOf(event.target as HTMLButtonElement);
      let next = 0;
      if (event.key === 'End') {
        next = items.length - 1;
      } else if (event.key !== 'Home') {
        const step = event.key === 'ArrowDown' ? 1 : -1;
        next = (index + step + items.length) % items.length;
      }
      event.preventDefault();
      event.stopPropagation();
      items[next].focus();
    }
  }

  raiseOnAction(action: ContextMenuAction) {
    if (action !== '|' && !this.evaluateFunction(action.inactive)) {
      this.onAction.emit(action);
      this.hideActions();
    }
  }
}
