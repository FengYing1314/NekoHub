<script setup lang="ts">
import { computed } from 'vue';
import { NTag } from 'naive-ui';
import { useI18n } from 'vue-i18n';

interface Props {
  isPublic: boolean;
}

const props = defineProps<Props>();
const { t } = useI18n();

const tagType = computed(() => (props.isPublic ? 'success' : 'warning'));
const label = computed(() => (
  props.isPublic
    ? t('asset.visibility.public')
    : t('asset.visibility.private')
));
</script>

<template>
  <n-tag :type="tagType" size="small" :bordered="false">
    {{ label }}
  </n-tag>
</template>
