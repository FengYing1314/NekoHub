export interface WorkflowSkillCatalogItem {
  skillId: string;
  shortCode: string;
  accentColor: string;
  enabled: boolean;
  labelKey: string;
  descriptionKey: string;
}

export const WORKFLOW_SKILL_CATALOG = [
  {
    skillId: 'thumbnail',
    shortCode: '缩略',
    accentColor: '#2563eb',
    enabled: true,
    labelKey: 'workflows.skills.thumbnail.label',
    descriptionKey: 'workflows.skills.thumbnail.description',
  },
  {
    skillId: 'ai-caption',
    shortCode: '说明',
    accentColor: '#0f766e',
    enabled: true,
    labelKey: 'workflows.skills.aiCaption.label',
    descriptionKey: 'workflows.skills.aiCaption.description',
  },
  {
    skillId: 'exif-strip',
    shortCode: '清理',
    accentColor: '#b45309',
    enabled: true,
    labelKey: 'workflows.skills.exifStrip.label',
    descriptionKey: 'workflows.skills.exifStrip.description',
  },
  {
    skillId: 'format-convert',
    shortCode: '转码',
    accentColor: '#7c3aed',
    enabled: true,
    labelKey: 'workflows.skills.formatConvert.label',
    descriptionKey: 'workflows.skills.formatConvert.description',
  },
  {
    skillId: 'watermark',
    shortCode: '水印',
    accentColor: '#db2777',
    enabled: true,
    labelKey: 'workflows.skills.watermark.label',
    descriptionKey: 'workflows.skills.watermark.description',
  },
] as const satisfies readonly WorkflowSkillCatalogItem[];

export function getWorkflowSkillCatalogItem(skillId: string): WorkflowSkillCatalogItem | undefined {
  return WORKFLOW_SKILL_CATALOG.find((item) => item.skillId === skillId);
}
