const isBrowser = typeof window !== 'undefined';

export function readFromLocalStorage<T>(key: string): T | null {
  if (!isBrowser) {
    return null;
  }

  const rawValue = window.localStorage.getItem(key);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as T;
  } catch {
    return null;
  }
}

export function writeToLocalStorage<T>(key: string, value: T): void {
  if (!isBrowser) {
    return;
  }

  window.localStorage.setItem(key, JSON.stringify(value));
}
