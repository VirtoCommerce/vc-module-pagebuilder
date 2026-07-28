import type {
  PageBuilderLinkedComponent,
  PageBuilderLinkedComponentSearchCriteria,
  PageBuilderLinkedComponentUsagePage,
} from "../../api_client/virtocommerce.pagebuildermodule";

export interface LinkedComponentUsagePage extends PageBuilderLinkedComponentUsagePage {
  id?: string;
}

export interface LinkedComponent extends Omit<PageBuilderLinkedComponent, "id" | "storeId" | "name" | "usagePages"> {
  id: string;
  storeId: string;
  name: string;
  usageCount: number;
  usagePages: LinkedComponentUsagePage[];
}

export interface LinkedComponentSearchCriteria extends PageBuilderLinkedComponentSearchCriteria {
  storeId?: string;
  keyword?: string;
  skip?: number;
  take?: number;
}

export interface LinkedComponentSearchResult {
  totalCount: number;
  results: LinkedComponent[];
}

export interface RenameLinkedComponentPayload {
  component: LinkedComponent;
  name: string;
}

export interface LinkedComponentActionResult {
  succeeded: boolean;
  errorMessage?: string;
}
