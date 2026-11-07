<template>
  <VcPopup
    :model-value="true"
    :title="$t('PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_TITLE')"
    @close="emit('close')"
  >
    <template #content>
      <div class="pd-discard">
        <p>{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_PICK") }}</p>
        <ul class="pd-discard__list">
          <li
            v-for="option in options"
            :key="option.part"
          >
            <label
              class="pd-discard__option"
              :class="{ 'pd-discard__option--off': !option.allowed }"
              :title="option.allowed ? undefined : $t('PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_NOT_ALLOWED')"
            >
              <input
                type="checkbox"
                :checked="ticked.includes(option.part)"
                :disabled="!option.allowed"
                @change="toggle(option.part)"
              />
              <span>{{ $t(partLabel(option.part, template)) }}</span>
            </label>
          </li>
        </ul>
        <p v-if="options.some((x) => x.part !== 'page')">{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_THEME") }}</p>
        <p v-if="unsaved">{{ $t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_UNSAVED") }}</p>
        <VcButton
          :disabled="!unsaved && !choice.page && !choice.theme.length"
          @click="emit('discard', choice)"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.HISTORY.DISCARD_CONFIRM") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script setup lang="ts">
// Discard's confirmation: which saved drafts go. The page's is ticked; a theme part's draft is shared by every page, so
// it goes only when ticked, and only one the editor may edit can be.
import { computed, ref } from "vue";
import { VcButton, VcPopup } from "@vc-shell/framework/ui";
import { discardChoice, type DiscardOption } from "../discard";
import { partLabel, type HistoryFile, type ThemePart } from "../history";

const props = defineProps<{
  options: DiscardOption[];
  /** the page part is a template */
  template: boolean;
  /** unsaved edits, which go whatever is ticked */
  unsaved: boolean;
}>();
const emit = defineEmits<{ discard: [choice: { page: boolean; theme: ThemePart[] }]; close: [] }>();

const ticked = ref<HistoryFile[]>(props.options.filter((x) => x.checked).map((x) => x.part));
const toggle = (part: HistoryFile) => (ticked.value = ticked.value.includes(part) ? ticked.value.filter((x) => x !== part) : [...ticked.value, part]);
const choice = computed(() => discardChoice(props.options, ticked.value));
</script>

<style lang="scss" scoped>
.pd-discard {
  display: flex;
  flex-direction: column;
  gap: 12px;
  width: min(480px, 80vw);

  p {
    margin: 0;
    color: var(--neutrals-600);
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  &__option {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;

    input {
      accent-color: var(--primary-500);
    }

    &--off {
      color: var(--neutrals-400);
      cursor: not-allowed;
    }
  }

  > :last-child {
    align-self: flex-end;
  }
}
</style>
