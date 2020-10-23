import { template } from './utils';

describe('string templates', () => {
    it('Named arguments are replaced', () => {
        var result = template('Hello {{name}}, how are you?', { name: 'Mark' });
        expect(result).toEqual('Hello Mark, how are you?');
    });

    it('Named arguments at the start of strings are replaced', () => {
        var result = template('{{likes}} people have liked this', {
            likes: 123
        });
        expect(result).toEqual('123 people have liked this');
    });

    it('Named arguments at the end of string are replaced', () => {
        var result = template('Please respond by {{date}}', {
            date: '01/01/2015'
        });
        expect(result).toEqual('Please respond by 01/01/2015');
    });

    it('Multiple named arguments are replaced', () => {
        var result = template('Hello {{name}}, you have {{emails}} new messages', {
            name: 'Anna',
            emails: 5
        });
        expect(result).toEqual('Hello Anna, you have 5 new messages');
    });

    it('Missing named arguments become 0 characters', () => {
        var result = template('Hello{{name}}, how are you?', {});
        expect(result).toEqual('Hello, how are you?');
    });

    it('Named arguments can be escaped', () => {
        var result = template('Hello {{{name}}}, how are you?', { name: 'Mark' });
        expect(result).toEqual('Hello {{name}}, how are you?');
    });

    it('Curly brackets not processed', () => {
        var result = template('Hello {name}, how are you?', { name: 'Mark' });
        expect(result).toEqual('Hello {name}, how are you?');
    });

    it('Array arguments are replaced', () => {
        var result = template('Hello {{0}}, how are you?', ['Mark']);
        expect(result).toEqual('Hello Mark, how are you?');
    });

    it('Array arguments at the start of strings are replaced', () => {
        var result = template('{{0}} people have liked this', [123]);
        expect(result).toEqual('123 people have liked this');
    });

    it('Array arguments at the end of string are replaced', () => {
        var result = template('Please respond by {{0}}', ['01/01/2015']);
        expect(result).toEqual('Please respond by 01/01/2015');
    });

    it('Multiple array arguments are replaced', () => {
        var result = template('Hello {{0}}, you have {{1}} new messages', [
            'Anna',
            5
        ]);
        expect(result).toEqual('Hello Anna, you have 5 new messages');
    });

    it('Missing array arguments become 0 characters', () => {
        var result = template('Hello{{0}}, how are you?', []);
        expect(result).toEqual('Hello, how are you?');
    });

    it('Array arguments can be escaped', () => {
        var result = template('Hello {{{0}}}, how are you?', ['Mark']);
        expect(result).toEqual('Hello {{0}}, how are you?');
    });

    it('Array arguments with single bracket can be escaped', () => {
        var result = template('Hello {0}, how are you?', ['Mark']);
        expect(result).toEqual('Hello {0}, how are you?');
    });

    it('Array keys are not accessible', () => {
        var result = template('Function{{splice}}', []);
        expect(result).toEqual('Function');
    });

    it('Listed arguments are replaced', () => {
        var result = template('Hello {{0}}, how are you?', 'Mark');
        expect(result).toEqual('Hello Mark, how are you?');
    });

    it('Listed arguments at the start of strings are replaced', () => {
        var result = template('{{0}} people have liked this', 123);
        expect(result).toEqual('123 people have liked this');
    });

    it('Listed arguments at the end of string are replaced', () => {
        var result = template('Please respond by {{0}}', '01/01/2015');
        expect(result).toEqual('Please respond by 01/01/2015');
    });

    it('Multiple listed arguments are replaced', () => {
        var result = template('Hello {{0}}, you have {{1}} new messages', 'Anna', 5);
        expect(result).toEqual('Hello Anna, you have 5 new messages');
    });

    it('Missing listed arguments become 0 characters', () => {
        var result = template('Hello{{1}}, how are you?', 'no');
        expect(result).toEqual('Hello, how are you?');
    });

    it('Listed arguments can be escaped', () => {
        var result = template('Hello {{{0}}}, how are you?', 'Mark');
        expect(result).toEqual('Hello {{0}}, how are you?');
    });

    it('Listed arguments with single bracket can be escaped', () => {
        var result = template('Hello {0}, how are you?', 'Mark');
        expect(result).toEqual('Hello {0}, how are you?');
    });

    it('Allow null data', () => {
        var result = template('Hello{{0}}', null);
        expect(result).toEqual('Hello');
    });

    it('Allow undefined data', () => {
        var result1 = template('Hello{{0}}');
        var result2 = template('Hello{{0}}', undefined);
        expect(result1).toEqual('Hello');
        expect(result2).toEqual('Hello');
    });

    it('Null keys become 0 characters', () => {
        var result1 = template('Hello{{name}}', { name: null });
        var result2 = template('Hello{{0}}', [null]);
        var result3 = template('Hello{{0}}{{1}}{{2}}', null, null, null);
        expect(result1).toEqual('Hello');
        expect(result2).toEqual('Hello');
        expect(result3).toEqual('Hello');
    });

    it('Undefined keys become 0 characters', () => {
        var result1 = template('Hello{{firstName}}{{lastName}}', { name: undefined });
        var result2 = template('Hello{{0}}{{1}}', [undefined]);
        var result3 = template('Hello{{0}}{{1}}{{2}}', undefined, undefined);
        expect(result1).toEqual('Hello');
        expect(result2).toEqual('Hello');
        expect(result3).toEqual('Hello');
    });

    it('Works across multline strings', () => {
        var result1 = template('{{zero}}\n{{one}}\n{{two}}', {
            zero: 'A',
            one: 'B',
            two: 'C'
        });
        var result2 = template('{{0}}\n{{1}}\n{{2}}', ['A', 'B', 'C']);
        var result3 = template('{{0}}\n{{1}}\n{{2}}', 'A', 'B', 'C');
        expect(result1).toEqual('A\nB\nC');
        expect(result2).toEqual('A\nB\nC');
        expect(result3).toEqual('A\nB\nC');
    });

    it('Allow multiple references', () => {
        var result1 = template('{{a}}{{b}}{{c}}\n{{a}}{{b}}{{c}}\n{{a}}{{b}}{{c}}', {
            a: 'one',
            b: 'two',
            c: 'three'
        });
        var result2 = template('{{0}}{{1}}{{2}}\n{{0}}{{1}}{{2}}\n{{0}}{{1}}{{2}}', [
            'one',
            'two',
            'three'
        ]);
        var result3 = template('{{0}}{{1}}{{2}}\n{{0}}{{1}}{{2}}\n{{0}}{{1}}{{2}}',
            'one',
            'two',
            'three');
        expect(result1).toEqual('onetwothree\nonetwothree\nonetwothree');
        expect(result2).toEqual('onetwothree\nonetwothree\nonetwothree');
        expect(result3).toEqual('onetwothree\nonetwothree\nonetwothree');
    });

    it('Template string without arguments', () => {
        var result = template('Hello, how are you?');
        expect(result).toEqual('Hello, how are you?');
    });

    it('Template string with underscores', () => {
        var result = template('Hello {{FULL_NAME}}, how are you?', {
            FULL_NAME: 'James Bond'
        });
        expect(result).toEqual('Hello James Bond, how are you?');
    });

    it('Prod example', () => {
        var result = template('{"operationName":null,"variables":{},"query":"{products(storeId:\"odt\",filter:\"sku:{{block.product.__searchQuery}}\",userId:\"\"){items{id,name,imgSrc,descriptions{reviewType,content}prices{actual{formattedAmount}}}}}"}', { block: { product: { __searchQuery: 'query' } } });
        expect(result).toEqual('{"operationName":null,"variables":{},"query":"{products(storeId:\"odt\",filter:\"sku:query\",userId:\"\"){items{id,name,imgSrc,descriptions{reviewType,content}prices{actual{formattedAmount}}}}}"}')
    })
});
