<script setup lang="ts">
import { computed } from 'vue';
import { NCard, NSpace } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import type { AssetStructuredResultSummaryResponse } from '../../../types/assets';
import { formatDateTime } from '../../../utils/format';

interface Props {
  result: AssetStructuredResultSummaryResponse;
}

const props = defineProps<Props>();
const { t } = useI18n();

const prettyPayload = computed(() => {
  try {
    const parsed = JSON.parse(props.result.payloadJson) as unknown;
    return JSON.stringify(parsed, null, 2);
  } catch {
    return props.result.payloadJson;
  }
});
</script>

<template>
  <n-card size="small">
    <template #header>{{ t('asset.detail.unknownStructuredResult') }}</template>
    <n-space vertical :size="8">
      <div>
        <strong>{{ t('asset.detail.structuredResultFields.kind') }}:</strong>
        <span class="kind-value">{{ result.kind }}</span>
      </div>
      <div>
        <strong>{{ t('asset.detail.structuredResultFields.createdAt') }}:</strong>
        {{ formatDateTime(result.createdAtUtc) }}
      </div>
      <div>
        <strong>{{ t('asset.detail.structuredResultRaw') }}:</strong>
        <pre class="payload-json">{{ prettyPayload }}</pre>
      </div>
    </n-space>
  </n-card>
</template>

<style scoped>
.kind-value {
  color: #111827;
  font-weight: 600;
  margin-left: 6px;
}

.payload-json {
  margin: 8px 0 0;
  padding: 10px;
  border-radius: 8px;
  background: #f3f4f6;
  overflow-x: auto;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
