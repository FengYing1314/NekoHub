<script setup lang="ts">
interface Props {
  title: string;
  description?: string;
}

defineProps<Props>();
</script>

<template>
  <div class="page-header">
    <div class="page-header__content">
      <h1 class="page-header__title">{{ title }}</h1>
      <p v-if="description" class="page-header__description">{{ description }}</p>
    </div>
    <div v-if="$slots.actions" class="page-header__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 22px;
  padding: 2px 0;
}

.page-header__content {
  min-width: 0;
}

.page-header__title {
  margin: 0;
  font-size: 28px;
  font-weight: 700;
  letter-spacing: -0.03em;
  color: var(--app-text-strong);
}

.page-header__description {
  margin: 10px 0 0;
  max-width: 760px;
  color: var(--app-text-muted);
  font-size: 14px;
  line-height: 1.7;
}

.page-header__actions {
  flex-shrink: 0;
  min-width: 0;
}

.page-header__actions :deep(.n-space) {
  flex-wrap: wrap;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .page-header {
    flex-direction: column;
    align-items: stretch;
  }

  .page-header__actions {
    width: 100%;
  }

  .page-header__title {
    font-size: 24px;
  }

  .page-header__actions :deep(.n-space) {
    width: 100%;
    justify-content: flex-start;
  }

  .page-header__actions :deep(.n-space-item) {
    max-width: 100%;
  }
}
</style>
