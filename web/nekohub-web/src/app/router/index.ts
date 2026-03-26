import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import AppLayout from '../../layouts/AppLayout.vue';
import SettingsPage from '../../pages/settings/SettingsPage.vue';
import AssetListPage from '../../pages/assets/AssetListPage.vue';
import AssetUploadPage from '../../pages/assets/AssetUploadPage.vue';
import AssetDetailPage from '../../pages/assets/AssetDetailPage.vue';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: AppLayout,
    children: [
      {
        path: '',
        redirect: '/assets',
      },
      {
        path: 'settings',
        name: 'settings',
        component: SettingsPage,
        meta: {
          titleKey: 'settings.title',
          navKey: '/settings',
        },
      },
      {
        path: 'assets',
        name: 'asset-list',
        component: AssetListPage,
        meta: {
          titleKey: 'asset.list.title',
          navKey: '/assets',
        },
      },
      {
        path: 'assets/upload',
        name: 'asset-upload',
        component: AssetUploadPage,
        meta: {
          titleKey: 'asset.upload.title',
          navKey: '/assets/upload',
        },
      },
      {
        path: 'assets/:id',
        name: 'asset-detail',
        component: AssetDetailPage,
        meta: {
          titleKey: 'asset.detail.title',
          navKey: '/assets',
        },
      },
    ],
  },
];

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});
