<template>
  <VcBlade
    :title="$t('PAGE_BUILDER.GIT.TITLE')"
    :subtitle="$t('PAGE_BUILDER.GITHUB.ALL_SITES')"
    :toolbar-items="toolbar"
    :loading="busy"
  >
    <div
      v-if="connection"
      class="pb-gh"
    >
      <VcBanner
        v-if="connection.error"
        variant="danger"
        icon="lucide-triangle-alert"
      >
        {{ connection.error }}
      </VcBanner>

      <template v-if="connection.configured && !settingUp">
        <GitCard
          icon="lucide-github"
          :title="connection.organization ?? connection.repository ?? ''"
          :href="connection.organizationUrl"
          :subtitle="connection.organizationUrl?.replace(/^https?:\/\//, '')"
          :status="$t(connection.error ? 'PAGE_BUILDER.GITHUB.FAILING' : 'PAGE_BUILDER.GITHUB.CONNECTED')"
          :status-variant="connection.error ? 'danger' : 'success'"
        >
          <div>
            <dt>{{ $t("PAGE_BUILDER.GITHUB.APP") }}</dt>
            <dd>
              <a
                v-if="connection.appUrl"
                :href="connection.appUrl"
                target="_blank"
                rel="noopener"
                >{{ connection.appSlug }}</a
              >
              <span v-else>{{ $t("PAGE_BUILDER.GITHUB.FROM_CONFIGURATION") }}</span>
            </dd>
          </div>
          <div>
            <dt>{{ $t("PAGE_BUILDER.GITHUB.NEW_SITES") }}</dt>
            <dd>{{ $t(connection.autoCreateRepositories ? "PAGE_BUILDER.GITHUB.NEW_SITES_ON" : "PAGE_BUILDER.GITHUB.NEW_SITES_OFF") }}</dd>
          </div>
          <template
            v-if="connection.source === 'app'"
            #actions
          >
            <VcButton
              v-if="connection.installUrl"
              :variant="connection.error ? 'primary' : 'secondary'"
              size="sm"
              icon="lucide-external-link"
              @click="openTab(connection.installUrl)"
            >
              {{ $t(connection.error ? "PAGE_BUILDER.GIT.SETUP.INSTALL" : "PAGE_BUILDER.GITHUB.MANAGE_ACCESS", { organization: connection.organization }) }}
            </VcButton>
            <VcButton
              variant="secondary"
              size="sm"
              icon="lucide-github"
              @click="settingUp = true"
            >
              {{ $t("PAGE_BUILDER.GIT.SETUP.AGAIN") }}
            </VcButton>
          </template>
        </GitCard>

        <section
          v-if="connection.sites?.length"
          class="pb-gh__section"
        >
          <h4 class="pb-gh__heading">{{ $t("PAGE_BUILDER.GITHUB.SITES") }}</h4>
          <ul class="pb-gh__sites">
            <li
              v-for="site in connection.sites"
              :key="site.storeId"
              class="pb-gh__site"
            >
              <div class="pb-gh__site-main">
                <span class="pb-gh__site-name">{{ site.name }}</span>
                <a
                  v-if="site.url"
                  class="pb-gh__site-repo"
                  :href="site.url"
                  target="_blank"
                  rel="noopener"
                  >{{ site.repository }}</a
                >
              </div>
              <VcStatus :variant="site.enabled ? 'success' : 'info'">
                {{ $t(site.enabled ? "PAGE_BUILDER.GITHUB.HISTORY_ON" : "PAGE_BUILDER.GITHUB.HISTORY_OFF") }}
              </VcStatus>
            </li>
          </ul>
        </section>

        <p class="pb-gh__note">{{ $t("PAGE_BUILDER.GITHUB.CONNECTED_HINT", { organization: connection.organization ?? connection.repository }) }}</p>
      </template>

      <p
        v-else-if="connection.source === 'configuration'"
        class="pb-gh__text"
      >
        {{ $t("PAGE_BUILDER.GIT.SETUP.CONFIGURATION") }}
      </p>

      <template v-else>
        <p class="pb-gh__text">{{ $t("PAGE_BUILDER.GITHUB.INTRO") }}</p>
        <VcInput
          v-model="organization"
          :label="$t('PAGE_BUILDER.GIT.SETUP.ORGANIZATION')"
          :hint="$t('PAGE_BUILDER.GIT.SETUP.ORGANIZATION_HINT')"
          placeholder="acme-sites"
          required
          @keyup.enter="createApp"
        />
        <a
          class="pb-gh__link"
          href="https://github.com/account/organizations/new"
          target="_blank"
          rel="noopener"
          >{{ $t("PAGE_BUILDER.GIT.SETUP.NEW_ORGANIZATION") }}</a
        >
        <ol class="pb-gh__steps">
          <li>{{ $t("PAGE_BUILDER.GIT.SETUP.STEP_CREATE") }}</li>
          <li>{{ $t("PAGE_BUILDER.GIT.SETUP.STEP_INSTALL") }}</li>
          <li>{{ $t("PAGE_BUILDER.GIT.SETUP.STEP_RETURN") }}</li>
        </ol>
        <div class="pb-gh__actions">
          <VcButton
            icon="lucide-github"
            :disabled="busy || !organization.trim()"
            @click="createApp"
          >
            {{ $t("PAGE_BUILDER.GIT.SETUP.CREATE_APP") }}
          </VcButton>
          <VcButton
            v-if="connection.configured"
            variant="secondary"
            @click="settingUp = false"
          >
            {{ $t("PAGE_BUILDER.GITHUB.CANCEL") }}
          </VcButton>
        </div>
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
// The platform's GitHub connection, once for every site (from the Sites panel): the organization that holds one
// repository per site and the App the platform acts as, and the one-click setup that makes both.
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useEventListener } from "@vueuse/core";
import { type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcButton, VcInput, VcStatus } from "@vc-shell/framework/ui";
import GitCard from "../page-builder/components/GitCard.vue";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";
import { gitHubAppSetupUrl, gitHubConnectionUrl, type GitHubConnection } from "../widgets/siteGit";

defineBlade({
  name: "PageBuilderGitHub",
  url: "/page-builder-github",
  permissions: "store:read",
});

const { t } = useI18n({ useScope: "global" });

const connection = ref<GitHubConnection>();
const busy = ref(false);
const actionError = ref<string>();
const settingUp = ref(false);
const organization = ref("");

async function request<T>(url: string): Promise<T> {
  const response = await fetch(url, { headers: await getAuthHeaders() });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error((body as { error?: string }).error ?? `${response.status} ${response.statusText}`);
  return body as T;
}

async function refresh() {
  busy.value = true;
  actionError.value = undefined;
  try {
    connection.value = await request<GitHubConnection>(gitHubConnectionUrl);
  } catch (error) {
    actionError.value = error instanceof Error ? error.message : String(error);
  } finally {
    busy.value = false;
  }
}

/**
 * Opens GitHub's create-an-App form in a new tab, through the platform's page that posts it (the admin's own pages
 * may not post forms to other sites). The tab is opened straight away, inside the click, or the browser would block
 * it as a pop-up once the platform has answered.
 */
async function createApp() {
  if (!organization.value.trim() || busy.value) return;
  const tab = window.open("about:blank", "_blank");
  busy.value = true;
  actionError.value = undefined;
  try {
    const { url } = await request<{ url: string }>(gitHubAppSetupUrl(organization.value));
    if (tab) tab.location.href = url;
    else window.open(url, "_blank");
  } catch (error) {
    tab?.close();
    actionError.value = error instanceof Error ? error.message : String(error);
  } finally {
    busy.value = false;
  }
}

const openTab = (url?: string) => url && window.open(url, "_blank", "noopener");

// back from GitHub's tab: show where the setup got to
useEventListener(window, "focus", () => {
  if (!busy.value && (!connection.value?.configured || connection.value.error || settingUp.value)) {
    settingUp.value = false;
    refresh();
  }
});

const toolbar = computed<IBladeToolbar[]>(() => [
  {
    id: "test",
    title: t("PAGE_BUILDER.GIT.TEST"),
    icon: "lucide-refresh-cw",
    disabled: busy.value,
    clickHandler: refresh,
  },
]);

onMounted(refresh);
</script>

<style lang="scss" scoped>
.pb-gh {
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

  &__text {
    margin: 0;
    color: var(--neutrals-700);
  }

  &__actions {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
  }

  &__link {
    align-self: flex-start;
  }

  &__steps {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin: 0;
    padding-left: 20px;
    // the shell's reset takes list markers away; the order is the point here
    list-style: decimal;
    color: var(--neutrals-700);
  }

  &__note {
    margin: 0;
    font-size: 13px;
    color: var(--neutrals-500);
  }

  &__section {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  &__heading {
    margin: 0;
    font-size: 12px;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    color: var(--neutrals-500);
  }

  &__sites {
    margin: 0;
    padding: 0;
    list-style: none;
    border: 1px solid var(--neutrals-200);
    border-radius: 12px;
  }

  &__site {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 16px;

    & + & {
      border-top: 1px solid var(--neutrals-100);
    }
  }

  &__site-main {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
  }

  &__site-name {
    font-weight: 500;
    color: var(--neutrals-900);
  }

  &__site-repo {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 12px;
  }
}
</style>
