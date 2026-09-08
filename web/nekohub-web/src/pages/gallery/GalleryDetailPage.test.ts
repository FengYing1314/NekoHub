import { flushPromises, mount } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import GalleryDetailPage from './GalleryDetailPage.vue';
import { getPublicAsset } from '../../api/public/public-assets.api';
import zhCN from '../../locales/zh-CN';

vi.mock('../../api/public/public-assets.api', () => ({ getPublicAsset: vi.fn().mockResolvedValue({
  id: 'asset-1', originalFileName: 'cat.png', publicUrl: 'https://content.example/cat.png', derivatives: [],
}) }));
afterEach(() => vi.unstubAllGlobals());

describe('public asset preview', () => {
  it('does not navigate the current page when noopener returns null', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/gallery/:id', component: GalleryDetailPage }] });
    await router.push('/gallery/asset-1');
    const wrapper = mount(GalleryDetailPage, { global: { plugins: [router,
      createI18n({ legacy: false, locale: 'zh-CN', messages: { 'zh-CN': zhCN } })] } });
    await flushPromises();
    const open = vi.fn().mockReturnValue(null);
    const assign = vi.fn();
    vi.stubGlobal('window', new Proxy(window, {
      get: (target, key) => key === 'open' ? open : key === 'location' ? { assign } : Reflect.get(target, key, target),
    }));
    const button = wrapper.findAll('button').find((item) => item.text().includes(zhCN.gallery.detail.openOriginal))!;
    await button.trigger('click');
    expect(open).toHaveBeenCalledWith('https://content.example/cat.png', '_blank', 'noopener,noreferrer');
    expect(assign).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
    wrapper.unmount();
  });
  it('retries a failed detail request without leaving the current asset', async () => {
    vi.mocked(getPublicAsset).mockRejectedValueOnce(new Error('Temporarily unavailable'));
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/gallery/:id', component: GalleryDetailPage }] });
    await router.push('/gallery/asset-1');
    const wrapper = mount(GalleryDetailPage, { global: { plugins: [router,
      createI18n({ legacy: false, locale: 'zh-CN', messages: { 'zh-CN': zhCN } })] } });
    await flushPromises();
    expect(wrapper.get('.gallery-detail-error').text()).toContain('Temporarily unavailable');
    await wrapper.get('.gallery-detail-error button').trigger('click');
    await flushPromises();
    expect(getPublicAsset).toHaveBeenLastCalledWith('asset-1');
    expect(wrapper.find('.gallery-detail-error').exists()).toBe(false);
    expect(wrapper.get('h1').text()).toBe('cat.png');
    expect(router.currentRoute.value.path).toBe('/gallery/asset-1');
    wrapper.unmount();
  });

});
