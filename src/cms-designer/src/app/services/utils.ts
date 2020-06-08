
export function generateUniqueString(length: number): string {
    const characters = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_-';
    const randomChar = () => characters[Math.floor(Math.random() * characters.length)];
    const result = Array.from({ length }, randomChar).join('');
    return result;
}

export function onlyLettersAndDigits(value: string): string {
    if (!!value) {
        return value.replace(/[^a-zA-Z0-9]/g, '');
    }
    return value;
}
