export interface LinkedComponentUsagePage {
  id?: string;
  name?: string;
  permalink?: string;
  cultureName?: string;
  status?: string;
}

export interface LinkedComponent {
  id: string;
  storeId: string;
  name: string;
  usageCount: number;
  usagePages: LinkedComponentUsagePage[];
  createdBy?: string;
  createdDate?: Date | string;
  modifiedBy?: string;
  modifiedDate?: Date | string;
}

export interface LinkedComponentSearchCriteria {
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
