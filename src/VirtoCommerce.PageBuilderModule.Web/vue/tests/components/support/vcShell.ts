import { vi } from "vitest";
import { computed, getCurrentInstance, readonly, ref, type Ref } from "vue";

export const notification = { success: vi.fn(), error: vi.fn(), warning: vi.fn(), remove: vi.fn(), update: vi.fn(), clearAll: vi.fn() };

export class DisplayableError extends Error {
  constructor(
    message: string,
    readonly details: string,
    readonly originalError: unknown,
  ) {
    super(message);
    this.name = "DisplayableError";
  }
}

// As the framework's: an NSwag ApiException shows its JSON body's message, or "<status>: <message>".
export function parseError(error: unknown): DisplayableError {
  if (error instanceof DisplayableError) return error;
  if (error instanceof Error && typeof (error as { status?: unknown }).status === "number" && typeof (error as { response?: unknown }).response === "string") {
    const { status, response } = error as Error & { status: number; response: string };
    try {
      return parseError(JSON.parse(response));
    } catch {
      return new DisplayableError(`${status}: ${error.message || "Error"}`, response || error.message, error);
    }
  }
  if (error instanceof Error && "response" in error && error.response) return parseError(error.response);
  if (error instanceof Error) return new DisplayableError(error.message, error.stack || String(error), error);
  if (typeof error === "string") {
    try {
      return parseError(JSON.parse(error));
    } catch {
      return new DisplayableError(error, error, error);
    }
  }
  if (typeof error === "object" && error !== null) {
    const shape = error as Record<string, unknown>;
    if ("status" in shape && "data" in shape) return new DisplayableError(`${shape.status}: ${shape.statusText || "Error"}`, JSON.stringify(shape.data), error);
    return new DisplayableError((shape.message as string) || "An unexpected error occurred.", (shape.stack as string) || JSON.stringify(shape), error);
  }
  return new DisplayableError("An unknown error occurred.", String(error), error);
}

const pendingToasts = new Set<ReturnType<typeof setTimeout>>();

// As in the framework, a failed action rethrows and, unless `notify: false`, toasts an error object on a later task.
export function useAsync<Payload = void, Result = void>(
  action: (payload?: Payload, ...rest: unknown[]) => Promise<Result>,
  options?: { notify?: boolean; notifyTimeout?: number },
) {
  const loading = ref(false);
  const error = ref<DisplayableError | null>(null);
  return {
    loading: readonly(loading),
    error: readonly(error),
    action: async (payload?: Payload, ...rest: unknown[]) => {
      loading.value = true;
      error.value = null;
      try {
        return await action(payload, ...rest);
      } catch (e) {
        const parsed = parseError(e);
        error.value = parsed;
        if (options?.notify !== false && e && typeof e === "object") {
          const toast = setTimeout(() => {
            pendingToasts.delete(toast);
            notification.error(parsed.message, { timeout: options?.notifyTimeout ?? 8000, notificationId: `async-error-${parsed.message.slice(0, 80)}` });
          }, 0);
          pendingToasts.add(toast);
        }
        throw e;
      } finally {
        loading.value = false;
      }
    },
  };
}
export const useLoading = (...flags: { readonly value: boolean }[]) => computed(() => flags.some((x) => x.value));

export const popup = { confirm: true, showConfirmation: vi.fn(async (_message: string | Ref<string>, _options?: { html?: boolean }) => popup.confirm) };
export const usePopup = () => ({ showConfirmation: popup.showConfirmation });

export const useApiClient = <Client>(Client: new () => Client) => ({ getApiClient: async () => new Client() });

export const permissions = { has: (_permission: string | string[]) => true };
export const usePermissions = () => ({
  hasAccess: (permission?: string | string[]) => !permission || (Array.isArray(permission) && !permission.length) || permissions.has(permission),
});

export const blade = {
  id: ref("blade"),
  param: ref<string | undefined>(),
  query: ref<Record<string, string> | undefined>(),
  options: ref<Record<string, unknown> | undefined>(),
  activeChildParam: ref<string | undefined>(),
  openBlade: vi.fn(async (_event: unknown) => undefined),
  closeSelf: vi.fn(async () => true),
  closeChildren: vi.fn(async () => undefined),
  replaceWith: vi.fn(async (_event: unknown) => undefined),
  callParent: vi.fn(async (_method: string, _args?: unknown): Promise<unknown> => undefined),
  onBeforeClose: vi.fn((_guard: () => Promise<boolean>) => undefined),
  onActivated: vi.fn((_callback: () => void) => undefined),
};
// A mounted component stands for a blade; as in the framework, called outside any component only openBlade works.
export function useBlade() {
  const outside = !getCurrentInstance();
  const inBlade = <T>(value: T) => {
    if (outside) throw new Error("[vc-shell] useBlade() was called outside a blade.");
    return value;
  };
  return {
    id: computed(() => inBlade(blade.id.value)),
    param: computed(() => inBlade(blade.param.value)),
    query: computed(() => inBlade(blade.query.value)),
    options: computed(() => inBlade(blade.options.value)),
    activeChildParam: computed(() => (outside ? undefined : blade.activeChildParam.value)),
    openBlade: blade.openBlade,
    closeSelf: () => inBlade(blade.closeSelf)(),
    closeChildren: () => inBlade(blade.closeChildren)(),
    replaceWith: (event: unknown) => inBlade(blade.replaceWith)(event),
    callParent: (method: string, args?: unknown) => inBlade(blade.callParent)(method, args),
    onBeforeClose: (guard: () => Promise<boolean>) => inBlade(blade.onBeforeClose)(guard),
    onActivated: (callback: () => void) => inBlade(blade.onActivated)(callback),
  };
}
export const useBladeStack = () => ({ blades: ref([]), closeChildren: vi.fn() });
export const useBeforeUnload = vi.fn();
export const useResponsive = () => ({ isMobile: ref(false) });

const MODIFIERS = ["mod", "ctrl", "meta", "alt", "shift"];
const hotkeyOf = (modifiers: Record<string, boolean>): unknown =>
  new Proxy({}, { get: (_target, key) => (typeof key !== "string" || key === "then" ? undefined : MODIFIERS.includes(key) ? hotkeyOf({ ...modifiers, [key]: true }) : { key, ...modifiers }) });
export const hotkey = hotkeyOf({}) as Record<string, Record<string, { key: string; mod?: boolean }>>;
export const BladeMaximizedKey = Symbol("BladeMaximized");
export const BladeStackKey = Symbol("BladeStack");

export function resetVcShell() {
  for (const toast of pendingToasts) clearTimeout(toast);
  pendingToasts.clear();
  for (const fn of Object.values(notification)) fn.mockReset();
  popup.confirm = true;
  popup.showConfirmation.mockReset();
  permissions.has = () => true;
  blade.id.value = "blade";
  blade.param.value = undefined;
  blade.query.value = undefined;
  blade.options.value = undefined;
  blade.activeChildParam.value = undefined;
  for (const fn of [blade.openBlade, blade.closeSelf, blade.closeChildren, blade.replaceWith, blade.callParent, blade.onBeforeClose, blade.onActivated]) fn.mockReset();
  useBeforeUnload.mockReset();
}
