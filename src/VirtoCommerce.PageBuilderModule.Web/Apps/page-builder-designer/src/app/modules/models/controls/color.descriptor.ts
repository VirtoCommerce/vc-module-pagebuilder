import { BaseControlDescriptor } from "./base-control.descriptor";

export interface ColorDescriptor extends BaseControlDescriptor {
    // outputFormat: 'auto' | 'hex' | 'rgba' | 'hsla'; // 'auto'
    // colorMode?: 'color' | 'grayscale' | 'presets'; // 'color'
    // alpha?: 'enabled' | 'disabled' | 'always' | 'forced'; // 'enabled'
    colorMode?: 'color' | 'presets'; // 'color'
    disableAlpha?: boolean;
    clearValue?: string;
    inline?: boolean;
    presets?: string[];
}
