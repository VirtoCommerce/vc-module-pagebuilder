/**
 * @license
 * Copyright Google LLC All Rights Reserved.
 *
 * Use of this source code is governed by an MIT-style license that can be
 * found in the LICENSE file at https://angular.dev/license
 */

// https://github.com/angular/components/blob/2857b738a95da1e2f1463e353138f1f9d60d90ee/src/cdk/drag-drop/dom/clone-node.ts

/** Creates a deep clone of an element. */
export function deepCloneNode(node: HTMLElement): HTMLElement {
    const clone = node.cloneNode(true) as HTMLElement;
    const descendantsWithId = clone.querySelectorAll('[id]');
    const nodeName = node.nodeName.toLowerCase();

    // Remove the `id` to avoid having multiple elements with the same id on the page.
    clone.removeAttribute('id');

    for (let i = 0; i < descendantsWithId.length; i++) {
        descendantsWithId[i].removeAttribute('id');
    }

    if (nodeName === 'canvas') {
        transferCanvasData(node as HTMLCanvasElement, clone as HTMLCanvasElement);
    } else if (nodeName === 'input' || nodeName === 'select' || nodeName === 'textarea') {
        transferInputData(node as HTMLInputElement, clone as HTMLInputElement);
    }

    transferData('canvas', node, clone, transferCanvasData);
    transferData('input, textarea, select', node, clone, transferInputData);
    return clone;
}

/** Matches elements between an element and its clone and allows for their data to be cloned. */
function transferData<T extends Element>(
    selector: string,
    node: HTMLElement,
    clone: HTMLElement,
    callback: (source: T, clone: T) => void,
) {
    const descendantElements = node.querySelectorAll<T>(selector);

    if (descendantElements.length) {
        const cloneElements = clone.querySelectorAll<T>(selector);

        for (let i = 0; i < descendantElements.length; i++) {
            callback(descendantElements[i], cloneElements[i]);
        }
    }
}

// Counter for unique cloned radio button names.
let cloneUniqueId = 0;

/** Transfers the data of one input element to another. */
function transferInputData(
    source: Element & { value: string },
    clone: Element & { value: string; name: string; type: string },
) {
    // Browsers throw an error when assigning the value of a file input programmatically.
    if (clone.type !== 'file') {
        clone.value = source.value;
    }

    // Radio button `name` attributes must be unique for radio button groups
    // otherwise original radio buttons can lose their checked state
    // once the clone is inserted in the DOM.
    if (clone.type === 'radio' && clone.name) {
        clone.name = `mat-clone-${clone.name}-${cloneUniqueId++}`;
    }
}

/** Transfers the data of one canvas element to another. */
function transferCanvasData(source: HTMLCanvasElement, clone: HTMLCanvasElement) {
    const context = clone.getContext('2d');

    if (context) {
        // In some cases `drawImage` can throw (e.g. if the canvas size is 0x0).
        // We can't do much about it so just ignore the error.
        try {
            context.drawImage(source, 0, 0);
        } catch { }
    }
}

/**
 * Toggles whether an element is visible while preserving its dimensions.
 * @param element Element whose visibility to toggle
 * @param enable Whether the element should be visible.
 * @param importantProperties Properties to be set as `!important`.
 * @docs-private
 */
export function toggleVisibility(
    element: HTMLElement,
    enable: boolean,
    importantProperties?: Set<string>,
) {
    const userSelect = enable ? '' : 'none';
    extendStyles(
        element.style,
        {
            position: enable ? '' : 'fixed',
            top: enable ? '' : '0',
            opacity: enable ? '' : '0',
            left: enable ? '' : '-999em',
            'touch-action': userSelect,
            '-webkit-user-drag': userSelect,
            '-webkit-tap-highlight-color': enable ? '' : 'transparent',
            'user-select': userSelect,
            '-ms-user-select': userSelect,
            '-webkit-user-select': userSelect,
            '-moz-user-select': userSelect,
        },
        importantProperties,
    );
}

/**
 * Shallow-extends a stylesheet object with another stylesheet-like object.
 * Note that the keys in `source` have to be dash-cased.
 * @docs-private
 */
export function extendStyles(
    dest: CSSStyleDeclaration,
    source: Record<string, string>,
    importantProperties?: Set<string>,
) {
    for (let key in source) {
        if (source.hasOwnProperty(key)) {
            const value = source[key];

            if (value) {
                dest.setProperty(key, value, importantProperties?.has(key) ? 'important' : '');
            } else {
                dest.removeProperty(key);
            }
        }
    }

    return dest;
}
