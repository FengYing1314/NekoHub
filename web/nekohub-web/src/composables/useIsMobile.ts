import { onBeforeUnmount, onMounted, ref } from 'vue';

export function useIsMobile(maxWidth = 768) {
  const isMobile = ref(false);

  let mediaQuery: MediaQueryList | null = null;

  const updateMatches = (matches: boolean): void => {
    isMobile.value = matches;
  };

  const handleChange = (event: MediaQueryListEvent): void => {
    updateMatches(event.matches);
  };

  onMounted(() => {
    mediaQuery = window.matchMedia(`(max-width: ${maxWidth}px)`);
    updateMatches(mediaQuery.matches);

    if (typeof mediaQuery.addEventListener === 'function') {
      mediaQuery.addEventListener('change', handleChange);
      return;
    }

    mediaQuery.addListener(handleChange);
  });

  onBeforeUnmount(() => {
    if (!mediaQuery) {
      return;
    }

    if (typeof mediaQuery.removeEventListener === 'function') {
      mediaQuery.removeEventListener('change', handleChange);
      return;
    }

    mediaQuery.removeListener(handleChange);
  });

  return {
    isMobile,
  };
}
