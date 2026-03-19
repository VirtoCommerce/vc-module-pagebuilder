import { SectionModel } from "@models/document";

export interface ModelChangedEventArgs {
    model: SectionModel;
    changes: Partial<SectionModel>;
}
