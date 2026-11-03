import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { afterNextRender, Component, input, output, signal, ChangeDetectionStrategy, viewChild, contentChild, ElementRef, inject, Injector, DestroyRef } from '@angular/core';
import { NgClass } from '@angular/common';
import { ContextMenuAction, ContextMenuActionType } from '@core/models';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-context-menu',
  templateUrl: './context-menu.component.html',
  styleUrls: ['./context-menu.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, CdkConnectedOverlay, CdkOverlayOrigin, IconComponent]
})
export class ContextMenuComponent {
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  readonly actions = input<ContextMenuAction[] | null>(null);
  readonly visible = input(false);
  readonly accessibleLabel = input('Actions');
  readonly getActions = input<(() => Promise<ContextMenuAction[]>) | null>(null);

  readonly overlay = viewChild.required(CdkConnectedOverlay);
  readonly trigger = viewChild.required(CdkOverlayOrigin);
  readonly menuPanel = viewChild<ElementRef<HTMLElement>>('menuPanel');
  readonly customTrigger = contentChild<ElementRef>('contextMenuTrigger');

  readonly onAction = output<ContextMenuActionType>();

  private readonly _cachedActions = signal<ContextMenuAction[] | null>(null);
  private loadId = 0;
  private returnFocusTo: HTMLElement | null = null;
  readonly isOpen = signal(false);
  positions: ConnectedPosition[] = [];

  constructor() {
    this.destroyRef.onDestroy(() => this.loadId++);
  }

  focusFirstAction() {
    afterNextRender(() => {
      if (this.isOpen()) {
        this.menuPanel()?.nativeElement.querySelector<HTMLButtonElement>('[role="menuitem"]')
          ?.focus({ preventScroll: true });
      }
    }, { injector: this.injector });
  }

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
    if (!this.isOpen()) {
      const activeElement = this.trigger().elementRef.nativeElement.ownerDocument.activeElement;
      this.returnFocusTo = activeElement instanceof HTMLElement ? activeElement : null;
    }
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
    const panel = this.menuPanel()?.nativeElement;
    const document = this.trigger().elementRef.nativeElement.ownerDocument;
    if (panel?.contains(document.activeElement) || (this.isOpen() && document.activeElement === document.body)) {
      const target = this.returnFocusTo?.isConnected && this.returnFocusTo !== document.body
        ? this.returnFocusTo : this.trigger().elementRef.nativeElement;
      target.focus({ preventScroll: true });
    }
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
      // CDK owns Escape dismissal through (detach). Contain it to this menu.
      event.stopPropagation();
      return;
    }
    if (event.key === 'Tab') {
      // Restore the trigger before the browser advances to the next/previous control.
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
