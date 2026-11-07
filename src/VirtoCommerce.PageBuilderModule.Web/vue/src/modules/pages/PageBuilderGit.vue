<template>
  <VcBlade
    :title="$t('PAGE_BUILDER.GIT.TITLE')"
    :subtitle="param"
    :toolbar-items="toolbar"
    :loading="busy"
  >
    <div
      v-if="status"
      class="pb-git"
    >
      <VcBanner
        v-if="status.error"
        variant="danger"
        icon="lucide-triangle-alert"
      >
        {{ status.error }}
      </VcBanner>

      <template v-if="view === 'setup'">
        <GitCard
          icon="lucide-history"
          :title="$t('PAGE_BUILDER.GIT.TITLE')"
          :status="$t('PAGE_BUILDER.GITHUB.NOT_CONNECTED')"
          status-variant="warning"
        >
          <template #actions>
            <VcButton
              size="sm"
              icon="lucide-history"
              @click="openConnection"
            >
              {{ $t("PAGE_BUILDER.GITHUB.WIDGET_OFF") }}
            </VcButton>
          </template>
        </GitCard>
        <p class="pb-git__note">{{ $t("PAGE_BUILDER.GIT.SETUP.INTRO") }} {{ $t("PAGE_BUILDER.GIT.CONNECT_FIRST") }}</p>
      </template>

      <template v-else>
        <GitCard
          icon="lucide-history"
          :title="status.repository ?? ''"
          :href="status.url"
          :subtitle="$t('PAGE_BUILDER.GIT.REPOSITORY')"
          :status="$t(status.enabled ? 'PAGE_BUILDER.GIT.ON' : 'PAGE_BUILDER.GIT.OFF')"
          :status-variant="status.enabled ? 'success' : 'info'"
        >
          <div v-if="!status.url">
            <dt>{{ $t("PAGE_BUILDER.GIT.REPOSITORY") }}</dt>
            <dd>{{ $t("PAGE_BUILDER.GIT.NOT_CREATED") }}</dd>
          </div>
          <div>
            <dt>{{ $t("PAGE_BUILDER.GIT.BRANCH") }}</dt>
            <dd>{{ status.baseBranch }}</dd>
          </div>
          <div v-if="status.url && status.lastCommitSha">
            <dt>{{ $t("PAGE_BUILDER.GIT.LAST_CHANGE") }}</dt>
            <dd>
              <a
                :href="`${status.url}/commit/${status.lastCommitSha}`"
                target="_blank"
                rel="noopener"
                ><code>{{ status.lastCommitSha.slice(0, 7) }}</code></a
              >
            </dd>
          </div>
          <template
            v-if="!status.enabled || view === 'error'"
            #actions
          >
            <VcButton
              v-if="!status.enabled"
              size="sm"
              icon="lucide-history"
              :disabled="busy"
              @click="connect"
            >
              {{ $t("PAGE_BUILDER.GIT.TURN_ON") }}
            </VcButton>
            <VcButton
              v-if="view === 'error'"
              variant="secondary"
              size="sm"
              icon="lucide-github"
              @click="openConnection"
            >
              {{ $t("PAGE_BUILDER.GIT.OPEN_GITHUB") }}
            </VcButton>
          </template>
        </GitCard>
        <p
          v-if="view !== 'error'"
          class="pb-git__note"
        >
          {{ $t(`PAGE_BUILDER.GIT.HINT.${view.toUpperCase()}`, { repository: status.repository }) }}
        </p>
      </template>

      <VcBanner
        v-if="actionError"
        variant="danger"
        icon="lucide-triangle-alert"
      >
        {{ actionError }}
      </VcBanner>
    </div>
  </VcBlade>
</template>

<script lang="ts" setup>
// A site's version history (param = store id): the repository its pages are kept in, and the switch. The GitHub
// connection itself is the platform's, set up once on the Sites panel (PageBuilderGitHub).
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useBlade, usePopup, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcButton } from "@vc-shell/framework/ui";
import GitCard from "../page-builder/components/GitCard.vue";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";
import { siteGitUrl, siteGitView, type SiteGitStatus } from "../widgets/siteGit";

defineBlade({
  name: "PageBuilderGit",
  url: "/page-builder-git",
  permissions: "store:read",
});

const { param, callParent, openBlade } = useBlade();
const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();

const status = ref<SiteGitStatus>();
const busy = ref(false);
const actionError = ref<string>();
const view = computed(() => (status.value ? siteGitView(status.value) : "setup"));

async function request(action: "" | "/connect" | "/disconnect", method = "GET"): Promise<SiteGitStatus> {
  const response = await fetch(siteGitUrl(param.value!, action), { method, headers: await getAuthHeaders() });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error((body as { error?: string }).error ?? `${response.status} ${response.statusText}`);
  return body as SiteGitStatus;
}

/** Runs a request, shows its answer, and lets the site page catch up after a change. */
async function run(action: () => Promise<SiteGitStatus>, changed = false) {
  if (!param.value) return;
  busy.value = true;
  actionError.value = undefined;
  try {
    status.value = await action();
    if (changed) await callParent("reload").catch(() => undefined);
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : String(error);
  } finally {
    busy.value = false;
  }
}

const refresh = () => run(() => request(""));
const connect = () => run(() => request("/connect", "POST"), true);

async function disconnect() {
  if (await showConfirmation(t("PAGE_BUILDER.GIT.CONFIRM_OFF"))) await run(() => request("/disconnect", "POST"), true);
}

/** The platform's GitHub connection; this site's status again once it closes */
const openConnection = () => openBlade({ name: "PageBuilderGitHub", onClose: refresh });

const toolbar = computed<IBladeToolbar[]>(() => [
  {
    id: "test",
    title: t("PAGE_BUILDER.GIT.TEST"),
    icon: "lucide-refresh-cw",
    disabled: busy.value,
    clickHandler: refresh,
  },
  {
    id: "on",
    title: t("PAGE_BUILDER.GIT.TURN_ON"),
    icon: "lucide-history",
    isVisible: view.value === "off" || view.value === "missing",
    disabled: busy.value,
    clickHandler: connect,
  },
  {
    id: "off",
    title: t("PAGE_BUILDER.GIT.TURN_OFF"),
    icon: "lucide-circle-off",
    isVisible: !!status.value?.enabled,
    disabled: busy.value,
    clickHandler: disconnect,
  },
]);

onMounted(refresh);
</script>

<style lang="scss" scoped>
.pb-git {
  // a compact panel even when the blade stretches over the workspace
  max-width: 680px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  // the blade's content is 16px; the shell's text, buttons and fields are 14px
  font-size: 14px;
  line-height: 20px;

  a {
    color: var(--link-text-color);

    &:hover {
      color: var(--link-text-color-hover);
    }
  }

  &__note {
    margin: 0;
    font-size: 13px;
    color: var(--neutrals-500);
  }
}
</style>
