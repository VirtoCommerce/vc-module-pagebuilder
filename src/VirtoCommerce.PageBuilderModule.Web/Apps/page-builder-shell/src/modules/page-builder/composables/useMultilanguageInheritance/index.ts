/**
 * Settings a language version inherits from its Multilanguage Page unless it overrides them.
 * Names match the backend mask (`overriddenFields`).
 */
export type InheritableField = "Permalink" | "Visibility" | "UserGroups" | "OrganizationId" | "StartDate" | "EndDate";

export const inheritableFields: InheritableField[] = [
  "Permalink",
  "Visibility",
  "UserGroups",
  "OrganizationId",
  "StartDate",
  "EndDate",
];

export const inheritableFieldLabelKey: Record<InheritableField, string> = {
  Permalink: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK",
  Visibility: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.VISIBILITY",
  UserGroups: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.USER_GROUPS",
  OrganizationId: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.ORGANIZATION",
  StartDate: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE",
  EndDate: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE",
};

export interface IInheritableSettings {
  permalink?: string;
  visibility?: boolean;
  userGroups?: string;
  organizationId?: string;
  startDate?: Date | string;
  endDate?: Date | string;
}

const propertyByField: Record<InheritableField, keyof IInheritableSettings> = {
  Permalink: "permalink",
  Visibility: "visibility",
  UserGroups: "userGroups",
  OrganizationId: "organizationId",
  StartDate: "startDate",
  EndDate: "endDate",
};

export function parseOverriddenFields(mask?: string): InheritableField[] {
  return (mask ?? "")
    .split(",")
    .map((x) => x.trim())
    .filter((x): x is InheritableField => (inheritableFields as string[]).includes(x));
}

export function isFieldOverridden(mask: string | undefined, field: InheritableField): boolean {
  return parseOverriddenFields(mask).includes(field);
}

// Empty values ("", null, undefined) are the same "not set"; dates compare by instant.
function normalize(value: unknown): string {
  if (value === undefined || value === null || value === "") {
    return "";
  }
  if (value instanceof Date) {
    return value.toISOString();
  }
  const date = typeof value === "string" && /^\d{4}-\d{2}-\d{2}T/.test(value) ? new Date(value) : null;
  return date && !Number.isNaN(date.getTime()) ? date.toISOString() : String(value);
}

/** Inheritable fields whose value differs between two versions of the settings. */
export function getChangedFields(before: IInheritableSettings, after: IInheritableSettings): InheritableField[] {
  return inheritableFields.filter(
    (field) => normalize(before[propertyByField[field]]) !== normalize(after[propertyByField[field]]),
  );
}
