import { onBeforeUnmount, ref, watch, type Ref } from "vue";
import { debounce } from "lodash-es";
import { searchSharedComponents, type SharedComponent } from "./api";

export function useSharedComponentSearch(storeId: () => string | undefined, keyword: Ref<string | undefined>) {
  const components = ref<SharedComponent[]>([]);
  const loading = ref(true);
  const failed = ref(false);

  let searches = 0;
  const load = debounce(async () => {
    const request = ++searches;
    const store = storeId();
    const found = store ? await searchSharedComponents(store, keyword.value).catch(() => undefined) : [];
    // An earlier search that answers last must not replace a later one's results.
    if (request !== searches) return;
    components.value = found ?? [];
    failed.value = !found;
    loading.value = false;
  }, 250);
  watch([keyword, storeId], load, { immediate: true });
  onBeforeUnmount(() => load.cancel());

  return { components, loading, failed, reload: load };
}
