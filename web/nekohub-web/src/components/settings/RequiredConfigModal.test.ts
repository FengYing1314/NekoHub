import { createPinia, setActivePinia } from 'pinia';
import { flushPromises, mount } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import RequiredConfigModal from './RequiredConfigModal.vue';
import zhCN from '../../locales/zh-CN';
import { useAppConfigStore } from '../../stores/app-config';

vi.mock('naive-ui', () => ({
  NButton: {
    emits: ['click'],
    template: '<button @click="$emit(\'click\')"><slot /></button>',
  },
  NForm: {
    template: '<form><slot /></form>',
  },
  NFormItem: {
    template: '<div><slot /></div>',
  },
  NInput: {
    props: ['value'],
    emits: ['update:value'],
    template: '<input :value="value" @input="$emit(\'update:value\', $event.target.value)" />',
  },
  NModal: {
    props: ['show'],
    emits: ['update:show'],
    template: '<div data-testid="required-config-modal" :data-show="String(show)"><slot name="header" /><slot /></div>',
  },
  NSpace: {
    template: '<div><slot /></div>',
  },
  useMessage: () => ({
    error: vi.fn(),
    success: vi.fn(),
  }),
}));

function createTestI18n() {
  return createI18n({
    legacy: false,
    locale: 'zh-CN',
    messages: {
      'zh-CN': zhCN,
    },
  });
}

async function mountAtPath(path: string) {
  const pinia = createPinia();
  setActivePinia(pinia);

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/gallery', component: { template: '<div />' } },
      { path: '/assets', component: { template: '<div />' } },
    ],
  });

  await router.push(path);
  await router.isReady();

  const store = useAppConfigStore();
  store.$patch({
    apiBaseUrl: '',
    bootstrapAvailable: false,
    maxUploadSizeBytes: 0,
    allowedContentTypes: [],
    isConfigModalOpen: false,
  });

  return {
    wrapper: mount(RequiredConfigModal, {
      global: {
        plugins: [pinia, router, createTestI18n()],
      },
    }),
    store,
  };
}

describe('RequiredConfigModal', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.unstubAllGlobals();
  });

  it('does not block public gallery routes', async () => {
    const { wrapper } = await mountAtPath('/gallery');

    expect(wrapper.get('[data-testid="required-config-modal"]').attributes('data-show')).toBe('false');
  });

  it('still blocks admin routes when config is missing', async () => {
    const { wrapper } = await mountAtPath('/assets');

    expect(wrapper.get('[data-testid="required-config-modal"]').attributes('data-show')).toBe('true');
  });

  it('supports manual open on the login page and closes after a successful save', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: {
          apiKeyRequired: false,
          maxUploadSizeBytes: 1024,
          allowedContentTypes: ['image/png'],
        },
      }),
    });

    vi.stubGlobal('fetch', fetchMock);

    const { wrapper, store } = await mountAtPath('/gallery');
    store.openConfigModal();
    await flushPromises();

    expect(wrapper.get('[data-testid="required-config-modal"]').attributes('data-show')).toBe('true');

    await wrapper.get('input').setValue('https://api.example.com/');
    await wrapper.get('button').trigger('click');
    await flushPromises();

    expect(store.apiBaseUrl).toBe('https://api.example.com');
    expect(store.isConfigModalOpen).toBe(false);
    expect(fetchMock).toHaveBeenCalled();
  });
});
