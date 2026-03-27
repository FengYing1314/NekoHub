import type { StorageProviderCapabilitiesResponse } from '../../types/storage';

export type CapabilityTagType = 'success' | 'warning' | 'info' | 'default';

type TranslateFn = (key: string) => string;

interface CapabilityDescriptor {
  key: keyof StorageProviderCapabilitiesResponse;
  positiveLabelKey: string;
  positiveType: CapabilityTagType;
  negativeLabelKey?: string;
  negativeType?: CapabilityTagType;
}

const capabilityDescriptors: CapabilityDescriptor[] = [
  {
    key: 'supportsPublicRead',
    positiveLabelKey: 'settings.storage.capabilities.supportsPublicRead',
    positiveType: 'success',
    negativeLabelKey: 'settings.storage.capabilities.notSupportsPublicRead',
    negativeType: 'warning',
  },
  {
    key: 'supportsPrivateRead',
    positiveLabelKey: 'settings.storage.capabilities.supportsPrivateRead',
    positiveType: 'success',
    negativeLabelKey: 'settings.storage.capabilities.notSupportsPrivateRead',
    negativeType: 'warning',
  },
  {
    key: 'supportsVisibilityToggle',
    positiveLabelKey: 'settings.storage.capabilities.supportsVisibilityToggle',
    positiveType: 'info',
    negativeLabelKey: 'settings.storage.capabilities.notSupportsVisibilityToggle',
    negativeType: 'warning',
  },
  {
    key: 'supportsDelete',
    positiveLabelKey: 'settings.storage.capabilities.supportsDelete',
    positiveType: 'info',
    negativeLabelKey: 'settings.storage.capabilities.notSupportsDelete',
    negativeType: 'warning',
  },
  {
    key: 'supportsDirectPublicUrl',
    positiveLabelKey: 'settings.storage.capabilities.supportsDirectPublicUrl',
    positiveType: 'info',
    negativeLabelKey: 'settings.storage.capabilities.noDirectPublicUrl',
    negativeType: 'default',
  },
  {
    key: 'requiresAccessProxy',
    positiveLabelKey: 'settings.storage.capabilities.requiresAccessProxy',
    positiveType: 'warning',
  },
  {
    key: 'recommendedForPrimaryStorage',
    positiveLabelKey: 'settings.storage.capabilities.recommendedForPrimaryStorage',
    positiveType: 'success',
    negativeLabelKey: 'settings.storage.capabilities.notRecommendedForPrimaryStorage',
    negativeType: 'warning',
  },
];

export interface CapabilityTagItem {
  key: string;
  label: string;
  type: CapabilityTagType;
}

export interface CapabilityDisplay {
  tags: CapabilityTagItem[];
  limitations: string[];
}

export interface SetDefaultCapabilityState {
  canSetDefault: boolean;
  disabledReason: string | null;
  warningHint: string | null;
}

export function buildCapabilityDisplay(
  capabilities: StorageProviderCapabilitiesResponse,
  translate: TranslateFn,
): CapabilityDisplay {
  const tags: CapabilityTagItem[] = [];
  const limitations: string[] = [];

  for (const descriptor of capabilityDescriptors) {
    const capabilityValue = capabilities[descriptor.key];

    if (capabilityValue) {
      tags.push({
        key: String(descriptor.key),
        label: translate(descriptor.positiveLabelKey),
        type: descriptor.positiveType,
      });
      continue;
    }

    if (!descriptor.negativeLabelKey) {
      continue;
    }

    const negativeLabel = translate(descriptor.negativeLabelKey);
    tags.push({
      key: `not-${String(descriptor.key)}`,
      label: negativeLabel,
      type: descriptor.negativeType ?? 'default',
    });

    const shouldTreatAsLimitation = descriptor.key === 'supportsPrivateRead'
      || descriptor.key === 'supportsVisibilityToggle'
      || descriptor.key === 'supportsDelete'
      || descriptor.key === 'recommendedForPrimaryStorage';

    if (shouldTreatAsLimitation) {
      limitations.push(negativeLabel);
    }
  }

  return {
    tags,
    limitations,
  };
}

export function getSetDefaultCapabilityState(
  profileEnabled: boolean,
  capabilities: StorageProviderCapabilitiesResponse,
  translate: TranslateFn,
): SetDefaultCapabilityState {
  if (!profileEnabled) {
    return {
      canSetDefault: false,
      disabledReason: translate('settings.storage.actionHints.setDefaultRequiresEnabled'),
      warningHint: null,
    };
  }

  if (!capabilities.recommendedForPrimaryStorage) {
    return {
      canSetDefault: true,
      disabledReason: null,
      warningHint: translate('settings.storage.actionHints.notRecommendedForPrimary'),
    };
  }

  return {
    canSetDefault: true,
    disabledReason: null,
    warningHint: null,
  };
}

export function getDeleteCapabilityHint(
  capabilities: StorageProviderCapabilitiesResponse,
  translate: TranslateFn,
): string | null {
  if (!capabilities.supportsDelete) {
    return translate('settings.storage.actionHints.assetDeleteLimited');
  }

  return null;
}
