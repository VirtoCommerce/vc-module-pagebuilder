/**
 * Evaluates the expressions schemas and settings carry (`visibility`, `optionsSelector`, `action.execute`,
 * `response.selector`, `{{=…}}`, `@{{…}}`) without running them as code. Schemas are written by anyone who
 * can save a theme or a page, and run in the admin's session, so only two kinds of expression are accepted:
 *
 * - a small expression language: literals, `this.…` member access, `!`, arithmetic, comparisons, `&&`, `||`,
 *   `??`, `?:`, array literals and calls to the helpers in `functions` or to a few pure string and array methods;
 * - a known form: an exact expression from a schema or settings file that needs more than that (callbacks,
 *   spreads, regular expressions), mapped to the code that does the same thing.
 *
 * Anything else is not executed: `evaluate` throws `UnsupportedExpressionError`.
 */

export class UnsupportedExpressionError extends Error {}

type Node =
  | { kind: 'literal'; value: unknown }
  | { kind: 'this' }
  | { kind: 'identifier'; name: string }
  | { kind: 'member'; object: Node; property: Node; computed: boolean; optional: boolean }
  | { kind: 'call'; callee: Node; args: Node[] }
  | { kind: 'unary'; operator: string; argument: Node }
  | { kind: 'binary'; operator: string; left: Node; right: Node }
  | { kind: 'conditional'; test: Node; consequent: Node; alternate: Node }
  | { kind: 'array'; elements: Node[] };

type Token = { type: 'number' | 'string' | 'name' | 'punct'; value: string };

const FORBIDDEN_PROPERTIES = new Set(['__proto__', 'prototype', 'constructor', '__defineGetter__', '__defineSetter__', '__lookupGetter__', '__lookupSetter__']);

const STRING_METHODS = new Set(['indexOf', 'includes', 'startsWith', 'endsWith', 'trim', 'toLowerCase', 'toUpperCase', 'split', 'slice', 'substring', 'charAt', 'replace']);
const ARRAY_METHODS = new Set(['indexOf', 'includes', 'join', 'slice']);

const PUNCTUATORS = ['===', '!==', '?.', '??', '&&', '||', '==', '!=', '<=', '>=', '+', '-', '*', '/', '%', '<', '>', '!', '?', ':', '.', ',', '(', ')', '[', ']'];

const BINARY_PRECEDENCE: Record<string, number> = {
  '??': 1, '||': 2, '&&': 3,
  '==': 4, '!=': 4, '===': 4, '!==': 4,
  '<': 5, '>': 5, '<=': 5, '>=': 5,
  '+': 6, '-': 6,
  '*': 7, '/': 7, '%': 7,
};

const LITERALS: Record<string, unknown> = { true: true, false: false, null: null, undefined: undefined };

export type ExpressionFunctions = Record<string, (...args: any[]) => unknown>;

const BUILT_IN_FUNCTIONS: ExpressionFunctions = {
  encodeURIComponent: (value: unknown) => encodeURIComponent(String(value ?? '')),
  'JSON.stringify': (value: unknown, _replacer?: unknown, space?: number) => JSON.stringify(value, null, space),
};

const KNOWN_FORMS = new Map<string, (context: any, functions: ExpressionFunctions) => unknown>();

/** Registers an exact expression that needs more than the expression language, with code that does the same. */
export function registerKnownForm(expression: string, implementation: (context: any, functions: ExpressionFunctions) => unknown): void {
  KNOWN_FORMS.set(normalize(expression), implementation);
}

export function evaluate(expression: string, context: any, functions: ExpressionFunctions = {}): unknown {
  const knownForm = KNOWN_FORMS.get(normalize(expression));
  if (knownForm) {
    return knownForm(context, functions);
  }

  return evaluateNode(parse(expression), context, { ...BUILT_IN_FUNCTIONS, ...functions });
}

/** Whether `evaluate` would run the expression at all: a known form, or one the expression language parses. */
export function isSupportedExpression(expression: string): boolean {
  if (KNOWN_FORMS.has(normalize(expression))) {
    return true;
  }
  try {
    parse(expression);
    return true;
  } catch (e) {
    if (e instanceof UnsupportedExpressionError) {
      return false;
    }
    throw e;
  }
}

function normalize(expression: string): string {
  return expression.replaceAll(/\s+/g, ' ').trim();
}

// ── parsing ────────────────────────────────────────────────────────

function tokenize(source: string): Token[] {
  const tokens: Token[] = [];
  let index = 0;

  while (index < source.length) {
    const char = source[index];

    if (/\s/.test(char)) {
      index++;
      continue;
    }

    if (/\d/.test(char) || (char === '.' && /\d/.test(source[index + 1] ?? ''))) {
      const match = /^\d*\.?\d+(?:[eE][+-]?\d+)?/.exec(source.slice(index))!;
      tokens.push({ type: 'number', value: match[0] });
      index += match[0].length;
      continue;
    }

    if (char === '"' || char === "'") {
      const [value, end] = readString(source, index);
      tokens.push({ type: 'string', value });
      index = end;
      continue;
    }

    if (/[A-Za-z_$]/.test(char)) {
      const match = /^[A-Za-z_$][\w$]*/.exec(source.slice(index))!;
      tokens.push({ type: 'name', value: match[0] });
      index += match[0].length;
      continue;
    }

    const punctuator = PUNCTUATORS.find(p => source.startsWith(p, index));
    if (!punctuator) {
      throw new UnsupportedExpressionError(`Unexpected "${char}"`);
    }
    tokens.push({ type: 'punct', value: punctuator });
    index += punctuator.length;
  }

  return tokens;
}

function readString(source: string, start: number): [string, number] {
  const quote = source[start];
  const escapes: Record<string, string> = { n: '\n', t: '\t', r: '\r', '\\': '\\', "'": "'", '"': '"' };
  let value = '';
  let index = start + 1;

  while (index < source.length && source[index] !== quote) {
    if (source[index] === '\\') {
      const escaped = source[index + 1];
      if (!(escaped in escapes)) {
        throw new UnsupportedExpressionError(`Unsupported escape "\\${escaped}"`);
      }
      value += escapes[escaped];
      index += 2;
    } else {
      value += source[index++];
    }
  }

  if (source[index] !== quote) {
    throw new UnsupportedExpressionError('Unterminated string');
  }

  return [value, index + 1];
}

function parse(source: string): Node {
  const tokens = tokenize(source);
  let position = 0;

  const peek = (): Token | undefined => tokens[position];
  const isPunct = (value: string) => peek()?.type === 'punct' && peek()?.value === value;
  const expect = (value: string) => {
    if (!isPunct(value)) {
      throw new UnsupportedExpressionError(`Expected "${value}"`);
    }
    position++;
  };

  function parseExpression(): Node {
    const test = parseBinary(0);
    if (!isPunct('?')) {
      return test;
    }
    position++;
    const consequent = parseExpression();
    expect(':');
    return { kind: 'conditional', test, consequent, alternate: parseExpression() };
  }

  function parseBinary(minPrecedence: number): Node {
    let left = parseUnary();

    for (;;) {
      const token = peek();
      const precedence = token?.type === 'punct' ? BINARY_PRECEDENCE[token.value] : undefined;
      if (precedence === undefined || precedence <= minPrecedence) {
        return left;
      }
      position++;
      left = { kind: 'binary', operator: token!.value, left, right: parseBinary(precedence) };
    }
  }

  function parseUnary(): Node {
    if (isPunct('!') || isPunct('-') || isPunct('+')) {
      const operator = tokens[position++].value;
      return { kind: 'unary', operator, argument: parseUnary() };
    }
    return parsePostfix(parsePrimary());
  }

  function parsePostfix(node: Node): Node {
    for (;;) {
      if (isPunct('.') || isPunct('?.')) {
        const optional = tokens[position++].value === '?.';
        if (optional && isPunct('[')) {
          position++;
          const property = parseExpression();
          expect(']');
          node = { kind: 'member', object: node, property, computed: true, optional };
          continue;
        }
        const name = tokens[position++];
        if (name?.type !== 'name') {
          throw new UnsupportedExpressionError('Expected a property name');
        }
        node = { kind: 'member', object: node, property: { kind: 'literal', value: name.value }, computed: false, optional };
      } else if (isPunct('[')) {
        position++;
        const property = parseExpression();
        expect(']');
        node = { kind: 'member', object: node, property, computed: true, optional: false };
      } else if (isPunct('(')) {
        position++;
        node = { kind: 'call', callee: node, args: parseList(')') };
      } else {
        return node;
      }
    }
  }

  function parseList(close: string): Node[] {
    const items: Node[] = [];
    while (!isPunct(close)) {
      items.push(parseExpression());
      if (!isPunct(close)) {
        expect(',');
      }
    }
    position++;
    return items;
  }

  function parsePrimary(): Node {
    const token = tokens[position++];

    if (!token) {
      throw new UnsupportedExpressionError('Unexpected end of expression');
    }
    if (token.type === 'number') {
      return { kind: 'literal', value: Number(token.value) };
    }
    if (token.type === 'string') {
      return { kind: 'literal', value: token.value };
    }
    if (token.type === 'name') {
      // own keys only: `in` also finds `constructor`, `toString`… on Object.prototype and would hand out Object
      if (Object.hasOwn(LITERALS, token.value)) {
        return { kind: 'literal', value: LITERALS[token.value] };
      }
      return token.value === 'this' ? { kind: 'this' } : { kind: 'identifier', name: token.value };
    }
    if (token.value === '(') {
      const inner = parseExpression();
      expect(')');
      return inner;
    }
    if (token.value === '[') {
      return { kind: 'array', elements: parseList(']') };
    }

    throw new UnsupportedExpressionError(`Unexpected "${token.value}"`);
  }

  const result = parseExpression();
  if (position < tokens.length) {
    throw new UnsupportedExpressionError(`Unexpected "${tokens[position].value}"`);
  }
  return result;
}

// ── evaluation ─────────────────────────────────────────────────────

function evaluateNode(node: Node, context: any, functions: ExpressionFunctions): unknown {
  switch (node.kind) {
    case 'literal':
      return node.value;
    case 'this':
      return context;
    case 'identifier':
      throw new UnsupportedExpressionError(`"${node.name}" is not available`);
    case 'array':
      return node.elements.map(element => evaluateNode(element, context, functions));
    case 'member': {
      const object = evaluateNode(node.object, context, functions) as any;
      if (object === null || object === undefined) {
        if (node.optional) {
          return undefined;
        }
        throw new TypeError('Cannot read a property of null or undefined');
      }
      return readProperty(object, evaluateNode(node.property, context, functions));
    }
    case 'call':
      return evaluateCall(node, context, functions);
    case 'unary':
      return evaluateUnary(node.operator, evaluateNode(node.argument, context, functions));
    case 'binary':
      return evaluateBinary(node, context, functions);
    case 'conditional':
      return evaluateNode(node.test, context, functions)
        ? evaluateNode(node.consequent, context, functions)
        : evaluateNode(node.alternate, context, functions);
  }
}

function readProperty(object: any, property: unknown): unknown {
  const key = String(property);
  if (FORBIDDEN_PROPERTIES.has(key)) {
    throw new UnsupportedExpressionError(`"${key}" is not available`);
  }
  const value = object[key];
  if (typeof value === 'function') {
    throw new UnsupportedExpressionError(`"${key}" can only be called`);
  }
  return value;
}

function evaluateUnary(operator: string, value: any): unknown {
  switch (operator) {
    case '!':
      return !value;
    case '-':
      return -value;
    default:
      return +value;
  }
}

const NOT_CALLABLE = 'Only the expression helpers and plain string and array methods can be called';

function evaluateCall(node: Extract<Node, { kind: 'call' }>, context: any, functions: ExpressionFunctions): unknown {
  const args = () => node.args.map(arg => evaluateNode(arg, context, functions));
  const callee = node.callee;

  if (callee.kind === 'identifier' && Object.hasOwn(functions, callee.name)) {
    return functions[callee.name](...args());
  }

  if (callee.kind === 'member' && !callee.computed) {
    return evaluateMethodCall(callee, args, context, functions);
  }

  throw new UnsupportedExpressionError(NOT_CALLABLE);
}

/** `JSON.stringify(…)`-style helpers by their dotted name, else a plain string or array method of the value. */
function evaluateMethodCall(callee: Extract<Node, { kind: 'member' }>, args: () => unknown[], context: any, functions: ExpressionFunctions): unknown {
  const name = String((callee.property as { value: unknown }).value);
  const helper = callee.object.kind === 'identifier' ? `${callee.object.name}.${name}` : null;

  if (helper && Object.hasOwn(functions, helper)) {
    return functions[helper](...args());
  }

  const target = evaluateNode(callee.object, context, functions) as any;
  if (isPlainMethod(target, name)) {
    const callArgs = args();
    if (name === 'replace' && callArgs.some(arg => typeof arg !== 'string')) {
      throw new UnsupportedExpressionError('"replace" takes strings only');
    }
    return target[name](...callArgs);
  }
  if ((target === null || target === undefined) && callee.optional) {
    return undefined;
  }

  throw new UnsupportedExpressionError(NOT_CALLABLE);
}

function isPlainMethod(target: unknown, name: string): boolean {
  return (typeof target === 'string' && STRING_METHODS.has(name)) || (Array.isArray(target) && ARRAY_METHODS.has(name));
}

function evaluateBinary(node: Extract<Node, { kind: 'binary' }>, context: any, functions: ExpressionFunctions): unknown {
  const left = evaluateNode(node.left, context, functions) as any;

  switch (node.operator) {
    case '&&':
      return left && evaluateNode(node.right, context, functions);
    case '||':
      return left || evaluateNode(node.right, context, functions);
    case '??':
      return left ?? evaluateNode(node.right, context, functions);
  }

  const right = evaluateNode(node.right, context, functions) as any;

  switch (node.operator) {
    case '+': return left + right;
    case '-': return left - right;
    case '*': return left * right;
    case '/': return left / right;
    case '%': return left % right;
    case '<': return left < right;
    case '>': return left > right;
    case '<=': return left <= right;
    case '>=': return left >= right;
    case '==': return left == right;
    case '!=': return left != right;
    case '===': return left === right;
    case '!==': return left !== right;
  }

  throw new UnsupportedExpressionError(`Unsupported operator "${node.operator}"`);
}
