<template>
  <!-- Shown in the language row, not in a popup: the select's option list opens in the app root, under a popup. -->
  <VcCol class="tw-gap-2">
    <div class="tw-flex tw-items-end tw-gap-3">
      <VcSelect
        v-model="groupId"
        class="tw-grow"
        :label="$t('PAGE_BUILDER.MULTILANGUAGE.LINK_PAGE.PAGE', { language: cultureName })"
        :options="loadPages"
        option-value="id"
        option-label="title"
        searchable
      />
      <VcButton
        variant="secondary"
        @click="$emit('close')"
      >
        {{ $t("PAGE_BUILDER.MULTILANGUAGE.LINK_PAGE.CANCEL") }}
      </VcButton>
      <VcButton
        :disabled="!groupId"
        @click="groupId && $emit('link', groupId)"
      >
        {{ $t("PAGE_BUILDER.MULTILANGUAGE.LINK_PAGE.LINK") }}
      </VcButton>
    </div>
    <VcHint>{{ $t("PAGE_BUILDER.MULTILANGUAGE.LINK_PAGE.HINT") }}</VcHint>
  </VcCol>
</template>

<script lang="ts" setup>
import { ref } from "vue";
import { VcButton, VcCol, VcHint, VcSelect } from "@vc-shell/framework/ui";
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
