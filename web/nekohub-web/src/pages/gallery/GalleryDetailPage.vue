<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NAlert, NButton, NEmpty, NTag } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import { getPublicAsset } from '../../api/public/public-assets.api';
import type { PublicAssetDerivativeSummaryResponse, PublicAssetResponse } from '../../types/public-assets';
import { formatDateTime, formatFileSize } from '../../utils/format';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();

const loading = ref(false);
const loadErrorMessage = ref('');
const asset = ref<PublicAssetResponse | null>(null);

const assetId = computed(() => String(route.params.id ?? ''));
const titleText = computed(() => (
  asset.value?.originalFileName?.trim()
  || asset.value?.altText?.trim()
  || asset.value?.description?.trim()
  || t('gallery.detail.untitled')
));
const summaryText = computed(() => asset.value?.description?.trim() || asset.value?.altText?.trim() || '');

function backToGallery(): void {
  void router.push({
    path: '/gallery',
    query: route.query,
  });
}

function openPublicUrl(url: string): void {
  const popup = window.open(url, '_blank', 'noopener,noreferrer');
  if (!popup) {
    window.location.assign(url);
  }
}

function canPreviewDerivative(derivative: PublicAssetDerivativeSummaryResponse): boolean {
  return derivative.contentType.startsWith('image/') && Boolean(derivative.publicUrl);
}

async function loadAsset(): Promise<void> {
  if (!assetId.value) {
    asset.value = null;
    return;
  }

  loading.value = true;
  loadErrorMessage.value = '';

  try {
    asset.value = await getPublicAsset(assetId.value);
  } catch (error) {
    asset.value = null;
    loadErrorMessage.value = error instanceof Error
      ? error.message
      : t('gallery.detail.loadFailed');
  } finally {
    loading.value = false;
  }
}

watch(
  () => assetId.value,
  () => {
    void loadAsset();
  },
  { immediate: true },
);
</script>

<template>
  <div class="gallery-detail-page">
    <section class="gallery-detail-hero">
      <div class="gallery-detail-hero__copy">
        <span class="gallery-detail-hero__eyebrow">{{ t('gallery.detail.eyebrow') }}</span>
        <h1 class="gallery-detail-hero__title">{{ titleText }}</h1>
        <p v-if="summaryText" class="gallery-detail-hero__description">{{ summaryText }}</p>

        <div class="gallery-detail-hero__actions">
          <n-button secondary @click="backToGallery">
            {{ t('gallery.detail.backAction') }}
          </n-button>
          <n-button
            v-if="asset?.publicUrl"
            type="primary"
            @click="openPublicUrl(asset.publicUrl)"
          >
            {{ t('gallery.detail.openOriginal') }}
          </n-button>
        </div>
      </div>

      <div class="gallery-detail-hero__preview">
        <img
          v-if="asset?.publicUrl"
          :src="asset.publicUrl"
          :alt="asset.altText || titleText"
          class="gallery-detail-hero__image"
        />
        <div v-else class="gallery-detail-hero__placeholder">
          {{ t('gallery.detail.previewUnavailable') }}
        </div>
      </div>
    </section>

    <n-alert v-if="loadErrorMessage" type="warning" :show-icon="false">
      {{ t('gallery.detail.loadFailed') }}: {{ loadErrorMessage }}
    </n-alert>

    <n-empty
      v-else-if="!loading && !asset"
      :description="t('gallery.detail.loadFailed')"
      style="padding: 60px 0"
    />

    <div v-else-if="asset" class="gallery-detail-grid">
      <section class="gallery-detail-panel">
        <header class="gallery-detail-panel__header">
          <h2>{{ t('gallery.detail.metadataTitle') }}</h2>
        </header>

        <dl class="gallery-detail-info-list">
          <dt>{{ t('gallery.detail.fields.fileName') }}</dt>
          <dd>{{ asset.originalFileName || '-' }}</dd>
          <dt>{{ t('gallery.detail.fields.contentType') }}</dt>
          <dd><n-tag size="small" :bordered="false">{{ asset.contentType }}</n-tag></dd>
          <dt>{{ t('gallery.detail.fields.size') }}</dt>
          <dd>{{ formatFileSize(asset.size) }}</dd>
          <dt>{{ t('gallery.detail.fields.dimensions') }}</dt>
          <dd>{{ asset.width ?? '-' }} × {{ asset.height ?? '-' }}</dd>
          <dt>{{ t('gallery.detail.fields.altText') }}</dt>
          <dd>{{ asset.altText || '-' }}</dd>
          <dt>{{ t('gallery.detail.fields.createdAt') }}</dt>
          <dd>{{ formatDateTime(asset.createdAtUtc) }}</dd>
          <dt>{{ t('gallery.detail.fields.updatedAt') }}</dt>
          <dd>{{ formatDateTime(asset.updatedAtUtc) }}</dd>
        </dl>
      </section>

      <section class="gallery-detail-panel">
        <header class="gallery-detail-panel__header">
          <h2>{{ t('gallery.detail.derivativesTitle') }}</h2>
        </header>

        <n-empty
          v-if="asset.derivatives.length === 0"
          :description="t('gallery.detail.noDerivatives')"
          style="padding: 22px 0"
        />

        <div v-else class="gallery-derivative-list">
          <article
            v-for="derivative in asset.derivatives"
            :key="`${derivative.kind}-${derivative.createdAtUtc}`"
            class="gallery-derivative-card"
          >
            <div class="gallery-derivative-card__preview">
              <img
                v-if="canPreviewDerivative(derivative) && derivative.publicUrl"
                :src="derivative.publicUrl"
                :alt="derivative.kind"
                class="gallery-derivative-card__image"
              />
              <div v-else class="gallery-derivative-card__placeholder">
                {{ t('gallery.detail.previewUnavailable') }}
              </div>
            </div>

            <div class="gallery-derivative-card__body">
              <div class="gallery-derivative-card__title">{{ derivative.kind }}</div>
              <div class="gallery-derivative-card__meta">{{ derivative.contentType }}</div>
              <div class="gallery-derivative-card__meta">
                {{ formatFileSize(derivative.size) }} · {{ derivative.width ?? '-' }} × {{ derivative.height ?? '-' }}
              </div>
              <div class="gallery-derivative-card__meta">{{ formatDateTime(derivative.createdAtUtc) }}</div>

              <n-button
                v-if="derivative.publicUrl"
                size="small"
                quaternary
                type="primary"
                @click="openPublicUrl(derivative.publicUrl)"
              >
                {{ t('gallery.detail.openDerivative') }}
              </n-button>
            </div>
          </article>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.gallery-detail-page {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.gallery-detail-hero {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(320px, 0.9fr);
  gap: 20px;
  padding: 24px;
  border: 1px solid rgba(191, 219, 254, 0.78);
  border-radius: 30px;
  background:
    radial-gradient(circle at top left, rgba(191, 219, 254, 0.55), transparent 28%),
    linear-gradient(135deg, rgba(255, 255, 255, 0.92) 0%, rgba(239, 246, 255, 0.94) 48%, rgba(255, 251, 235, 0.92) 100%);
}

.gallery-detail-hero__eyebrow {
  display: inline-flex;
  margin-bottom: 14px;
  padding: 6px 10px;
  border-radius: 999px;
  background: rgba(14, 116, 144, 0.1);
  color: #0f766e;
  font-size: 12px;
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

.gallery-detail-hero__title {
  margin: 0;
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: clamp(28px, 4vw, 46px);
  line-height: 1;
  letter-spacing: -0.05em;
  color: #111827;
}

.gallery-detail-hero__description {
  margin: 14px 0 0;
  color: #475569;
  line-height: 1.7;
}

.gallery-detail-hero__actions {
  margin-top: 24px;
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.gallery-detail-hero__preview {
  min-height: 320px;
  border-radius: 24px;
  overflow: hidden;
  background: linear-gradient(180deg, #f8fafc 0%, #e2e8f0 100%);
  box-shadow: inset 0 0 0 1px rgba(226, 232, 240, 0.9);
}

.gallery-detail-hero__image {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.gallery-detail-hero__placeholder,
.gallery-derivative-card__placeholder {
  width: 100%;
  height: 100%;
  display: grid;
  place-items: center;
  padding: 20px;
  color: #64748b;
  text-align: center;
}

.gallery-detail-grid {
  display: grid;
  grid-template-columns: 360px minmax(0, 1fr);
  gap: 18px;
}

.gallery-detail-panel {
  border: 1px solid rgba(226, 232, 240, 0.9);
  border-radius: 24px;
  background: rgba(255, 255, 255, 0.84);
  padding: 20px;
  box-shadow: 0 16px 34px rgba(148, 163, 184, 0.12);
}

.gallery-detail-panel__header {
  margin-bottom: 16px;
}

.gallery-detail-panel__header h2 {
  margin: 0;
  font-size: 18px;
  color: #0f172a;
}

.gallery-detail-info-list {
  display: grid;
  grid-template-columns: minmax(0, 112px) minmax(0, 1fr);
  gap: 12px 16px;
  margin: 0;
}

.gallery-detail-info-list dt {
  color: #64748b;
  font-size: 13px;
}

.gallery-detail-info-list dd {
  margin: 0;
  color: #0f172a;
  word-break: break-word;
}

.gallery-derivative-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.gallery-derivative-card {
  overflow: hidden;
  border-radius: 20px;
  border: 1px solid rgba(226, 232, 240, 0.9);
  background: #f8fafc;
}

.gallery-derivative-card__preview {
  aspect-ratio: 4 / 3;
  background: linear-gradient(180deg, #f8fafc 0%, #e2e8f0 100%);
}

.gallery-derivative-card__image {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.gallery-derivative-card__body {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px;
}

.gallery-derivative-card__title {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.gallery-derivative-card__meta {
  font-size: 13px;
  color: #64748b;
  line-height: 1.6;
}

@media (max-width: 1024px) {
  .gallery-detail-hero,
  .gallery-detail-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .gallery-detail-hero,
  .gallery-detail-panel {
    padding: 18px;
    border-radius: 24px;
  }

  .gallery-derivative-list {
    grid-template-columns: 1fr;
  }

  .gallery-detail-info-list {
    grid-template-columns: 1fr;
    gap: 6px;
  }
}
</style>
