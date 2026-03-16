<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import {
  NAlert,
  NButton,
  NEmpty,
  NPagination,
  NSkeleton,
  NSpace,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import { listPublicAssets } from '../../api/public/public-assets.api';
import type { PublicAssetListItemResponse } from '../../types/public-assets';
import { formatDateTime, formatFileSize } from '../../utils/format';

const PAGE_SIZE = 24;

const route = useRoute();
const router = useRouter();
const { t } = useI18n();

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

function openDetail(id: string): void {
  void router.push({
    path: `/gallery/${id}`,
    query: route.query,
  });
}

function buildAssetTitle(asset: PublicAssetListItemResponse): string {
  return asset.originalFileName?.trim()
    || asset.altText?.trim()
    || asset.description?.trim()
    || t('gallery.list.untitled');
}

watch(
  () => route.query,
  (query) => {
    const nextPage = parseRoutePage(Array.isArray(query.page) ? query.page[0] : query.page);
    const nextQuery = typeof query.query === 'string' ? query.query.trim() : '';
    void fetchAssets(nextPage, nextQuery);
  },
  { immediate: true },
);
</script>

<template>
  <div class="gallery-page">
    <section class="gallery-hero">
      <div class="gallery-hero__copy">
        <span class="gallery-hero__eyebrow">{{ t('gallery.list.eyebrow') }}</span>
        <h1 class="gallery-hero__title">{{ t('gallery.list.title') }}</h1>
        <p class="gallery-hero__description">{{ t('gallery.list.description') }}</p>
      </div>

      <div class="gallery-hero__toolbar">
        <label class="gallery-search">
          <span class="gallery-search__label">{{ t('gallery.list.searchLabel') }}</span>
          <input
            v-model="queryDraft"
            type="search"
            class="gallery-search__input"
            :placeholder="t('gallery.list.searchPlaceholder')"
            @keyup.enter="applySearch"
          />
        </label>

        <n-space :size="10" wrap>
          <n-button type="primary" @click="applySearch">
            {{ t('gallery.list.searchAction') }}
          </n-button>
          <n-button quaternary @click="resetSearch">
            {{ t('gallery.list.resetAction') }}
          </n-button>
        </n-space>

        <div class="gallery-hero__meta">{{ resultLabel }}</div>
      </div>
    </section>

    <n-alert v-if="loadErrorMessage" type="warning" :show-icon="false" class="gallery-alert">
      {{ t('gallery.list.loadFailed') }}: {{ loadErrorMessage }}
    </n-alert>

    <div v-if="loading && assets.length === 0" class="gallery-grid">
      <div v-for="index in 6" :key="index" class="gallery-card gallery-card--loading">
        <n-skeleton height="220px" style="border-radius: 22px" />
        <n-skeleton text style="margin-top: 18px; width: 64%" />
        <n-skeleton text :repeat="2" />
      </div>
    </div>

    <n-empty
      v-else-if="isEmpty"
      :description="t('gallery.list.empty')"
      style="padding: 72px 0 60px"
    />

    <div v-else class="gallery-grid">
      <article
        v-for="asset in assets"
        :key="asset.id"
        class="gallery-card"
        @click="openDetail(asset.id)"
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

          <h2 class="gallery-card__title">{{ buildAssetTitle(asset) }}</h2>
          <p class="gallery-card__description">
            {{ asset.description || asset.altText || t('gallery.list.descriptionFallback') }}
          </p>

          <div class="gallery-card__meta">
            <span>{{ formatFileSize(asset.size) }}</span>
            <span>{{ asset.width ?? '-' }} × {{ asset.height ?? '-' }}</span>
          </div>
        </div>
      </article>
    </div>

    <div v-if="total > PAGE_SIZE" class="gallery-pagination">
      <n-pagination
        :page="page"
        :page-size="PAGE_SIZE"
        :item-count="total"
        :page-slot="7"
        @update:page="handlePageChange"
      />
    </div>
  </div>
</template>

<style scoped>
.gallery-page {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.gallery-hero {
  position: relative;
  overflow: hidden;
  border-radius: 30px;
  padding: 34px;
  background:
    radial-gradient(circle at top right, rgba(253, 186, 116, 0.45), transparent 26%),
    linear-gradient(135deg, #1f2937 0%, #334155 42%, #78350f 100%);
  color: #f8fafc;
  box-shadow: 0 24px 48px rgba(15, 23, 42, 0.16);
}

.gallery-hero__copy {
  max-width: 620px;
}

.gallery-hero__eyebrow {
  display: inline-flex;
  margin-bottom: 14px;
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
  font-size: clamp(34px, 5vw, 56px);
  line-height: 0.96;
  letter-spacing: -0.06em;
}

.gallery-hero__description {
  margin: 16px 0 0;
  max-width: 560px;
  font-size: 15px;
  line-height: 1.7;
  color: rgba(241, 245, 249, 0.92);
}

.gallery-hero__toolbar {
  margin-top: 28px;
  padding-top: 20px;
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  gap: 14px;
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
  min-height: 48px;
  padding: 0 16px;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 16px;
  background: rgba(15, 23, 42, 0.3);
  color: #f8fafc;
  font: inherit;
}

.gallery-search__input::placeholder {
  color: rgba(226, 232, 240, 0.64);
}

.gallery-hero__meta {
  margin-left: auto;
  font-size: 13px;
  color: rgba(226, 232, 240, 0.78);
}

.gallery-alert {
  border-radius: 18px;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 18px;
}

.gallery-card {
  overflow: hidden;
  border: 1px solid rgba(203, 213, 225, 0.72);
  border-radius: 24px;
  background: rgba(255, 255, 255, 0.82);
  cursor: pointer;
  box-shadow: 0 20px 36px rgba(148, 163, 184, 0.14);
  transition: transform 0.22s ease, box-shadow 0.22s ease, border-color 0.22s ease;
}

.gallery-card:hover {
  transform: translateY(-4px);
  border-color: rgba(245, 158, 11, 0.4);
  box-shadow: 0 28px 42px rgba(148, 163, 184, 0.2);
}

.gallery-card--loading {
  padding: 18px;
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
  padding: 16px 18px 18px;
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
  margin: 10px 0 8px;
  font-size: 20px;
  line-height: 1.18;
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
  margin: 0 0 14px;
  min-height: 48px;
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
    padding: 24px 20px;
    border-radius: 24px;
  }

  .gallery-grid {
    grid-template-columns: 1fr;
  }

  .gallery-hero__toolbar {
    align-items: stretch;
  }

  .gallery-hero__meta {
    width: 100%;
    margin-left: 0;
  }
}
</style>
