import { createPinia, setActivePinia } from 'pinia';
import { mount } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import LoginPage from './LoginPage.vue';
import zhCN from '../../locales/zh-CN';
import { useAppConfigStore } from '../../stores/app-config';

vi.mock('naive-ui', () => ({
  NButton: {
    emits: ['click'],
    template: '<button @click="$emit(\'click\')"><slot /></button>',
  },
  NCard: {
    template: '<div><slot /></div>',
  },
  NForm: {
    template: '<form><slot /></form>',
  },
  NFormItem: {
    template: '<div><slot /></div>',
  },
  NInput: {
    props: ['value'],
    emits: ['update:value', 'keyup.enter'],
    template: '<input :value="value" @input="$emit(\'update:value\', $event.target.value)" />',
  },
  NSpace: {
    template: '<div><slot /></div>',
  },
  NText: {
    template: '<span><slot /></span>',
  },
  useMessage: () => ({
    error: vi.fn(),
    success: vi.fn(),
    warning: vi.fn(),
  }),
}));

vi.mock('../../stores/auth.store', () => ({
  useAuthStore: () => ({
    login: vi.fn(),
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

describe('LoginPage', () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
  });

  it('opens the config modal from the login page', async () => {
    const pinia = createPinia();
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/login', component: LoginPage },
      ],
    });

    await router.push('/login');
    await router.isReady();

    const wrapper = mount(LoginPage, {
      global: {
        plugins: [pinia, router, createTestI18n()],
      },
    });

    const appConfigStore = useAppConfigStore(pinia);
    expect(appConfigStore.isConfigModalOpen).toBe(false);

    await wrapper.get('button').trigger('click');

    expect(appConfigStore.isConfigModalOpen).toBe(true);
  });
});
