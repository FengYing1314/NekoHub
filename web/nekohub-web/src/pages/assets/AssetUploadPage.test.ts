import { defineComponent, h } from 'vue';
import { flushPromises, mount } from '@vue/test-utils';
import { createPinia } from 'pinia';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AssetUploadPage from './AssetUploadPage.vue';
import zhCN from '../../locales/zh-CN';

const api = vi.hoisted(() => ({ listAssetStorageTargets: vi.fn(), uploadAsset: vi.fn() }));
vi.mock('../../api/assets/assets.api', () => api);
vi.mock('naive-ui', () => {
  const container = defineComponent({ setup: (_, { slots }) => () => h('div', slots.default?.()) });
  return {
    NAlert: container, NCard: container, NDescriptions: container, NDescriptionsItem: container,
    NForm: container, NFormItem: container, NSpace: container, NSwitch: container,
    NInput: defineComponent({ props: ['value', 'placeholder'], setup: (props) => () => h('textarea', { placeholder: props.placeholder }) }),
    NSelect: defineComponent({ props: ['options', 'value'], emits: ['update:value'], setup: (props, { emit }) => () => h('select', {
      value: props.value, onChange: (event: Event) => emit('update:value', (event.target as HTMLSelectElement).value),
    }, props.options.map((option: { value: string; label: string }) => h('option', { value: option.value }, option.label))) }),
    NButton: defineComponent({ props: ['disabled'], emits: ['click'], setup: (props, { slots, emit }) => () => h('button', {
      disabled: props.disabled, onClick: () => emit('click'),
    }, slots.default?.()) }),
    useMessage: () => ({ warning: vi.fn(), success: vi.fn(), error: vi.fn() }),
  };
});

async function mountUpload() {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/assets/upload', component: AssetUploadPage }] });
  await router.push('/assets/upload');
  await router.isReady();
  const wrapper = mount(AssetUploadPage, { global: { plugins: [createPinia(), router,
    createI18n({ legacy: false, locale: 'zh-CN', messages: { 'zh-CN': zhCN } })] } });
  await flushPromises();
  const input = wrapper.get('input[type="file"]');
  Object.defineProperty(input.element, 'files', { value: [new File(['image'], 'cat.png', { type: 'image/png' })] });
  await input.trigger('change');
  return wrapper;
}

describe('asset upload storage targets', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.uploadAsset.mockResolvedValue({ id: 'asset-1' });
  });

  it('recognizes a runtime GitHub default without provider management permission', async () => {
    api.listAssetStorageTargets.mockResolvedValue([{ id: null, name: 'Runtime GitHub', displayName: null,
      providerType: 'github-repo', isDefault: true }]);
    const wrapper = await mountUpload();
    expect(api.listAssetStorageTargets).toHaveBeenCalledOnce();
    expect(wrapper.text()).toContain('Runtime GitHub');
    expect(wrapper.findAll('textarea')).toHaveLength(3);
    const submit = wrapper.findAll('button').find((button) => button.text() === zhCN.asset.upload.submit)!;
    expect(submit.attributes('disabled')).toBeUndefined();
    await submit.trigger('click');
    expect(api.uploadAsset).toHaveBeenCalledWith(expect.objectContaining({ storageProviderProfileId: undefined }));
    wrapper.unmount();
  });

  it('requires explicit target selection when the configured default is unavailable', async () => {
    api.listAssetStorageTargets.mockResolvedValue([{ id: 'profile-1', name: 'Alternative', displayName: null,
      providerType: 'local', isDefault: false }]);
    const wrapper = await mountUpload();
    const submit = wrapper.findAll('button').find((button) => button.text() === zhCN.asset.upload.submit)!;
    expect(submit.attributes('disabled')).toBeDefined();
    expect(wrapper.text()).toContain('默认存储不可用');
    await wrapper.get('select').setValue('profile-1');
    expect(submit.attributes('disabled')).toBeUndefined();
    await submit.trigger('click');
    expect(api.uploadAsset).toHaveBeenCalledWith(expect.objectContaining({ storageProviderProfileId: 'profile-1' }));
    wrapper.unmount();
  });
});
