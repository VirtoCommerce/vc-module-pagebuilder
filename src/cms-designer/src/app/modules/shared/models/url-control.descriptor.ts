import { BaseControlDescriptor, OptionModel } from '.';

export interface UrlControlDescriptor extends BaseControlDescriptor {
    styles?: OptionModel[];
    urlLabel?: string;
    openInNewTab?: boolean;
    openInNewTabLabel?: string;
    textLabel: string;
    flagLabel: string;
    styleLabel: string;
}
