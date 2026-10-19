<template>
  <VcPopup
    :model-value="true"
    :title="$t('PAGE_BUILDER.MULTILANGUAGE.LINK_POPUP.TITLE', { language: cultureName })"
    is-mobile-fullscreen
    @update:model-value="!$event && $emit('close')"
    @close="$emit('close')"
  >
    <template #content>
      <VcCol class="tw-gap-3">
        <VcHint>{{ $t("PAGE_BUILDER.MULTILANGUAGE.LINK_POPUP.HINT") }}</VcHint>
        <VcSelect
          v-model="groupId"
          :label="$t('PAGE_BUILDER.MULTILANGUAGE.LINK_POPUP.PAGE')"
          :options="loadPages"
          option-value="id"
          option-label="title"
          searchable
        />
      </VcCol>
    </template>

    <template #footer>
      <div class="tw-flex tw-justify-end tw-gap-3">
        <VcButton
          variant="secondary"
          @click="$emit('close')"
        >
          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LINK_POPUP.CANCEL") }}
        </VcButton>
        <VcButton
          :disabled="!groupId"
          @click="groupId && $emit('link', groupId)"
        >
          {{ $t("PAGE_BUILDER.MULTILANGUAGE.LINK_POPUP.LINK") }}
        </VcButton>
      </div>
    </template>
  </VcPopup>
</template>

<script lang="ts" setup>
import { ref } from "vue";
import { VcButton, VcCol, VcHint, VcPopup, VcSelect } from "@vc-shell/framework/ui";
import { PageBuilderPageListItem } from "../../../api_client/virtocommerce.pagebuildermodule";

interface Props {
  cultureName: string;
  /** Landing Pages of the language that can become its version. */
  findPages: (
    cultureName: string,
    keyword?: string,
    skip?: number,
  ) => Promise<{ totalCount: number; results: PageBuilderPageListItem[] }>;
}

interface Emits {
  (event: "close"): void;
  (event: "link", groupId: string): void;
}

const props = defineProps<Props>();
defineEmits<Emits>();

const groupId = ref<string>();

async function loadPages(keyword?: string, skip?: number) {
  const pages = await props.findPages(props.cultureName, keyword, skip);
  return {
    totalCount: pages.totalCount,
    results: pages.results.map((x) => ({ id: x.id, title: `${x.name} (${x.permalink})` })),
  };
}
</script>
