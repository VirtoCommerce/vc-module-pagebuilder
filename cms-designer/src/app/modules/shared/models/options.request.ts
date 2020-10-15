import { ServerRequest } from './server.request';
import { ValueDescriptorModel } from './value-descriptor.model';

export interface OptionsRequest extends ServerRequest {
    group: string;
    label: string;
}