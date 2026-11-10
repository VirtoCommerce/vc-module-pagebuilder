import { vi } from "vitest";
import { computed, ref } from "vue";

export const notification = { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() };

export const parseError = (error: unknown) => ({ message: error instanceof Error ? error.message : String(error) });

// As in the framework, a failed action shows its own error toast unless `notify: false`, and still rethrows.
export function useAsync<Payload = void, Result = void>(action: (payload?: Payload) => Promise<Result>, options?: { notify?: boolean }) {
  const loading = ref(false);
  return {
    loading: computed(() => loading.value),
    action: async (payload?: Payload) => {
      loading.value = true;
      try {
        return await action(payload);
      } catch (error) {
        const { message } = parseError(error);
        if (options?.notify !== false) notification.error(message, { notificationId: `async-error-${message}` });
        throw error;
      } finally {
        loading.value = false;
      }
    },
  };
}
export const useLoading = (...flags: { readonly value: boolean }[]) => computed(() => flags.some((x) => x.value));

export const popup = { confirm: true, showConfirmation: vi.fn(async (_message: string) => popup.confirm) };
export const usePopup = () => ({ showConfirmation: popup.showConfirmation });

export const TEST_TOKEN = "test-token";
export const apiClient: { authToken: string; getGroup: (id: string) => Promise<unknown> } = { authToken: TEST_TOKEN, getGroup: vi.fn() };
export const useApiClient = (_client: unknown) => ({ getApiClient: async () => apiClient });

export const permissions = { has: (_permission: string | string[]) => true };
export const usePermissions = () => ({ hasAccess: (permission: string | string[]) => permissions.has(permission) });

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
