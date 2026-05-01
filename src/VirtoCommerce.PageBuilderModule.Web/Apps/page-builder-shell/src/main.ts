import VirtoShellFramework, { notification, useUser, useLanguages } from "@vc-shell/framework";
import { createApp } from "vue";
import { router } from "./router";
import * as locales from "./locales";
import { RouterView } from "vue-router";
import PageBuilder from "./modules/page-builder";

// Load required CSS
import "@vc-shell/framework/dist/index.css";

// Bridge platform auth to vc-shell. PageBuilder is embedded inside the VC platform admin, which
// uses cookie-based session login and (separately) the AngularJS Designer stores a JWT under
// "ls.authenticationData". vc-shell's useUser() reads from "vc_auth_data" only.
//
// Strategy:
//   1. If "ls.authenticationData" already has a non-expired JWT, mirror it to "vc_auth_data".
//   2. Otherwise exchange the cookie session for a JWT via /connect/token grant_type=impersonate
//      (same call the Designer uses in AppInitializator), write to BOTH keys.
//   3. Listen for cross-tab `storage` events so login / refresh / logout in the admin tab keeps
//      "vc_auth_data" in sync.
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

async function startApp() {
  await bridgeLegacyAuthData();

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
          url: import.meta.env.APP_AI_AGENT_URL,
        },
      },
    })
    // PageBuilder module initialization
    .use(PageBuilder)
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
