import { ChangeDetectionStrategy, Component, computed, input, output, signal, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { IconComponent } from '@core/components/icon/icon.component';
import { CheckboxComponent } from '@core/controls/checkbox/checkbox.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
// import { trigger, state, style, animate, transition } from '@angular/animations';

import { ContextMenuAction } from '@core/models';
import { SectionModel, SectionSchema } from '@models/document';
import { ContextMenuHelper, helpers } from '@editor/helpers';

@Component({
  selector: 'app-section-item',
  templateUrl: './section-item.component.html',
  styleUrls: ['./section-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, IconComponent, CheckboxComponent, ContextMenuComponent]
})
export class SectionItemComponent {

  private readonly helper = inject(ContextMenuHelper);

  readonly isHover = signal(false);
  readonly isIconHover = signal(false);

  readonly section = input.required<SectionModel>();
  readonly sectionSchema = input.required<SectionSchema>();
  readonly hasContextMenu = input(false);
  readonly selectable = input(true);
  readonly selected = input(false);

  readonly actionClick = output<string>();
  readonly itemClick = output();
  readonly itemHover = output();
  readonly itemSelectChanged = output<boolean>();

  readonly displayCheckbox = computed(() => (this.isIconHover() && this.selectable()) || this.selected());
  readonly sectionIcon = computed(() => this.sectionSchema()?.icon || 'blur_on');
  readonly sectionName = computed(() => helpers.getSectionName(this.section(), this.sectionSchema()));

  onItemClick(event: MouseEvent) {
    if (!!this.sectionSchema()) {
      this.itemClick.emit();
    }
  }

  onCheckboxClick(event: MouseEvent) {
    event.stopPropagation();
  }

  onCheckboxValueChanged(value: boolean) {
    this.itemSelectChanged.emit(value);
  }

  onActionClick(event: ContextMenuAction) {
    if (event !== '|') {
      this.actionClick.emit(event.action);
    }
  }

  onItemHover() {
    this.itemHover.emit();
  }

  getItemActions: () => Promise<ContextMenuAction[]> = () => {
    const result = this.helper.getSectionsActions(this.section(), !!this.sectionSchema()?.blocks?.length);
    return result;
  };
}
