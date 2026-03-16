import { afterEach, vi } from 'vitest';

afterEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
});
