import { computed, onBeforeUnmount, ref, shallowRef } from "vue";
import { useLocalStorage } from "@vueuse/core";
import { usePermissions } from "@vc-shell/framework";
import { impersonate, loadPreviewUsers } from "./api";
import { ANONYMOUS, authMessage, type AuthMessage } from "./previewMessages";
import { IMPERSONATE_PERMISSION, TOKEN_MARGIN_MS, usableToken, type PreviewToken, type PreviewUser } from "./previewUsers";

/** The shortest wait before a token is renewed, so a short-lived one isn't asked for in a loop */
const MIN_RENEW_MS = 10_000;

/**
 * Whom the preview renders as: an anonymous visitor, or one of the accounts the store lists (remembered per store).
 * `message` is the `auth` the preview gets after every `loaded`; `refresh` gets a token for it first, and a token is
 * renewed before it expires. A grant that fails goes back to the anonymous visitor, through `onFailed`.
 */
export function usePreviewUser(options: { send: (message: AuthMessage) => void; onFailed: (user: PreviewUser | undefined) => void }) {
  const { hasAccess } = usePermissions();
  /** the editor may impersonate: without it the control hides */
  const allowed = computed(() => hasAccess(IMPERSONATE_PERMISSION));
  const users = shallowRef<PreviewUser[]>([]);
  const userId = ref<string | null>(null);
  const user = computed(() => users.value.find((x) => x.id === userId.value));
  const picks = useLocalStorage<Record<string, string>>("vc-page-designer-preview-as", {});

  let storeId: string | undefined;
  let token: PreviewToken | undefined;
  let message: AuthMessage = ANONYMOUS;
  let requests = 0;
  let renewal: ReturnType<typeof setTimeout> | undefined;
  onBeforeUnmount(() => clearTimeout(renewal));

  async function load(store: string) {
    storeId = store;
    users.value = allowed.value ? await loadPreviewUsers(store).catch(() => []) : [];
    const saved = picks.value[store];
    userId.value = saved && users.value.some((x) => x.id === saved) ? saved : null;
    // a preview that connected meanwhile renders as the anonymous visitor
    if (userId.value && (await refresh())) options.send(message);
  }

  /** Makes `message` the one for the user picked, with a token valid for a while yet; true when it is still the latest */
  async function refresh(): Promise<boolean> {
    const request = ++requests;
    const id = userId.value;
    clearTimeout(renewal);
    let next: AuthMessage = ANONYMOUS;
    if (id) {
      try {
        token = usableToken(token, id, Date.now()) ?? (await impersonate(id));
        next = authMessage(id, token.accessToken);
      } catch (e) {
        if (request !== requests) return false;
        console.warn("[page-builder] The preview could not be shown as the user", e);
        options.onFailed(user.value);
        choose(null);
      }
    }
    if (request !== requests) return false;
    message = next;
    if (id && token && next !== ANONYMOUS) {
      renewal = setTimeout(() => void renew(), Math.max(token.expiresAt - TOKEN_MARGIN_MS - Date.now(), MIN_RENEW_MS));
    }
    return true;
  }

  async function renew() {
    if (await refresh()) options.send(message);
  }

  function choose(id: string | null) {
    userId.value = id;
    if (storeId) picks.value = { ...picks.value, [storeId]: id ?? "" };
  }

  /** Renders the preview as the account (null: an anonymous visitor) */
  async function pick(id: string | null) {
    if (id === userId.value) return;
    choose(id);
    if (await refresh()) options.send(message);
  }

  return { allowed, users, userId, user, load, refresh, pick, message: () => message };
}
