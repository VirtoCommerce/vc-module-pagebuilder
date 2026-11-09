import { vi } from "vitest";
import { ref } from "vue";

// What the designer uses of @vc-shell/framework, as test doubles: the toasts and confirmations are recorded, the API
// client has a token (the platform is the fake behind `fetch`), the user has every permission.

export const notification = { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() };

/** What the next confirmations answer */
export const popup = { confirm: true, showConfirmation: vi.fn(async (_message: string) => popup.confirm) };
export const usePopup = () => ({ showConfirmation: popup.showConfirmation });

export const TEST_TOKEN = "test-token";
export const apiClient: { authToken: string; getGroup: (id: string) => Promise<unknown> } = { authToken: TEST_TOKEN, getGroup: vi.fn() };
export const useApiClient = (_client: unknown) => ({ getApiClient: async () => apiClient });

export const permissions = { has: (_permission: string | string[]) => true };
export const usePermissions = () => ({ hasAccess: (permission: string | string[]) => permissions.has(permission) });

/** The blade the component under test runs in */
export const blade = { param: ref<string | undefined>(), query: ref<Record<string, string> | undefined>() };
export const useBlade = () => ({
  id: ref("blade"),
  param: blade.param,
  query: blade.query,
  callParent: vi.fn(async () => undefined),
  openBlade: vi.fn(),
  onBeforeClose: vi.fn(),
});
export const useBladeStack = () => ({ blades: ref([]), closeChildren: vi.fn() });
export const useBeforeUnload = vi.fn();
export const useResponsive = () => ({ isMobile: ref(false) });
export const hotkey = { mod: new Proxy({}, { get: (_target, key) => `mod+${String(key)}` }) as Record<string, string> };
export const BladeMaximizedKey = Symbol("BladeMaximized");
export const BladeStackKey = Symbol("BladeStack");

export function resetVcShell() {
  for (const fn of Object.values(notification)) fn.mockClear();
  popup.confirm = true;
  popup.showConfirmation.mockClear();
  permissions.has = () => true;
  blade.param.value = undefined;
  blade.query.value = undefined;
  apiClient.getGroup = vi.fn();
}
