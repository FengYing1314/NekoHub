import { createPinia, setActivePinia } from 'pinia';
import { flushPromises, mount } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import LoginPage from './LoginPage.vue';
import zhCN from '../../locales/zh-CN';
import { useAppConfigStore } from '../../stores/app-config';

const { loginMock } = vi.hoisted(() => ({ loginMock: vi.fn() }));

vi.mock('naive-ui', () => ({
  NButton: {
    props: ['attrType'],
    emits: ['click'],
    template: '<button :type="attrType || \'button\'" @click="$emit(\'click\')"><slot /></button>',
  },
  NCard: {
    template: '<div><slot /></div>',
  },
  NForm: {
    template: '<form><slot /></form>',
  },
  NFormItem: {
    props: ['feedback', 'validationStatus'],
    template: '<div><slot /><p v-if="feedback" role="alert">{{ feedback }}</p></div>',
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
    login: loginMock,
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
    loginMock.mockReset();
    localStorage.clear();
    setActivePinia(createPinia());
  });

  async function mountLogin() {
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/login', component: LoginPage }] });
    await router.push('/login');
    await router.isReady();
    return mount(LoginPage, { global: { plugins: [createPinia(), router, createTestI18n()] } });
  }

  it('shows required feedback beside each empty field and clears it as the user types', async () => {
    const wrapper = await mountLogin();
    await wrapper.get('form').trigger('submit');
    expect(wrapper.findAll('[role="alert"]').map(item => item.text())).toEqual(['请输入用户名。', '请输入密码。']);
    expect(loginMock).not.toHaveBeenCalled();
    await wrapper.findAll('input')[0]!.setValue('preview');
    expect(wrapper.findAll('[role="alert"]').map(item => item.text())).toEqual(['请输入密码。']);
    wrapper.unmount();
  });

  it('submits the form and displays server errors beside the login action', async () => {
    loginMock.mockRejectedValue(new Error('服务暂时不可用'));
    const wrapper = await mountLogin();
    await wrapper.findAll('input')[0]!.setValue('preview');
    await wrapper.findAll('input')[1]!.setValue('preview-password');
    await wrapper.get('form').trigger('submit');
    await flushPromises();
    expect(loginMock).toHaveBeenCalledWith({ username: 'preview', password: 'preview-password' });
    expect(wrapper.get('.login-error').attributes('role')).toBe('alert');
    expect(wrapper.get('.login-error').text()).toContain('服务暂时不可用');
    wrapper.unmount();
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
