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
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { NgClass } from '@angular/common';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-overlap-panel',
  templateUrl: './overlap-panel.component.html',
  styleUrls: ['./overlap-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, IconComponent, CdkTrapFocus],
  host: {
    '[class.inplace]': 'skipTranslate()',
    '(window:resize)': 'onResize()',
  },
})
export class OverlapPanelComponent {

  private readonly windowRef = inject(EnvironmentRef);
  private readonly elementRef = inject(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly expandable = input(true);
  readonly skipTranslate = input<boolean | null>(false);
  readonly dismissible = input(false);
  readonly accessibleLabel = input('Editor panel');
  readonly dismissed = output();

  onKeydown(event: KeyboardEvent) {
    if (this.dismissible() && event.key === 'Escape' && !event.defaultPrevented) {
      event.preventDefault();
      event.stopPropagation();
      this.dismissed.emit();
    }
  }

  readonly contentWidth = signal<number | null>(null);
  readonly expanderPosition = signal<number | null>(null);
  readonly isOpened = signal(false);

  constructor() {
    afterNextRender(() => {
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
