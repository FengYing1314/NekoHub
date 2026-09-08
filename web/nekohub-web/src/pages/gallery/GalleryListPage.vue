<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import {
  NAlert,
  NButton,
  NEmpty,
  NPagination,
  NSkeleton,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useIsMobile } from '../../composables/useIsMobile';
import { listPublicAssets } from '../../api/public/public-assets.api';
import type { PublicAssetListItemResponse } from '../../types/public-assets';
import { formatDateTime, formatFileSize } from '../../utils/format';

const PAGE_SIZE = 24;

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const { isMobile } = useIsMobile();

const loading = ref(false);
const loadErrorMessage = ref('');
const assets = ref<PublicAssetListItemResponse[]>([]);
const total = ref(0);
const page = ref(1);
const queryText = ref('');
const queryDraft = ref('');

const isEmpty = computed(() => !loading.value && !loadErrorMessage.value && assets.value.length === 0);
const resultLabel = computed(() => t('gallery.list.resultCount', { count: total.value }));

function parseRoutePage(rawValue: unknown): number {
  if (typeof rawValue !== 'string') {
    return 1;
  }

  const parsed = Number.parseInt(rawValue, 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

function buildRouteQuery(nextPage: number, nextQuery: string): Record<string, string | undefined> {
  const normalizedQuery = nextQuery.trim();

  return {
    page: nextPage > 1 ? String(nextPage) : undefined,
    query: normalizedQuery || undefined,
  };
}

async function fetchAssets(nextPage: number, nextQuery: string): Promise<void> {
  loading.value = true;
  loadErrorMessage.value = '';

  try {
    const response = await listPublicAssets({
      page: nextPage,
      pageSize: PAGE_SIZE,
      query: nextQuery || undefined,
    });

    assets.value = response.items;
    total.value = response.total;
    page.value = response.page;
    queryText.value = nextQuery;
    queryDraft.value = nextQuery;
  } catch (error) {
    loadErrorMessage.value = error instanceof Error
      ? error.message
      : t('gallery.list.loadFailed');
    assets.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function applySearch(): void {
  void router.replace({
    path: '/gallery',
    query: buildRouteQuery(1, queryDraft.value),
  });
}

function resetSearch(): void {
  queryDraft.value = '';
  void router.replace({
    path: '/gallery',
    query: {},
  });
}

function handlePageChange(nextPage: number): void {
  void router.replace({
    path: '/gallery',
    query: buildRouteQuery(nextPage, queryText.value),
  });
}


function buildAssetTitle(asset: PublicAssetListItemResponse): string {
  return asset.originalFileName?.trim()
    || asset.altText?.trim()
    || asset.description?.trim()
    || t('gallery.list.untitled');
}

async function loadFromRoute(): Promise<void> {
  const query = route.query;
  const nextPage = parseRoutePage(Array.isArray(query.page) ? query.page[0] : query.page);
  const nextQuery = typeof query.query === 'string' ? query.query.trim() : '';
  await fetchAssets(nextPage, nextQuery);
}

watch(() => route.query, loadFromRoute, { immediate: true });
</script>

<template>
  <div class="gallery-page">
    <section class="gallery-hero">
      <div class="gallery-hero__copy">
        <span class="gallery-hero__eyebrow">{{ t('gallery.list.eyebrow') }}</span>
        <h1 class="gallery-hero__title">{{ t('gallery.list.title') }}</h1>
        <p class="gallery-hero__description">{{ t('gallery.list.description') }}</p>
      </div>

      <form class="gallery-hero__toolbar" @submit.prevent="applySearch">
        <label class="gallery-search">
          <span class="gallery-search__label">{{ t('gallery.list.searchLabel') }}</span>
          <input
            v-model="queryDraft"
            type="search"
            class="gallery-search__input"
            :placeholder="t('gallery.list.searchPlaceholder')"
          />
        </label>

        <div class="gallery-hero__actions">
          <n-button type="primary" attr-type="submit">
            {{ t('gallery.list.searchAction') }}
          </n-button>
          <n-button quaternary attr-type="button" class="gallery-reset"
            :theme-overrides="{ textColor: '#f8fafc', colorQuaternary: 'rgba(255,255,255,0.08)',
              colorQuaternaryHover: 'rgba(255,255,255,0.18)', colorQuaternaryPressed: 'rgba(255,255,255,0.24)' }"
            @click="resetSearch">
            {{ t('gallery.list.resetAction') }}
          </n-button>
        </div>

        <div class="gallery-hero__meta" aria-live="polite">{{ resultLabel }}</div>
      </form>
    </section>

    <n-alert v-if="loadErrorMessage" type="warning" :show-icon="false" class="gallery-alert">
      <div class="gallery-alert__content">
        <span>{{ t('gallery.list.loadFailed') }}: {{ loadErrorMessage }}</span>
        <n-button size="small" secondary :loading="loading" @click="loadFromRoute">{{ t('common.retry') }}</n-button>
      </div>
    </n-alert>

    <div v-if="loading && assets.length === 0" class="gallery-grid">
      <div v-for="index in 6" :key="index" class="gallery-card gallery-card--loading">
        <n-skeleton height="220px" class="gallery-card__skeleton" />
        <n-skeleton text style="margin-top: var(--app-space-md); width: 64%" />
        <n-skeleton text :repeat="2" />
      </div>
    </div>

    <n-empty
      v-else-if="isEmpty"
      :description="t('gallery.list.empty')"
      style="padding: 72px 0 60px"
    />

    <div v-else class="gallery-grid">
      <RouterLink
        v-for="asset in assets"
        :key="asset.id"
        class="gallery-card"
        :to="{ path: `/gallery/${asset.id}`, query: route.query }"
        :aria-label="buildAssetTitle(asset)"
      >
        <div class="gallery-card__preview">
          <img
            v-if="asset.publicUrl"
            :src="asset.publicUrl"
            :alt="asset.altText || buildAssetTitle(asset)"
            class="gallery-card__image"
            loading="lazy"
          />
          <div v-else class="gallery-card__placeholder">
            {{ t('gallery.list.previewUnavailable') }}
          </div>
        </div>

        <div class="gallery-card__body">
          <div class="gallery-card__topline">
            <span class="gallery-card__type">{{ asset.contentType }}</span>
            <span>{{ formatDateTime(asset.createdAtUtc) }}</span>
          </div>

          <h2 class="gallery-card__title" :title="buildAssetTitle(asset)">{{ buildAssetTitle(asset) }}</h2>
          <p class="gallery-card__description">
            {{ asset.description || asset.altText || t('gallery.list.descriptionFallback') }}
          </p>

          <div class="gallery-card__meta">
            <span>{{ formatFileSize(asset.size) }}</span>
            <span>{{ asset.width ?? '-' }} × {{ asset.height ?? '-' }}</span>
          </div>
        </div>
      </RouterLink>
    </div>

    <div v-if="total > PAGE_SIZE" class="gallery-pagination">
      <n-pagination
        :page="page"
        :page-size="PAGE_SIZE"
        :item-count="total"
        :page-slot="7"
        :simple="isMobile"
        @update:page="handlePageChange"
      />
    </div>
  </div>
</template>

<style scoped>
.gallery-page {
  display: flex;
  flex-direction: column;
  gap: var(--app-space-md);
}

.gallery-hero {
  position: relative;
  overflow: hidden;
  border-radius: var(--app-radius-panel);
  padding: var(--app-space-xl);
  background:
    radial-gradient(circle at top right, rgba(253, 186, 116, 0.45), transparent 26%),
    linear-gradient(135deg, #1f2937 0%, #334155 42%, #78350f 100%);
  color: #f8fafc;
  box-shadow: var(--app-shadow);
}

.gallery-hero__copy {
  max-width: 620px;
}

.gallery-hero__eyebrow {
  display: inline-flex;
  margin-bottom: var(--app-space-sm);
  padding: 6px 10px;
  border-radius: 999px;
  background: rgba(248, 250, 252, 0.12);
  font-size: 12px;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

.gallery-hero__title {
  margin: 0;
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: clamp(28px, 3vw, 32px);
  line-height: 1.25;
  letter-spacing: -0.03em;
}

.gallery-hero__description {
  margin: var(--app-space-md) 0 0;
  max-width: 560px;
  font-size: 15px;
  line-height: 1.7;
  color: rgba(241, 245, 249, 0.92);
}

.gallery-hero__toolbar {
  margin-top: var(--app-space-lg);
  padding-top: var(--app-space-md);
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  gap: var(--app-space-md);
  border-top: 1px solid rgba(248, 250, 252, 0.14);
}

.gallery-search {
  flex: 1 1 320px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.gallery-search__label {
  font-size: 12px;
  color: rgba(226, 232, 240, 0.78);
}

.gallery-search__input {
  width: 100%;
  min-height: 40px;
  padding: 0 16px;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: var(--app-radius-control);
  background: rgba(15, 23, 42, 0.3);
  color: #f8fafc;
  font: inherit;
}

.gallery-search__input::placeholder {
  color: rgba(226, 232, 240, 0.64);
}

.gallery-hero__actions {
  display: flex;
  gap: var(--app-space-sm);
}

.gallery-search__input:focus-visible,
.gallery-hero__actions :deep(.n-button:focus-visible) {
  outline: 2px solid #fbbf24;
  outline-offset: 3px;
}

.gallery-hero__meta {
  margin-left: auto;
  font-size: 13px;
  color: rgba(226, 232, 240, 0.78);
}

.gallery-alert {
  border-radius: var(--app-radius-card);
}

.gallery-alert__content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--app-space-sm);
  overflow-wrap: anywhere;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--app-space-md);
}

.gallery-card {
  display: block;
  min-width: 0;
  color: inherit;
  overflow: hidden;
  border: 1px solid rgba(203, 213, 225, 0.72);
  border-radius: var(--app-radius-card);
  background: rgba(255, 255, 255, 0.82);
  cursor: pointer;
  box-shadow: var(--app-shadow-soft);
  transition: transform 0.22s ease, box-shadow 0.22s ease, border-color 0.22s ease;
}

.gallery-card:hover {
  transform: translateY(-2px);
  border-color: rgba(245, 158, 11, 0.4);
  box-shadow: var(--app-shadow);
}

.gallery-card:focus-visible {
  outline: 3px solid #b45309;
  outline-offset: 3px;
}

.gallery-card--loading {
  padding: var(--app-space-md);
  cursor: default;
}

.gallery-card__skeleton {
  border-radius: var(--app-radius-control);
}

.gallery-card__preview {
  aspect-ratio: 4 / 3;
  background: linear-gradient(180deg, #f8fafc 0%, #e2e8f0 100%);
}

.gallery-card__image {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.gallery-card__placeholder {
  width: 100%;
  height: 100%;
  display: grid;
  place-items: center;
  padding: 20px;
  text-align: center;
  color: #64748b;
  font-size: 14px;
}

.gallery-card__body {
  padding: var(--app-space-md);
}

.gallery-card__topline,
.gallery-card__meta {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 12px;
  color: #64748b;
}

.gallery-card__title {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  overflow-wrap: anywhere;
  margin: var(--app-space-sm) 0;
  font-size: 18px;
  line-height: 1.4;
  color: #0f172a;
}

.gallery-card__type {
  display: inline-flex;
  max-width: 60%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.gallery-card__description {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  overflow-wrap: anywhere;
  margin: 0 0 var(--app-space-md);
  min-height: 40px;
  color: #475569;
  line-height: 1.6;
}

.gallery-pagination {
  display: flex;
  justify-content: center;
  margin-top: 8px;
  padding-top: 12px;
}

@media (max-width: 1024px) {
  .gallery-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .gallery-hero {
    padding: var(--app-space-md);
  }

  .gallery-hero__title {
    font-size: 24px;
  }

  .gallery-hero__eyebrow {
    padding: 4px var(--app-space-sm);
    font-size: 11px;
    letter-spacing: 0.1em;
  }

  .gallery-hero__description {
    margin-top: var(--app-space-sm);
    font-size: 13px;
    line-height: 1.6;
  }

  .gallery-grid {
    grid-template-columns: 1fr;
  }

  .gallery-hero__toolbar {
    margin-top: var(--app-space-md);
    padding-top: var(--app-space-md);
    align-items: center;
    gap: var(--app-space-sm);
  }

  .gallery-search {
    flex-basis: 100%;
    min-width: 0;
  }

  .gallery-search__input {
    padding: 0 12px;
    font-size: 14px;
  }

  .gallery-hero__meta {
    margin-left: auto;
    font-size: 12px;
  }
}
</style>
