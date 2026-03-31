import { SectionModel } from "@models/document";

export interface ReorderItemsModel {
    item: SectionModel;
    parent?: SectionModel;
    currentIndex: number;
    previousIndex: number;
}
