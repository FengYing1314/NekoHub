<script setup lang="ts">
import { computed } from 'vue';
import { NCard, NSpace } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import type { AssetStructuredResultSummaryResponse, BasicCaptionStructuredResultPayload } from '../../../types/assets';
import { formatDateTime } from '../../../utils/format';

interface Props {
  result: AssetStructuredResultSummaryResponse;
}

const props = defineProps<Props>();
const { t } = useI18n();

const payload = computed<BasicCaptionStructuredResultPayload | null>(() => {
  try {
    const parsed = JSON.parse(props.result.payloadJson) as unknown;
    if (!parsed || typeof parsed !== 'object') {
      return null;
    }

    return parsed as BasicCaptionStructuredResultPayload;
  } catch {
    return null;
  }
});

const captionText = computed(() => {
  const parsed = payload.value;
  if (!parsed) {
    return '-';
  }

  return parsed.caption || '-';
});
</script>

<template>
  <n-card size="small">
    <n-space vertical :size="8">
      <div><strong>{{ t('asset.detail.structuredResultFields.kind') }}:</strong> {{ result.kind }}</div>
      <div>
        <strong>{{ t('asset.detail.structuredResultFields.createdAt') }}:</strong>
        {{ formatDateTime(result.createdAtUtc) }}
      </div>
      <div><strong>{{ t('asset.detail.structuredResultFields.caption') }}:</strong> {{ captionText }}</div>
      <div><strong>{{ t('asset.detail.structuredResultFields.generator') }}:</strong> {{ payload?.generator ?? '-' }}</div>
    </n-space>
  </n-card>
</template>
