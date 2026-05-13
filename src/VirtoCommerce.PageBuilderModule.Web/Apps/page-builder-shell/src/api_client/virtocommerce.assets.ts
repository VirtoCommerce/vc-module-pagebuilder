import { AuthApiBase } from "./virtocommerce.pagebuildermodule";

export interface AssetEntry {
  type: "folder" | "blob";
  name: string;
  url?: string;
  relativeUrl?: string;
  contentType?: string;
  size?: number;
  referencesCount?: number;
  referencePages?: AssetReferencePage[];
  modifiedDate?: string;
  createdDate?: string;
}

export interface AssetSearchResult {
  totalCount: number;
  results: AssetEntry[];
}

export interface CreateFolderPayload {
  name: string;
  parentUrl: string;
}

export interface AssetReferencesSearchCriteria {
  storeId: string;
  assetUrls: string[];
  includePages?: boolean;
}

export interface AssetReferencePage {
  id?: string;
  name?: string;
  permalink?: string;
  cultureName?: string;
  status?: string;
}

export interface AssetReference {
  assetUrl: string;
  normalizedAssetUrl: string;
  referencesCount: number;
  pages?: AssetReferencePage[];
}

export interface AssetReferencesSearchResult {
  totalCount: number;
  results: AssetReference[];
}

export class AssetsClient extends AuthApiBase {
  private http: { fetch(url: RequestInfo, init?: RequestInit): Promise<Response> };
  private baseUrl: string;

  constructor(baseUrl?: string, http?: { fetch(url: RequestInfo, init?: RequestInit): Promise<Response> }) {
    super();
    this.http = http ? http : window as typeof window;
    this.baseUrl = this.getBaseUrl("", baseUrl || "");
  }

  async search(folderUrl?: string, keyword?: string): Promise<AssetSearchResult> {
    const query = new URLSearchParams();

    if (folderUrl) {
      query.set("folderUrl", folderUrl);
    }

    if (keyword) {
      query.set("keyword", keyword);
    }
    const queryString = query.toString();

    const response = await this.sendRequest(`${this.baseUrl}/api/assets${queryString ? `?${queryString}` : ""}`, {
      method: "GET",
      headers: {},
    });

    const payload = (await response.json()) as Partial<AssetSearchResult>;

    return {
      totalCount: payload.totalCount ?? payload.results?.length ?? 0,
      results: (payload.results ?? []).map((entry) => ({
        ...entry,
        referencesCount: entry.type === "blob" ? entry.referencesCount ?? 0 : undefined,
      })),
    };
  }

  async createFolder(payload: CreateFolderPayload): Promise<void> {
    await this.sendRequest(`${this.baseUrl}/api/assets/folder`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    });
  }

  async upload(folderUrl: string, file: File): Promise<AssetEntry[]> {
    const query = new URLSearchParams({
      folderUrl,
    });

    const formData = new FormData();
    formData.append("file", file);

    const response = await this.sendRequest(`${this.baseUrl}/api/assets?${query.toString()}`, {
      method: "POST",
      headers: {},
      body: formData,
    });

    return (await response.json()) as AssetEntry[];
  }

  async delete(urls: string[]): Promise<void> {
    const query = new URLSearchParams();
    urls.forEach((url) => query.append("urls", url));

    await this.sendRequest(`${this.baseUrl}/api/assets?${query.toString()}`, {
      method: "DELETE",
      headers: {},
    });
  }

  async searchReferences(criteria: AssetReferencesSearchCriteria): Promise<AssetReferencesSearchResult> {
    const response = await this.sendRequest(`${this.baseUrl}/api/page-builder-assets/references`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(criteria),
    });

    const payload = (await response.json()) as Partial<AssetReferencesSearchResult>;

    return {
      totalCount: payload.totalCount ?? payload.results?.length ?? 0,
      results: payload.results ?? [],
    };
  }

  private async sendRequest(url: string, options: RequestInit): Promise<Response> {
    const transformedOptions = await this.transformOptions(options);
    const response = await this.http.fetch(url, transformedOptions);

    if (response.ok) {
      return response;
    }

    let message = response.statusText;

    try {
      const payload = await response.json();
      message = payload?.message ?? payload?.Message ?? message;
    } catch {
      // Keep the default status text when the response body is empty or not JSON.
    }

    throw new Error(message || "Assets request failed.");
  }
}
