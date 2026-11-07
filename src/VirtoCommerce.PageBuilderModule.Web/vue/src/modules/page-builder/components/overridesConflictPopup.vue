<template>
  <VcPopup
    :model-value="true"
    :title="$t('PAGE_BUILDER.MULTILANGUAGE.CONFLICT_POPUP.TITLE')"
    is-mobile-fullscreen
    @update:model-value="!$event && $emit('close')"
    @close="$emit('close')"
  >
    <template #content>
      <VcCol class="tw-gap-3">
        <VcHint>{{ $t("PAGE_BUILDER.MULTILANGUAGE.CONFLICT_POPUP.HINT") }}</VcHint>
        <ul class="overrides-conflict__list">
          <li
            v-for="conflict in conflicts"
            :key="conflict.cultureName"
          >
            <span class="tw-font-semibold">{{ conflict.cultureName }}</span>
            — {{ conflict.fields.map(fieldLabel).join(", ") }}
          </li>
        </ul>
      </VcCol>
    </template>

    <template #footer>
      <div class="tw-flex tw-flex-wrap tw-justify-end tw-gap-3">
        <VcButton
          variant="secondary"
          @click="$emit('close')"
        >
          {{ $t("PAGE_BUILDER.MULTILANGUAGE.CONFLICT_POPUP.CANCEL") }}
        </VcButton>
        <VcButton
          variant="secondary"
          @click="$emit('apply-all')"
        >
          {{ $t("PAGE_BUILDER.MULTILANGUAGE.CONFLICT_POPUP.APPLY_ALL") }}
        </VcButton>
        <VcButton @click="$emit('apply-inherited')">
          {{ $t("PAGE_BUILDER.MULTILANGUAGE.CONFLICT_POPUP.APPLY_INHERITED") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import { VcButton, VcCol, VcHint, VcPopup } from "@vc-shell/framework/ui";
import { inheritableFieldLabelKey, type InheritableField } from "../composables/useMultilanguageInheritance";

export interface IOverridesConflict {
  cultureName: string;
  fields: InheritableField[];
}

interface Props {
  conflicts: IOverridesConflict[];
}

interface Emits {
  (event: "close"): void;
  /** Root values replace the overrides of the changed fields. */
  (event: "apply-all"): void;
  /** Only languages that inherit the changed fields get the new values (the default behavior of a save). */
  (event: "apply-inherited"): void;
}

defineProps<Props>();
defineEmits<Emits>();

const { t } = useI18n({ useScope: "global" });

function fieldLabel(field: InheritableField): string {
  return t(inheritableFieldLabelKey[field]);
}
</script>

<style lang="scss" scoped>
.overrides-conflict__list {
  @apply tw-list-disc tw-pl-5 tw-text-sm;
}
</style>
