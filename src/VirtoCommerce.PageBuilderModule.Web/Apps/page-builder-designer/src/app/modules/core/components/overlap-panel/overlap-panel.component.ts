import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { EnvironmentRef } from '@integration/services';
import { NgClass } from '@angular/common';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-overlap-panel',
  templateUrl: './overlap-panel.component.html',
  styleUrls: ['./overlap-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, IconComponent],
  host: {
    '[class.inplace]': 'skipTranslate()',
    '(window:resize)': 'onResize()',
    '(keydown)': 'onKeydown($event)',
  },
})
export class OverlapPanelComponent {

  private readonly windowRef = inject(EnvironmentRef);
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly returnFocusTo = this.elementRef.nativeElement.ownerDocument.activeElement;

  readonly expandable = input(true);
  readonly skipTranslate = input<boolean | null>(false);
  readonly dismissible = input(false);
  readonly accessibleLabel = input('Editor panel');
  readonly dismissed = output();

  onKeydown(event: KeyboardEvent) {
    if (this.dismissible() && event.key === 'Escape') {
      const select = (event.target as HTMLElement).closest('ng-select');
      // ng-select prevents Escape even when its dropdown is already closed.
      if (event.defaultPrevented && (!select || select.classList.contains('ng-select-opened'))) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      this.dismissed.emit();
    }
  }

  readonly contentWidth = signal<number | null>(null);
  readonly expanderPosition = signal<number | null>(null);
  readonly isOpened = signal(false);

  constructor() {
    this.destroyRef.onDestroy(() => {
      const host = this.elementRef.nativeElement;
      const document = host.ownerDocument;
      if (this.dismissible() && this.returnFocusTo instanceof HTMLElement && this.returnFocusTo.isConnected
        && (host.contains(document.activeElement) || document.activeElement === document.body)) {
        this.returnFocusTo.focus({ preventScroll: true });
      }
    });
    afterNextRender(() => {
      const host = this.elementRef.nativeElement;
      if (this.dismissible() && host.ownerDocument.activeElement === this.returnFocusTo) {
        host.querySelector<HTMLElement>('[role="region"]')?.focus({ preventScroll: true });
      }
      const interval = setInterval(() => this.changeWidth(), 1000);
      this.destroyRef.onDestroy(() => clearInterval(interval));
    });
  }

  onResize() {
    this.changeWidth();
  }

  toggle() {
    this.isOpened.set(!this.isOpened());
    this.changeWidth();
  }

  changeWidth() {
    setTimeout(() => {
      if (this.isOpened()) {
        this.contentWidth.set(this.windowRef.nativeWindow.innerWidth / 2);
      } else {
        this.contentWidth.set(null);
      }
      this.expanderPosition.set(this.contentWidth() || this.elementRef.nativeElement.offsetWidth);
    });
  }

}
