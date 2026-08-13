export interface SharedComponentUsagePage {
  id?: string;
  name?: string;
  permalink?: string;
  cultureName?: string;
  status?: string;
}

export interface SharedComponent {
  id: string;
  storeId: string;
  name: string;
  usageCount: number;
  usagePages: SharedComponentUsagePage[];
  createdBy?: string;
  createdDate?: Date | string;
  modifiedBy?: string;
  modifiedDate?: Date | string;
}

export interface SharedComponentSearchCriteria {
  storeId?: string;
  keyword?: string;
  skip?: number;
  take?: number;
}

export interface SharedComponentSearchResult {
  totalCount: number;
  results: SharedComponent[];
}

export interface RenameSharedComponentPayload {
  component: SharedComponent;
  name: string;
}

export interface SharedComponentActionResult {
  succeeded: boolean;
  errorMessage?: string;
}
