import VirtoShellFramework, { defineAppModule, notification, setMenuBadge, useUser, useLanguages } from "@vc-shell/framework";
import { createApp } from "vue";
import { router } from "./router";
import * as locales from "../src/locales";
import { RouterView } from "vue-router";
import PageBuilder, { pageCounts } from "../src/modules/page-builder";
import AssetLibrary from "../src/modules/asset-library";
import SharedComponents from "../src/modules/shared-components";
import PageBuilderDesigner from "../src/modules/pages/PageBuilderDesigner.vue";
import PageBuilderGit from "../src/modules/pages/PageBuilderGit.vue";
import PageBuilderGitHub from "../src/modules/pages/PageBuilderGitHub.vue";

const Designer = defineAppModule({ blades: { PageBuilderDesigner, PageBuilderGit, PageBuilderGitHub } });

import "@vc-shell/framework/dist/index.css";

setMenuBadge("DraftPagesList", { content: pageCounts.draft, variant: "warning" });
setMenuBadge("PendingPagesList", { content: pageCounts.pending, variant: "info" });
setMenuBadge("ActivePagesList", { content: pageCounts.active, variant: "success" });
setMenuBadge("ArchivedPagesList", { content: pageCounts.archived, variant: "secondary" });
setMenuBadge("AllPagesList", { content: pageCounts.all, variant: "primary" });

// vc-shell's useUser() reads only "vc_auth_data", while the platform admin has a cookie session and a JWT in "ls.authenticationData".
const LEGACY_AUTH_KEY = "ls.authenticationData";
const SHELL_AUTH_KEY = "vc_auth_data";

interface LegacyAuthData {
  token?: string;
  access_token?: string;
  refreshToken?: string;
  refresh_token?: string;
  expiresAt?: number;
  expires_at?: number;
  userName?: string;
}

function syncAuthFromLegacy() {
  const legacy = localStorage.getItem(LEGACY_AUTH_KEY);
  if (!legacy) {
    localStorage.removeItem(SHELL_AUTH_KEY);
    return;
  }
  try {
    const parsed = JSON.parse(legacy) as LegacyAuthData;
    const token = parsed.token ?? parsed.access_token;
    if (!token) {
      localStorage.removeItem(SHELL_AUTH_KEY);
      return;
    }
    localStorage.setItem(
      SHELL_AUTH_KEY,
      JSON.stringify({
        token,
        access_token: token,
        refresh_token: parsed.refreshToken ?? parsed.refresh_token ?? null,
        expires_at: parsed.expiresAt ?? parsed.expires_at ?? null,
      }),
    );
  } catch (e) {
    console.warn("[auth-bridge] Failed to sync legacy auth data", e);
  }
}

function readLegacyAuthData(): LegacyAuthData | null {
  const raw = localStorage.getItem(LEGACY_AUTH_KEY);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as LegacyAuthData;
  } catch {
    return null;
  }
}

function legacyTokenIsFresh(data: LegacyAuthData | null): boolean {
  return !!(data?.token && data.expiresAt && Date.now() < data.expiresAt);
}

async function obtainTokenFromCookieSession(): Promise<void> {
  try {
    const response = await fetch("/connect/token", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        "x-refresh": "true",
      },
      body: "grant_type=impersonate&scope=offline_access",
      credentials: "include",
    });
    if (!response.ok) {
      console.warn("[auth-bridge] impersonate grant failed", response.status);
      return;
    }
    const data = await response.json();
    if (!data.access_token) {
      console.warn("[auth-bridge] impersonate grant returned no access_token");
      return;
    }
    localStorage.setItem(
      LEGACY_AUTH_KEY,
      JSON.stringify({
        token: data.access_token,
        userName: data.userName,
        refreshToken: data.refresh_token,
        expiresAt: Date.now() + (data.expires_in ?? 0) * 1000,
      }),
    );
  } catch (e) {
    console.warn("[auth-bridge] impersonate grant errored", e);
  }
}

async function bridgeLegacyAuthData() {
  if (!legacyTokenIsFresh(readLegacyAuthData())) {
    await obtainTokenFromCookieSession();
  }
  syncAuthFromLegacy();
  globalThis.addEventListener("storage", (event) => {
    if (event.key === LEGACY_AUTH_KEY) {
      syncAuthFromLegacy();
    }
  });
}

// An empty OzAgentUrl setting disables the AI integration: vc-shell's aiAgentPlugin skips its install when config.url is empty.
async function fetchOzAgentUrl(): Promise<string> {
  try {
    const auth = localStorage.getItem(SHELL_AUTH_KEY);
    const token = auth ? (JSON.parse(auth) as { access_token?: string }).access_token : undefined;
    if (!token) {
      return "";
    }
    const response = await fetch(
      "/api/platform/settings/VirtoCommerce.PageBuilderModule.General.OzAgentUrl",
      { headers: { Authorization: `Bearer ${token}` } },
    );
    if (!response.ok) {
      return "";
    }
    const data = (await response.json()) as { value?: unknown; defaultValue?: unknown };
    const raw = data?.value ?? data?.defaultValue ?? "";
    return typeof raw === "string" ? raw : "";
  } catch (e) {
    console.warn("[oz-agent] failed to load OzAgentUrl setting", e);
    return "";
  }
}

async function startApp() {
  await bridgeLegacyAuthData();

  const ozAgentUrl = await fetchOzAgentUrl();

  const { loadUser } = useUser();
  await loadUser();

  const { currentLocale, setLocale } = useLanguages();

  const app = createApp(RouterView)
    .use(VirtoShellFramework, {
      router,
      i18n: {
        locale: import.meta.env.APP_I18N_LOCALE,
        fallbackLocale: import.meta.env.APP_I18N_FALLBACK_LOCALE,
      },
      aiAgent: {
        config: {
          url: ozAgentUrl,
        },
      },
    })
    .use(PageBuilder)
    .use(AssetLibrary)
    .use(SharedComponents)
    .use(Designer)
    .use(router);

  Object.entries(locales).forEach(([key, message]) => {
    app.config.globalProperties.$mergeLocaleMessage(key, message);
  });

  setLocale(currentLocale.value);

  app.config.errorHandler = (err) => {
    notification.error((err as Error).toString(), {
      timeout: 5000,
    });
  };

  await router.isReady();

  app.mount("#app");
}

startApp();
