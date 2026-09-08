import { flushPromises, mount } from '@vue/test-utils';
import { NPagination } from 'naive-ui';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import GalleryListPage from './GalleryListPage.vue';
import { listPublicAssets } from '../../api/public/public-assets.api';
import zhCN from '../../locales/zh-CN';
import type { PublicAssetPagedResponse } from '../../types/public-assets';

vi.mock('../../api/public/public-assets.api', () => ({ listPublicAssets: vi.fn() }));
const viewport = vi.hoisted(() => ({ isMobile: false }));
vi.mock('../../composables/useIsMobile', async () => {
  const { ref } = await import('vue');
  return { useIsMobile: () => ({ isMobile: ref(viewport.isMobile) }) };
});

const fileName = `${'a-long-original-file-name-'.repeat(8)}.png`;
const results: PublicAssetPagedResponse = {
  page: 2, pageSize: 24, total: 25,
  items: [{ id: 'asset-1', type: 'image', originalFileName: fileName, contentType: 'image/png',
    size: 1024, width: 640, height: 480, publicUrl: 'https://content.example/cat.png',
    description: 'A cat', altText: 'Cat', createdAtUtc: '2026-04-10T00:00:00Z', updatedAtUtc: '2026-04-10T00:00:00Z' }],
};

async function mountGallery(path = '/gallery?page=2&query=cat') {
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/gallery', component: GalleryListPage },
    { path: '/gallery/:id', component: { template: '<div />' } },
  ] });
  await router.push(path);
  const wrapper = mount(GalleryListPage, { global: { plugins: [router,
    createI18n({ legacy: false, locale: 'zh-CN', messages: { 'zh-CN': zhCN } })] } });
  await flushPromises();
  return { wrapper, router };
}

describe('public gallery navigation and recovery', () => {
  beforeEach(() => {
    viewport.isMobile = false;
    vi.mocked(listPublicAssets).mockReset();
    vi.mocked(listPublicAssets).mockResolvedValue(results);
  });

  it('renders cards as native links with full accessible titles and preserves search context', async () => {
    const { wrapper, router } = await mountGallery();
    expect(wrapper.getComponent(NPagination).props('simple')).toBe(false);
    expect(wrapper.getComponent(NPagination).props('pageSlot')).toBe(7);
    const card = wrapper.get('a.gallery-card');
    expect(card.attributes('href')).toBe('/gallery/asset-1?page=2&query=cat');
    expect(card.attributes('aria-label')).toBe(fileName);
    expect(card.get('h2').attributes('title')).toBe(fileName);
    await card.trigger('click');
    await flushPromises();
    expect(router.currentRoute.value.path).toBe('/gallery/asset-1');
    expect(router.currentRoute.value.query).toEqual({ page: '2', query: 'cat' });
    wrapper.unmount();
  });

  it('retries the failed page and query from the adjacent error action', async () => {
    vi.mocked(listPublicAssets).mockRejectedValueOnce(new Error('Temporarily unavailable'));
    const { wrapper } = await mountGallery();
    expect(wrapper.get('.gallery-alert').text()).toContain('Temporarily unavailable');
    await wrapper.get('.gallery-alert button').trigger('click');
    await flushPromises();
    expect(listPublicAssets).toHaveBeenLastCalledWith({ page: 2, pageSize: 24, query: 'cat' });
    expect(listPublicAssets).toHaveBeenCalledTimes(2);
    expect(wrapper.find('.gallery-alert').exists()).toBe(false);
    expect(wrapper.find('a.gallery-card').exists()).toBe(true);
    wrapper.unmount();
  });

  it('clears an unsubmitted search even when the route has no query', async () => {
    const { wrapper } = await mountGallery('/gallery');
    await wrapper.get('input[type="search"]').setValue('draft query');
    await wrapper.get('.gallery-reset').trigger('click');
    await flushPromises();
    expect((wrapper.get('input[type="search"]').element as HTMLInputElement).value).toBe('');
    wrapper.unmount();
  });
  it('uses simple pagination on narrow screens while preserving the search when changing pages', async () => {
    viewport.isMobile = true;
    vi.mocked(listPublicAssets).mockResolvedValue({ ...results, total: 240 });
    const { wrapper, router } = await mountGallery();
    const pagination = wrapper.getComponent(NPagination);
    expect(pagination.props('simple')).toBe(true);
    pagination.vm.$emit('update:page', 3);
    await flushPromises();
    expect(router.currentRoute.value.query).toEqual({ page: '3', query: 'cat' });
    expect(listPublicAssets).toHaveBeenLastCalledWith({ page: 3, pageSize: 24, query: 'cat' });
    wrapper.unmount();
  });

});
