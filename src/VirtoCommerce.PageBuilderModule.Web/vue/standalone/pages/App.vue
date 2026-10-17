<template>
  <VcApp
    :is-ready="isReady"
    :logo="logoImage"
    title="Page Builder"
    :version="version"
  >
  </VcApp>
</template>

<script lang="ts" setup>
import { useBlade, useUser } from "@vc-shell/framework";
import { nextTick, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import logoImage from "/assets/logo.svg";
import { refreshMenuBadges } from "../../src/modules/page-builder/composables";

import { VcApp } from "@vc-shell/framework/ui";

const isReady = ref(false);
const version = import.meta.env.PACKAGE_VERSION;

const { isAuthenticated } = useUser();
const { openBlade } = useBlade();
const router = useRouter();

/**
 * A link straight into the designer (the classic admin's page blade opens these):
 * ?storeId=…&designGroup=<page group id>, or ?storeId=…&designPath=<file path>&designType=pages|blogs[&cultureName=…]
 */
async function openDesignerFromLink() {
  const params = new URLSearchParams(window.location.search);
  const storeId = params.get("storeId");
  const group = params.get("designGroup");
  const path = params.get("designPath");
  if (!storeId || (!group && !path)) return;
  await router.isReady();
  await nextTick();
  if (group) {
    openBlade({ name: "PageBuilderDesigner", param: group });
    return;
  }
  const query: Record<string, string> = { storeId, path: path!, type: params.get("designType") || "pages" };
  const cultureName = params.get("cultureName");
  if (cultureName) query.cultureName = cultureName;
  openBlade({ name: "PageBuilderDesigner", query });
}

onMounted(async () => {
  try {
    if (isAuthenticated.value) {
      isReady.value = true;
      refreshMenuBadges();
      await openDesignerFromLink();
    }
  } catch (e) {
    console.log(e);
    throw e;
  }
});

console.debug(`Initializing App`);
</script>

<style lang="scss">
@use "./../styles/index.scss";
</style>
