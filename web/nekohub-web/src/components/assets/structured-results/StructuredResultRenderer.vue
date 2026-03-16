<script setup lang="ts">
import type { AssetStructuredResultSummaryResponse } from '../../../types/assets';
import BasicCaptionResultCard from './BasicCaptionResultCard.vue';
import UnknownStructuredResultCard from './UnknownStructuredResultCard.vue';

interface Props {
  result: AssetStructuredResultSummaryResponse;
}

defineProps<Props>();
</script>

<template>
  <basic-caption-result-card v-if="result.kind === 'basic_caption'" :result="result" />
  <unknown-structured-result-card v-else :result="result" />
</template>
