/**
 * Polyfills that must run before any SDK code is loaded.
 *
 * Promise.withResolvers was added in ES2024. The SpacetimeDB SDK uses it
 * internally, but Hermes (React Native's JS engine) doesn't support it until
 * RN 0.76+. Importing this file first in index.ts ensures it's patched
 * before spacetimedb is required.
 */
if (typeof (Promise as any).withResolvers === 'undefined') {
  (Promise as any).withResolvers = function <T>() {
    let resolve!: (value: T | PromiseLike<T>) => void;
    let reject!:  (reason?: any) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject  = rej;
    });
    return { promise, resolve, reject };
  };
}
