import { vi } from "vitest";

// What the designer uses of @vc-shell/framework, as test doubles: the toasts and confirmations are recorded, the API
// client has a token (the platform is the fake behind `fetch`), the user has every permission.

export const notification = { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() };

/** What the next confirmations answer */
export const popup = { confirm: true, showConfirmation: vi.fn(async (_message: string) => popup.confirm) };
export const usePopup = () => ({ showConfirmation: popup.showConfirmation });

export const TEST_TOKEN = "test-token";
export const useApiClient = (_client: unknown) => ({ getApiClient: async () => ({ authToken: TEST_TOKEN }) });

export const permissions = { has: (_permission: string | string[]) => true };
export const usePermissions = () => ({ hasAccess: (permission: string | string[]) => permissions.has(permission) });

export function resetVcShell() {
  for (const fn of Object.values(notification)) fn.mockClear();
  popup.confirm = true;
  popup.showConfirmation.mockClear();
  permissions.has = () => true;
}
