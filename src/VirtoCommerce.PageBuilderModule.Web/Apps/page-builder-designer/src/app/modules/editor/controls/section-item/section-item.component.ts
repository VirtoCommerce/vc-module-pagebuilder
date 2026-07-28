import { ChangeDetectionStrategy, Component, computed, input, output, signal, inject } from '@angular/core';
import { IconComponent } from '@core/components/icon/icon.component';
import { CheckboxComponent } from '@core/controls/checkbox/checkbox.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
import { ContextMenuAction } from '@core/models';
import { SectionModel, SectionSchema } from '@models/document';
import { ContextMenuHelper, helpers, isLinkedComponentReference } from '@editor/helpers';
import { LinkedComponent } from '@editor/models';

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
  readonly linkedComponent = input<LinkedComponent | null>(null);
  readonly linkedError = input<string | null>(null);

  readonly actionClick = output<string>();
  readonly itemClick = output();
  readonly itemHover = output<boolean>();
  readonly itemSelectChanged = output<boolean>();

  readonly displayCheckbox = computed(() => (this.isIconHover() && this.selectable()) || this.selected());
  readonly isLinked = computed(() => isLinkedComponentReference(this.section()));
  readonly sectionIcon = computed(() => {
    if (!this.isLinked()) {
      return this.sectionSchema()?.icon || 'blur_on';
    }

    return this.linkedError() ? 'link_off' : 'link';
  });
  readonly sectionName = computed(() => {
    if (this.isLinked()) {
      return this.linkedComponent()?.name || (this.linkedError() ? 'Missing Shared Component' : 'Shared Component');
    }
    const schema = this.sectionSchema();
    return helpers.getSectionName(this.section(), schema);
  });

  onItemClick(_event: MouseEvent) {
    if (this.sectionSchema() || this.isLinked()) {
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
    this.itemHover.emit(true);
  }

  onItemLeave() {
    this.isHover.set(false);
    this.isIconHover.set(false);
    this.itemHover.emit(false);
  }

  readonly getItemActions = () =>
    this.helper.getSectionsActions(this.section(), !!this.sectionSchema()?.blocks?.length);
}
