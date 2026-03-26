<script setup lang="ts">
import { computed } from 'vue';
import { NTag } from 'naive-ui';
import { useI18n } from 'vue-i18n';

interface Props {
  status: string;
}

const props = defineProps<Props>();
const { t, te } = useI18n();

const normalizedStatus = computed(() => props.status.toLowerCase());

const tagType = computed(() => {
  switch (normalizedStatus.value) {
    case 'ready':
      return 'success';
    case 'processing':
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
  return te(key) ? t(key) : props.status;
});
</script>

<template>
  <n-tag :type="tagType" size="small" :bordered="false">
    {{ label }}
  </n-tag>
</template>
