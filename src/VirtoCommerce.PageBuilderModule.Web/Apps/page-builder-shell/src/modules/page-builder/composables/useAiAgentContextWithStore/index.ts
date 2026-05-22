import { computed, inject, ref, watch, type ComputedRef, type Ref } from "vue";
import { useRoute } from "vue-router";
import {
  BladeDescriptorKey,
  useAiAgentContext,
  useBladeStack,
  type UseAiAgentContextOptions,
  type UseAiAgentContextReturn,
} from "@vc-shell/framework";

const STORE_SENTINEL_OBJECT_TYPE = "pagebuilder.store";

interface ContextItem {
  id: string | undefined;
  objectType: string | undefined;
  name?: string | undefined;
  [key: string]: unknown;
}

export interface UseAiAgentContextWithStoreOptions<T extends ContextItem> {
  /**
   * Реальные данные блейда. Если передан одиночный объект — он будет завёрнут
   * в массив перед мержем с store-сентинелом, что переведёт контекст в режим
   * "list" и обрежет поля элементов до {id, objectType, name} согласно
   * контракту useAiAgentContext. Подмержить полный объект "details" вместе
   * с сентинелом текущий контракт vc-shell не позволяет — это ограничение
   * фреймворка, не обёртки.
   */
  dataRef?: Ref<T | T[] | null | undefined> | ComputedRef<T | T[] | null | undefined>;
  suggestions?: UseAiAgentContextOptions<T>["suggestions"];
}

// WORKAROUND для отсутствия storeId в контракте AI-агента.
// См. docs/storeId-missing-in-ai-context.md — долгосрочный фикс это типизированное
// поле storeId в vc-shell payload + virto-oz BaseChatContext.
//
// Эта обёртка решает четыре проблемы одновременно:
//  1. Источник storeId реактивный и не зависит от createWebHashHistory: читаем
//     route.query, потом fallback на location.search.
//  2. Один писатель на блейд — vc-shell context-manager хранит ОДИН слот на блейд,
//     любые два вызова useAiAgentContext в одном блейде гоняются за него.
//     Все блейды pagebuilder должны идти через эту обёртку.
//  3. Гейтинг по активному блейду. vc-shell-овский _setContextData всегда таргетит
//     bladeGetter()?.id — т.е. ТЕКУЩИЙ активный блейд, а не "своего владельца".
//     Если писать когда блейд не активен — запись уедет в чужой слот. Поэтому
//     обновляем mergedItems только пока isActiveBlade === true.
//  4. Восстановление слота после фреймворкового onUnmounted-clobber. Когда дочерний
//     блейд закрывается, его useAiAgentContext.onUnmounted дёргает _setContextData([])
//     уже с активным родителем — и удаляет слот родителя. Когда фокус возвращается
//     к нашему блейду (isActiveBlade flips false→true), watch стреляет повторно
//     и перезаписывает sentinel в (теперь снова активный) слот.
export default function useAiAgentContextWithStore<T extends ContextItem = ContextItem>(
  options: UseAiAgentContextWithStoreOptions<T> = {},
): UseAiAgentContextReturn {
  const route = useRoute();
  const { activeBlade } = useBladeStack();
  const bladeDescriptor = inject(BladeDescriptorKey, null);

  const ownBladeId = computed(() => bladeDescriptor?.value?.id);
  const isActiveBlade = computed(
    () => ownBladeId.value != null && activeBlade.value?.id === ownBladeId.value,
  );

  const storeId = computed<string | null>(() => {
    const fromRoute = route.query.storeId;
    if (typeof fromRoute === "string" && fromRoute) return fromRoute;
    if (Array.isArray(fromRoute) && typeof fromRoute[0] === "string" && fromRoute[0]) return fromRoute[0];
    return new URLSearchParams(window.location.search).get("storeId");
  });

  const mergedItems = ref<ContextItem[]>([]);

  // Опциональный dataRef превращаем в стабильный источник для watch,
  // чтобы массив зависимостей watch имел постоянную форму.
  const dataRefSource: Ref<T | T[] | null | undefined> | ComputedRef<T | T[] | null | undefined> =
    options.dataRef ?? ref(undefined);

  watch(
    [isActiveBlade, storeId, dataRefSource],
    () => {
      if (!isActiveBlade.value) {
        // Не трогаем mergedItems: иначе useAiAgentContext.watch стрельнёт
        // и запись уедет в слот текущего активного блейда (не нашего).
        return;
      }

      const data = dataRefSource.value;
      const real: ContextItem[] =
        data == null ? [] : Array.isArray(data) ? [...data] : [data as ContextItem];

      if (!storeId.value) {
        mergedItems.value = real;
        return;
      }

      const sentinel: ContextItem = {
        id: storeId.value,
        objectType: STORE_SENTINEL_OBJECT_TYPE,
        name: `Current store: ${storeId.value}`,
      };

      mergedItems.value = [sentinel, ...real];
    },
    { immediate: true, deep: true },
  );

  return useAiAgentContext({
    dataRef: mergedItems,
    suggestions: options.suggestions,
  });
}
