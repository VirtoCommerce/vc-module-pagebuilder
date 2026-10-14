import { computed, ComputedRef, reactive, ref, Ref } from "vue";
import { useApiClient, useAsync, useLoading } from "@vc-shell/framework";
import {
  MultilanguagePage,
  MultilanguagePageBatchResult,
  MultilanguagePageClient,
  MultilanguagePageLanguage,
  PageBuilderPageClient,
  PageBuilderPageListItem,
} from "../../../../api_client/virtocommerce.pagebuildermodule";
import { PageTypes } from "../usePageBuilderList";

const { getApiClient } = useApiClient(MultilanguagePageClient);
const { getApiClient: getPageApiClient } = useApiClient(PageBuilderPageClient);

export interface UseMultilanguagePageDetailsOptions {
  id?: string;
  storeId?: string;
}

export interface IUseMultilanguagePageDetails {
  item: Ref<MultilanguagePage>;
  languages: Ref<MultilanguagePageLanguage[]>;
  isNew: ComputedRef<boolean>;
  loading: ComputedRef<boolean>;
  load: () => Promise<void>;
  save: () => Promise<MultilanguagePage>;
  remove: () => Promise<void>;
  publishAll: () => Promise<MultilanguagePageBatchResult>;
  archiveAll: () => Promise<MultilanguagePageBatchResult>;
  createLanguage: (args: { cultureName: string; copyContent: boolean }) => Promise<void>;
  link: (groupId: string) => Promise<void>;
  unlink: (groupId: string) => Promise<void>;
  markUpToDate: (groupId: string) => Promise<void>;
  findLandingPages: (cultureName: string, keyword?: string) => Promise<PageBuilderPageListItem[]>;
}

export function useMultilanguagePageDetails(options: UseMultilanguagePageDetailsOptions): IUseMultilanguagePageDetails {
  // New pages are visible to everyone (incl. anonymous) by default, like Landing Pages.
  const item = ref<MultilanguagePage>({ visibility: true, storeId: options.storeId } as MultilanguagePage);
  const languages = ref<MultilanguagePageLanguage[]>([]);
  const id = ref(options.id);

  const { action: load, loading: loadingItem } = useAsync(async () => {
    if (!id.value) {
      return;
    }

    const apiClient = await getApiClient();
    const [page, pageLanguages] = await Promise.all([apiClient.get(id.value), apiClient.getLanguages(id.value)]);
    item.value = reactive(page);
    languages.value = pageLanguages;
  });

  const { action: save, loading: saving } = useAsync(async () => {
    const apiClient = await getApiClient();
    const result = id.value ? await apiClient.update(item.value) : await apiClient.create(item.value);
    id.value = result.id;
    await load();
    return result;
  });

  const { action: remove, loading: removing } = useAsync(async () => {
    if (id.value) {
      const apiClient = await getApiClient();
      await apiClient.delete(id.value);
    }
  });

  // Batch and per-language actions refresh the language table; their result is returned to show a summary.
  function withReload<TArgs, TResult>(action: (apiClient: MultilanguagePageClient, args: TArgs) => Promise<TResult>) {
    return useAsync<TArgs, TResult>(async (args) => {
      const apiClient = await getApiClient();
      const result = await action(apiClient, args as TArgs);
      await load();
      return result;
    });
  }

  const { action: publishAll, loading: publishing } = withReload<void, MultilanguagePageBatchResult>((apiClient) =>
    apiClient.publishAll(id.value!),
  );
  const { action: archiveAll, loading: archiving } = withReload<void, MultilanguagePageBatchResult>((apiClient) =>
    apiClient.archiveAll(id.value!),
  );
  const { action: createLanguage, loading: creatingLanguage } = withReload<
    { cultureName: string; copyContent: boolean },
    void
  >(async (apiClient, args) => {
    await apiClient.createLanguage(id.value!, args.cultureName, args.copyContent);
  });
  const { action: link, loading: linking } = withReload<string, void>(async (apiClient, groupId) => {
    await apiClient.link(id.value!, groupId);
  });
  const { action: unlink, loading: unlinking } = withReload<string, void>(async (apiClient, groupId) => {
    await apiClient.unlink(groupId);
  });
  const { action: markUpToDate, loading: markingUpToDate } = withReload<string, void>(async (apiClient, groupId) => {
    await apiClient.markUpToDate(groupId);
  });

  async function findLandingPages(cultureName: string, keyword?: string): Promise<PageBuilderPageListItem[]> {
    const apiClient = await getPageApiClient();
    const result = await apiClient.searchList({
      storeId: item.value.storeId,
      pageType: PageTypes.Landing,
      languageCode: cultureName,
      keyword,
      take: 50,
    });
    return result.results ?? [];
  }

  return {
    item,
    languages,
    isNew: computed(() => !id.value),
    loading: useLoading(
      loadingItem,
      saving,
      removing,
      publishing,
      archiving,
      creatingLanguage,
      linking,
      unlinking,
      markingUpToDate,
    ),
    load,
    save: save as () => Promise<MultilanguagePage>,
    remove,
    publishAll: publishAll as () => Promise<MultilanguagePageBatchResult>,
    archiveAll: archiveAll as () => Promise<MultilanguagePageBatchResult>,
    createLanguage: createLanguage as (args: { cultureName: string; copyContent: boolean }) => Promise<void>,
    link: link as (groupId: string) => Promise<void>,
    unlink: unlink as (groupId: string) => Promise<void>,
    markUpToDate: markUpToDate as (groupId: string) => Promise<void>,
    findLandingPages,
  };
}
