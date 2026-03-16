<script setup lang="ts">
import { computed } from 'vue';
import { NButton, NCard, NSpace } from 'naive-ui';
import { Handle, Position, useVueFlow, type NodeProps } from '@vue-flow/core';
import { NodeToolbar } from '@vue-flow/node-toolbar';
import { useI18n } from 'vue-i18n';
import { getWorkflowSkillCatalogItem } from '../../pages/workflows/skill-catalog';
import type { WorkflowSkillNodeData } from '../../types/workflows';

interface SkillNodeProps extends NodeProps<WorkflowSkillNodeData> {
  readOnly?: boolean;
}

const props = defineProps<SkillNodeProps>();

const { t } = useI18n();
const { removeNodes } = useVueFlow();

const skillMeta = computed(() => getWorkflowSkillCatalogItem(props.data.skillId));
const nodeLabel = computed(() => (
  skillMeta.value
    ? t(skillMeta.value.labelKey)
    : props.data.skillId
));
const nodeDescription = computed(() => (
  skillMeta.value
    ? t(skillMeta.value.descriptionKey)
    : t('workflows.unknownSkill')
));
const nodeShortCode = computed(() => skillMeta.value?.shortCode ?? props.data.skillId.slice(0, 2).toUpperCase());
const accentColor = computed(() => skillMeta.value?.accentColor ?? '#4f46e5');

function handleDelete(): void {
  if (props.readOnly) {
    return;
  }

  removeNodes(props.id);
}
</script>

<template>
  <NodeToolbar :is-visible="selected && !readOnly" :position="Position.Top" :offset="12">
    <n-button
      size="tiny"
      tertiary
      type="error"
      class="skill-node__delete"
      @click.stop="handleDelete"
    >
      {{ t('workflows.node.delete') }}
    </n-button>
  </NodeToolbar>

  <Handle
    :id="`${id}-input`"
    type="target"
    :position="targetPosition ?? Position.Left"
    :connectable="connectable"
  />

  <n-card
    size="small"
    embedded
    class="skill-node"
    :class="{ 'skill-node--selected': selected }"
    :style="{ '--skill-accent': accentColor }"
  >
    <n-space align="start" :size="12">
      <div class="skill-node__icon">{{ nodeShortCode }}</div>

      <div class="skill-node__content">
        <div class="skill-node__title-row">
          <div class="skill-node__title">{{ nodeLabel }}</div>
        </div>

        <div class="skill-node__description">{{ nodeDescription }}</div>
      </div>
    </n-space>
  </n-card>

  <Handle
    :id="`${id}-output`"
    type="source"
    :position="sourcePosition ?? Position.Right"
    :connectable="connectable"
  />
</template>

<style scoped>
.skill-node {
  min-width: 250px;
  border-radius: 14px;
  border: 1px solid color-mix(in srgb, var(--skill-accent) 20%, #dbe4f0);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96)),
    linear-gradient(135deg, color-mix(in srgb, var(--skill-accent) 6%, #ffffff), #ffffff);
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.07);
  overflow: visible;
}

.skill-node--selected {
  box-shadow: 0 14px 30px rgba(37, 99, 235, 0.16);
}

.skill-node :deep(.n-card__content) {
  padding: 14px 16px;
}

.skill-node__icon {
  width: 42px;
  height: 42px;
  border-radius: 14px;
  background: color-mix(in srgb, var(--skill-accent) 16%, #f8fafc);
  color: var(--skill-accent);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.08em;
  flex-shrink: 0;
}

.skill-node__content {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.skill-node__title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  justify-content: space-between;
}

.skill-node__title {
  min-width: 0;
  font-size: 14px;
  font-weight: 700;
  color: #0f172a;
}

.skill-node__description {
  font-size: 12px;
  line-height: 1.6;
  color: #475569;
}

.skill-node__delete {
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.12);
}
</style>
