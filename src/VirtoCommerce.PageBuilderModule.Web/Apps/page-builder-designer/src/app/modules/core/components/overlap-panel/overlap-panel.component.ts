import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
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
  },
})
export class OverlapPanelComponent {

  private readonly windowRef = inject(EnvironmentRef);
  private readonly elementRef = inject(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly expandable = input(true);
  readonly skipTranslate = input<boolean | null>(false);

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
