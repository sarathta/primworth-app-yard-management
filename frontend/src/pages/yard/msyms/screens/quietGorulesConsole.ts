/** GoRules / React Flow / Ant Design log noise we cannot change in our code. */
const IGNORE = [
  '[DEPRECATED] Use `createWithEqualityFn`',
  "[React Flow]: It looks like you've created a new nodeTypes",
  'Accessing element.ref was removed in React 19',
];

function skip(args: unknown[]): boolean {
  return args.some(arg => typeof arg === 'string' && IGNORE.some(msg => arg.includes(msg)));
}

export function quietGorulesConsole(): () => void {
  const warn = console.warn;
  const error = console.error;
  console.warn = ((...args: unknown[]) => { if (!skip(args)) warn.apply(console, args); }) as typeof console.warn;
  console.error = ((...args: unknown[]) => { if (!skip(args)) error.apply(console, args); }) as typeof console.error;
  return () => {
    console.warn = warn;
    console.error = error;
  };
}
