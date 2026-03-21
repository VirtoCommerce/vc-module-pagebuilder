import { ChangeDetectionStrategy, Component, computed, input, output, signal, inject } from '@angular/core';
import { IconComponent } from '@core/components/icon/icon.component';
import { CheckboxComponent } from '@core/controls/checkbox/checkbox.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
import { ContextMenuAction } from '@core/models';
import { SectionModel, SectionSchema } from '@models/document';
import { ContextMenuHelper, helpers } from '@editor/helpers';

@Component({
  selector: 'app-section-item',
  templateUrl: './section-item.component.html',
  styleUrls: ['./section-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent, CheckboxComponent, ContextMenuComponent]
})
export class SectionItemComponent {

  private readonly helper = inject(ContextMenuHelper);

  readonly isHover = signal(false);
  readonly isIconHover = signal(false);

  readonly section = input.required<SectionModel>();
  readonly sectionSchema = input<SectionSchema | null>(null);
  readonly hasContextMenu = input(false);
  readonly selectable = input(true);
  readonly selected = input(false);

  readonly actionClick = output<string>();
  readonly itemClick = output();
  readonly itemHover = output();
  readonly itemSelectChanged = output<boolean>();

  readonly displayCheckbox = computed(() => (this.isIconHover() && this.selectable()) || this.selected());
  readonly sectionIcon = computed(() => this.sectionSchema()?.icon || 'blur_on');
  readonly sectionName = computed(() => {
    const schema = this.sectionSchema();
    return schema ? helpers.getSectionName(this.section(), schema) : '';
  });

  onItemClick(_event: MouseEvent) {
    if (this.sectionSchema()) {
      this.itemClick.emit();
    }
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
    this.isHover.set(true);
    this.itemHover.emit();
  }

  onItemLeave() {
    this.isHover.set(false);
    this.isIconHover.set(false);
  }

  readonly getItemActions = () =>
    this.helper.getSectionsActions(this.section(), !!this.sectionSchema()?.blocks?.length);
}
