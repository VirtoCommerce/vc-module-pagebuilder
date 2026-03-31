export type SectionPropertyType = string|number|boolean|SectionModel;

export interface SectionModel {
    id: string;
    type: string;
    hidden: boolean;

    blocks: SectionModel[];

    [key: string]: SectionPropertyType|SectionPropertyType[];
}
