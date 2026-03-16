<script setup lang="ts">
import { computed } from 'vue';
import { NTag } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { normalizeAssetStatus, type AssetStatusLike } from '../../types/assets';

interface Props {
  status: AssetStatusLike;
}

const props = defineProps<Props>();
const { t, te } = useI18n();

const normalizedStatus = computed(() => normalizeAssetStatus(props.status));

const tagType = computed(() => {
  switch (normalizedStatus.value) {
    case 'ready':
      return 'success';
    case 'pending':
      return 'warning';
    case 'failed':
      return 'error';
    case 'deleted':
      return 'default';
    default:
      return 'info';
  }
});

const label = computed(() => {
  const key = `asset.status.${normalizedStatus.value}`;
  if (te(key)) {
    return t(key);
  }

  if (typeof props.status === 'string' && props.status.trim().length > 0) {
    return props.status;
  }

  if (typeof props.status === 'number') {
    return String(props.status);
  }

  return t('asset.status.unknown');
});
</script>

<template>
  <n-tag :type="tagType" size="small" :bordered="false">
    {{ label }}
  </n-tag>
</template>
