import { ServerRequestDescriptor } from './server.request';
import { ValueDescriptorModel } from './value-descriptor.model';

export interface OptionsRequest extends ServerRequestDescriptor {
    group: string;
    label: string;
}