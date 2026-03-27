<script setup lang="ts">
import { computed, h, onMounted, reactive, ref } from 'vue';
import type { DataTableColumns, SelectOption } from 'naive-ui';
import {
  NAlert,
  NButton,
  NCard,
  NDataTable,
  NDescriptions,
  NDescriptionsItem,
  NEmpty,
  NForm,
  NFormItem,
  NInput,
  NModal,
  NSelect,
  NSkeleton,
  NSpace,
  NSwitch,
  NTag,
  useDialog,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PageHeader from '../../components/common/PageHeader.vue';
import GitHubRepoProfileBrowserDrawer from '../settings/GitHubRepoProfileBrowserDrawer.vue';
import { extractApiErrorMessage } from '../../api/client/error';
import {
  createStorageProviderProfile,
  deleteStorageProviderProfile,
  getStorageProviderOverview,
  setDefaultStorageProviderProfile,
  updateStorageProviderProfile,
} from '../../api/system/storage.api';
import { useIsMobile } from '../../composables/useIsMobile';
import type {
  CreateStorageProviderProfileRequest,
  StorageProviderCapabilitiesResponse,
  StorageProviderConfigurationSummaryResponse,
  StorageProviderOverviewResponse,
  StorageProviderProfileResponse,
  StorageProviderType,
  UpdateStorageProviderProfileRequest,
} from '../../types/storage';
import {
  buildCapabilityDisplay,
  getDeleteCapabilityHint,
  getSetDefaultCapabilityState,
  type CapabilityTagItem,
} from '../settings/storage-capabilities';
import { formatDateTime } from '../../utils/format';

const STORAGE_TABLE_SCROLL_X = 1240;

type AlertType = 'success' | 'warning' | 'info';
type ProfileModalMode = 'create' | 'edit';
type ProfileActionType = 'set-default' | 'delete';
type S3VendorPresetId = 'custom' | 'aws-s3' | 'minio' | 'cloudflare-r2' | 'backblaze-b2' | 'alibaba-oss';

interface AlignmentPresentation {
  type: AlertType;
  titleKey: string;
}

interface S3VendorPresetDefinition {
  endpoint: string;
  region: string;
  forcePathStyle: boolean;
  regionHintKey: string;
  publicBaseUrlHintKey: string;
}

interface BadgeMeta {
  key: string;
  label: string;
  type: CapabilityTagItem['type'];
}

const alignmentPresentationMap: Record<string, AlignmentPresentation> = {
  db_default_profile_missing: {
    type: 'warning',
    titleKey: 'settings.storage.alignment.titles.dbDefaultProfileMissing',
  },
  db_default_profile_disabled: {
    type: 'warning',
    titleKey: 'settings.storage.alignment.titles.dbDefaultProfileDisabled',
  },
  runtime_matches_db_default_provider_type: {
    type: 'success',
    titleKey: 'settings.storage.alignment.titles.runtimeMatchesDbDefaultProviderType',
  },
  runtime_mismatches_db_default_provider_type: {
    type: 'warning',
    titleKey: 'settings.storage.alignment.titles.runtimeMismatchesDbDefaultProviderType',
  },
};

const s3VendorPresetDefinitions: Record<Exclude<S3VendorPresetId, 'custom'>, S3VendorPresetDefinition> = {
  'aws-s3': {
    endpoint: 'https://s3.<region>.amazonaws.com',
    region: 'us-east-1',
    forcePathStyle: false,
    regionHintKey: 'providers.s3Presets.regionHints.awsS3',
    publicBaseUrlHintKey: 'providers.s3Presets.publicBaseUrlHints.awsS3',
  },
  minio: {
    endpoint: 'http://127.0.0.1:9000',
    region: 'us-east-1',
    forcePathStyle: true,
    regionHintKey: 'providers.s3Presets.regionHints.minio',
    publicBaseUrlHintKey: 'providers.s3Presets.publicBaseUrlHints.minio',
  },
  'cloudflare-r2': {
    endpoint: 'https://<account-id>.r2.cloudflarestorage.com',
    region: 'auto',
    forcePathStyle: false,
    regionHintKey: 'providers.s3Presets.regionHints.cloudflareR2',
    publicBaseUrlHintKey: 'providers.s3Presets.publicBaseUrlHints.cloudflareR2',
  },
  'backblaze-b2': {
    endpoint: 'https://s3.<region>.backblazeb2.com',
    region: 'us-west-002',
    forcePathStyle: false,
    regionHintKey: 'providers.s3Presets.regionHints.backblazeB2',
    publicBaseUrlHintKey: 'providers.s3Presets.publicBaseUrlHints.backblazeB2',
  },
  'alibaba-oss': {
    endpoint: 'https://oss-<region>.aliyuncs.com',
    region: 'cn-hangzhou',
    forcePathStyle: false,
    regionHintKey: 'providers.s3Presets.regionHints.alibabaOss',
    publicBaseUrlHintKey: 'providers.s3Presets.publicBaseUrlHints.alibabaOss',
  },
};

const { t } = useI18n();
const message = useMessage();
const dialog = useDialog();
const { isMobile } = useIsMobile();

const storageLoading = ref(false);
const storageLoadErrorMessage = ref('');
const storageOverview = ref<StorageProviderOverviewResponse | null>(null);
const profileModalVisible = ref(false);
const profileModalMode = ref<ProfileModalMode>('create');
const profileModalSubmitting = ref(false);
const editingProfileId = ref<string | null>(null);
const profileActionLoadingKey = ref<string | null>(null);
const s3EndpointHostHint = ref('');
const githubRepoBrowserVisible = ref(false);
const githubRepoBrowserProfile = ref<StorageProviderProfileResponse | null>(null);
const s3ForcePathStyleTouched = ref(false);

const profileForm = reactive({
  name: '',
  displayName: '',
  providerType: 'local' as StorageProviderType,
  isEnabled: true,
  localRootPath: '',
  localPublicBaseUrl: '',
  s3Endpoint: '',
  s3Bucket: '',
  s3Region: '',
  s3PublicBaseUrl: '',
  s3ForcePathStyle: true,
  s3AccessKey: '',
  s3SecretKey: '',
  s3VendorPreset: 'custom' as S3VendorPresetId,
});

const providerTypeOptions = computed<SelectOption[]>(() => [
  {
    label: t('settings.storage.providers.local'),
    value: 'local',
  },
  {
    label: t('settings.storage.providers.s3Compatible'),
    value: 's3-compatible',
  },
]);

const supportedFormProviderTypes = new Set<StorageProviderType>(['local', 's3-compatible']);

const s3VendorPresetOptions = computed<SelectOption[]>(() => [
  {
    label: t('providers.s3Presets.options.custom'),
    value: 'custom',
  },
  {
    label: t('providers.s3Presets.options.awsS3'),
    value: 'aws-s3',
  },
  {
    label: t('providers.s3Presets.options.minio'),
    value: 'minio',
  },
  {
    label: t('providers.s3Presets.options.cloudflareR2'),
    value: 'cloudflare-r2',
  },
  {
    label: t('providers.s3Presets.options.backblazeB2'),
    value: 'backblaze-b2',
  },
  {
    label: t('providers.s3Presets.options.alibabaOss'),
    value: 'alibaba-oss',
  },
]);

const profileColumns = computed<DataTableColumns<StorageProviderProfileResponse>>(() => [
  {
    title: t('settings.storage.profileTable.columns.name'),
    key: 'name',
    minWidth: 220,
    render: (profile) => {
      const displayName = profile.displayName?.trim() || profile.name;
      const secondaryName = displayName === profile.name ? null : profile.name;

      return h(
        'div',
        { class: 'profile-name-cell' },
        [
          h('div', { class: 'profile-name-main' }, displayName),
          secondaryName
            ? h('div', { class: 'profile-name-sub' }, secondaryName)
            : null,
        ],
      );
    },
  },
  {
    title: t('settings.storage.profileTable.columns.providerType'),
    key: 'providerType',
    minWidth: 240,
    render: (profile) => {
      const badges = buildProviderCategoryBadges(profile);

      return h(
        'div',
        { class: 'provider-type-cell' },
        [
          h('div', { class: 'provider-type-name' }, profile.providerType),
          badges.length > 0
            ? h(
              NSpace,
              {
                size: 6,
                wrap: true,
              },
              {
                default: () => badges.map((badge) => h(
                  NTag,
                  {
                    size: 'small',
                    type: badge.type,
                    bordered: false,
                  },
                  { default: () => badge.label },
                )),
              },
            )
            : null,
        ],
      );
    },
  },
  {
    title: t('settings.storage.profileTable.columns.status'),
    key: 'status',
    minWidth: 170,
    render: (profile) => {
      const setDefaultState = getSetDefaultCapabilityState(profile.isEnabled, profile.capabilities, t);

      return h(
        NSpace,
        { size: 6, wrap: true },
        {
          default: () => [
            h(
              NTag,
              { size: 'small', type: profile.isEnabled ? 'success' : 'default', bordered: false },
              {
                default: () => (profile.isEnabled ? t('settings.storage.tags.enabled') : t('settings.storage.tags.disabled')),
              },
            ),
            profile.isDefault
              ? h(
                NTag,
                { size: 'small', type: 'warning', bordered: false },
                { default: () => t('settings.storage.tags.default') },
              )
              : null,
            setDefaultState.warningHint
              ? h(
                NTag,
                { size: 'small', type: 'warning', bordered: false },
                { default: () => t('settings.storage.tags.notRecommendedDefault') },
              )
              : null,
          ],
        },
      );
    },
  },
  {
    title: t('settings.storage.profileTable.columns.capabilities'),
    key: 'capabilities',
    minWidth: 330,
    render: (profile) => renderCapabilityDisplay(profile.capabilities),
  },
  {
    title: t('settings.storage.profileTable.columns.configurationSummary'),
    key: 'configurationSummary',
    minWidth: 320,
    render: (profile) => {
      const summaryText = formatConfigurationSummary(profile.configurationSummary);
      return h('div', { class: 'profile-summary-cell' }, summaryText);
    },
  },
  {
    title: t('settings.storage.profileTable.columns.updatedAtUtc'),
    key: 'updatedAtUtc',
    minWidth: 180,
    render: (profile) => formatDateTime(profile.updatedAtUtc),
  },
  {
    title: t('settings.storage.profileTable.columns.actions'),
    key: 'actions',
    minWidth: 210,
    render: (profile) => {
      const supportsGitHubRepoBrowse = profile.providerType === 'github-repo';
      const canBrowseGitHubRepo = supportsGitHubRepoBrowse && profile.isEnabled;
      const supportsEdit = isProviderTypeSupportedForForm(profile.providerType);
      const setDefaultState = getSetDefaultCapabilityState(profile.isEnabled, profile.capabilities, t);
      const deleteHint = getDeleteCapabilityHint(profile.capabilities, t);
      const setDefaultLoading = isProfileActionLoading(profile.id, 'set-default');
      const deleteLoading = isProfileActionLoading(profile.id, 'delete');
      const rowDisabled = storageLoading.value || profileModalSubmitting.value || setDefaultLoading || deleteLoading;
      const actionHints: string[] = [];

      if (!supportsEdit) {
        actionHints.push(t('settings.storage.actionHints.editNotSupportedForProviderType'));
      }

      if (supportsGitHubRepoBrowse && !canBrowseGitHubRepo) {
        actionHints.push(t('settings.storage.actionHints.githubRepoBrowseRequiresEnabled'));
      }

      if (setDefaultState.warningHint) {
        actionHints.push(setDefaultState.warningHint);
      }

      if (deleteHint) {
        actionHints.push(deleteHint);
      }

      return h(
        'div',
        { class: 'profile-actions-cell' },
        [
          h(
            NSpace,
            { size: 6, wrap: true, class: 'profile-actions' },
            {
              default: () => [
                supportsGitHubRepoBrowse
                  ? h(
                    NButton,
                    {
                      size: 'small',
                      quaternary: true,
                      type: 'info',
                      disabled: rowDisabled || !canBrowseGitHubRepo,
                      title: !canBrowseGitHubRepo ? t('settings.storage.actionHints.githubRepoBrowseRequiresEnabled') : undefined,
                      onClick: () => openGitHubRepoBrowser(profile),
                    },
                    { default: () => t('settings.storage.actions.browseGitHubRepo') },
                  )
                  : null,
                h(
                  NButton,
                  {
                    size: 'small',
                    quaternary: true,
                    disabled: rowDisabled || !supportsEdit,
                    title: !supportsEdit ? t('settings.storage.actionHints.editNotSupportedForProviderType') : undefined,
                    onClick: () => openEditProfileModal(profile),
                  },
                  { default: () => t('settings.storage.actions.edit') },
                ),
                !profile.isDefault
                  ? h(
                    NButton,
                    {
                      size: 'small',
                      quaternary: true,
                      type: 'warning',
                      loading: setDefaultLoading,
                      disabled: rowDisabled || !setDefaultState.canSetDefault,
                      title: !setDefaultState.canSetDefault ? setDefaultState.disabledReason ?? undefined : setDefaultState.warningHint ?? undefined,
                      onClick: () => confirmSetDefaultProfile(profile),
                    },
                    { default: () => t('settings.storage.actions.setDefault') },
                  )
                  : null,
                h(
                  NButton,
                  {
                    size: 'small',
                    quaternary: true,
                    type: 'error',
                    loading: deleteLoading,
                    disabled: rowDisabled,
                    title: deleteHint ?? undefined,
                    onClick: () => confirmDeleteProfile(profile),
                  },
                  { default: () => t('settings.storage.actions.delete') },
                ),
              ],
            },
          ),
          actionHints.length > 0
            ? h('div', { class: 'profile-action-hints' }, actionHints.join(' · '))
            : null,
        ],
      );
    },
  },
]);

const storageHasError = computed(() => storageLoadErrorMessage.value.length > 0);
const isEditMode = computed(() => profileModalMode.value === 'edit');

const profileModalTitle = computed(() => (
  isEditMode.value
    ? t('settings.storage.modal.editTitle')
    : t('settings.storage.modal.createTitle')
));

const profileModalSubmitText = computed(() => (
  isEditMode.value
    ? t('settings.storage.actions.save')
    : t('settings.storage.actions.create')
));

const alignmentPresentation = computed<AlignmentPresentation>(() => {
  const alignmentCode = storageOverview.value?.alignment.code;
  if (!alignmentCode) {
    return {
      type: 'info',
      titleKey: 'settings.storage.alignment.titles.unknown',
    };
  }

  return alignmentPresentationMap[alignmentCode] ?? {
    type: 'info',
    titleKey: 'settings.storage.alignment.titles.unknown',
  };
});

const runtimeCapabilityLabels = computed(() => {
  const runtimeCapabilities = storageOverview.value?.runtime.capabilities;
  if (!runtimeCapabilities) {
    return [] as CapabilityTagItem[];
  }

  return buildCapabilityDisplay(runtimeCapabilities, t).tags;
});

const defaultProfileCapabilityLabels = computed(() => {
  const defaultCapabilities = storageOverview.value?.defaultProfile?.capabilities;
  if (!defaultCapabilities) {
    return [] as CapabilityTagItem[];
  }

  return buildCapabilityDisplay(defaultCapabilities, t).tags;
});

const profileCountText = computed(() =>
  t('settings.storage.profileTable.count', {
    count: storageOverview.value?.profiles.length ?? 0,
  }),
);

const hasProfiles = computed(() => (storageOverview.value?.profiles.length ?? 0) > 0);

const selectedS3PresetDefinition = computed(() => {
  if (profileForm.s3VendorPreset === 'custom') {
    return null;
  }

  return s3VendorPresetDefinitions[profileForm.s3VendorPreset];
});

const selectedS3PresetRegionHint = computed(() => {
  if (!selectedS3PresetDefinition.value) {
    return '';
  }

  return t(selectedS3PresetDefinition.value.regionHintKey, {
    region: selectedS3PresetDefinition.value.region,
  });
});

const selectedS3PresetPublicBaseUrlHint = computed(() => {
  if (!selectedS3PresetDefinition.value) {
    return '';
  }

  return t(selectedS3PresetDefinition.value.publicBaseUrlHintKey);
});

function isProviderTypeSupportedForForm(providerType: string): providerType is StorageProviderType {
  return supportedFormProviderTypes.has(providerType as StorageProviderType);
}

function renderCapabilityTags(tags: CapabilityTagItem[]) {
  if (tags.length === 0) {
    return '-';
  }

  return h(
    NSpace,
    { size: 6, wrap: true },
    {
      default: () =>
        tags.map((tag) =>
          h(NTag, { size: 'small', type: tag.type, bordered: false }, { default: () => tag.label })),
    },
  );
}

function renderCapabilityDisplay(capabilities: StorageProviderCapabilitiesResponse) {
  const display = buildCapabilityDisplay(capabilities, t);
  const tagsBlock = renderCapabilityTags(display.tags);

  if (display.limitations.length === 0) {
    return tagsBlock;
  }

  return h(
    'div',
    { class: 'capability-cell' },
    [
      tagsBlock,
      h(
        'div',
        { class: 'capability-limitations' },
        `${t('settings.storage.capabilityLimitationsLabel')}: ${display.limitations.join(' / ')}`,
      ),
    ],
  );
}

function buildProviderCategoryBadges(profile: StorageProviderProfileResponse): BadgeMeta[] {
  const badges: BadgeMeta[] = [];
  const mainlineProviderTypes = new Set(['local', 's3-compatible']);

  if (mainlineProviderTypes.has(profile.providerType)) {
    badges.push({
      key: `track-mainline-${profile.id}`,
      label: t('providers.badges.mainline'),
      type: 'success',
    });
  } else if (profile.providerType === 'github-repo') {
    badges.push({
      key: `track-platform-${profile.id}`,
      label: t('providers.badges.platformType'),
      type: 'info',
    });
  } else if (profile.providerType === 'github-releases') {
    badges.push({
      key: `track-skeleton-${profile.id}`,
      label: t('providers.badges.skeletonType'),
      type: 'warning',
    });
  }

  if (profile.capabilities.isPlatformBacked) {
    badges.push({
      key: `platform-backed-${profile.id}`,
      label: t('providers.badges.platformBacked'),
      type: 'info',
    });
  }

  if (profile.capabilities.isExperimental) {
    badges.push({
      key: `experimental-${profile.id}`,
      label: t('providers.badges.experimental'),
      type: 'warning',
    });
  }

  if (!profile.capabilities.recommendedForPrimaryStorage) {
    badges.push({
      key: `not-recommended-${profile.id}`,
      label: t('providers.badges.notRecommendedPrimary'),
      type: 'warning',
    });
  }

  return badges;
}

function formatConfigurationSummary(summary: StorageProviderConfigurationSummaryResponse): string {
  const entries: string[] = [];

  if (summary.providerName) {
    entries.push(`provider=${summary.providerName}`);
  }

  if (summary.rootPath) {
    entries.push(`rootPath=${summary.rootPath}`);
  }

  if (summary.endpointHost) {
    entries.push(`endpointHost=${summary.endpointHost}`);
  }

  if (summary.bucketOrContainer) {
    entries.push(`bucketOrContainer=${summary.bucketOrContainer}`);
  }

  if (summary.region) {
    entries.push(`region=${summary.region}`);
  }

  if (summary.publicBaseUrl) {
    entries.push(`publicBaseUrl=${summary.publicBaseUrl}`);
  }

  if (summary.forcePathStyle !== null) {
    entries.push(`forcePathStyle=${summary.forcePathStyle ? 'true' : 'false'}`);
  }

  if (summary.owner) {
    entries.push(`owner=${summary.owner}`);
  }

  if (summary.repository) {
    entries.push(`repo=${summary.repository}`);
  }

  if (summary.reference) {
    entries.push(`ref=${summary.reference}`);
  }

  if (summary.basePath) {
    entries.push(`basePath=${summary.basePath}`);
  }

  if (summary.visibilityPolicy) {
    entries.push(`visibilityPolicy=${summary.visibilityPolicy}`);
  }

  if (summary.apiBaseUrl) {
    entries.push(`apiBaseUrl=${summary.apiBaseUrl}`);
  }

  if (summary.rawBaseUrl) {
    entries.push(`rawBaseUrl=${summary.rawBaseUrl}`);
  }

  if (entries.length === 0) {
    return '-';
  }

  return entries.join(' | ');
}

function formatBoolean(value: boolean | null): string {
  if (value === null) {
    return '-';
  }

  return value ? t('common.yes') : t('common.no');
}

function getProfileDisplayName(profile: StorageProviderProfileResponse): string {
  return profile.displayName?.trim() || profile.name;
}

function normalizeOptionalString(value: string): string | null {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : null;
}

function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const parsed = new URL(value);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

function createProfileActionKey(profileId: string, action: ProfileActionType): string {
  return `${profileId}:${action}`;
}

function isProfileActionLoading(profileId: string, action: ProfileActionType): boolean {
  return profileActionLoadingKey.value === createProfileActionKey(profileId, action);
}

function resetProfileForm(): void {
  profileForm.name = '';
  profileForm.displayName = '';
  profileForm.providerType = 'local';
  profileForm.isEnabled = true;
  profileForm.localRootPath = '';
  profileForm.localPublicBaseUrl = '';
  profileForm.s3Endpoint = '';
  profileForm.s3Bucket = '';
  profileForm.s3Region = '';
  profileForm.s3PublicBaseUrl = '';
  profileForm.s3ForcePathStyle = true;
  profileForm.s3AccessKey = '';
  profileForm.s3SecretKey = '';
  profileForm.s3VendorPreset = 'custom';
  s3ForcePathStyleTouched.value = false;
  s3EndpointHostHint.value = '';
}

function inferS3VendorPreset(summary: StorageProviderConfigurationSummaryResponse): S3VendorPresetId {
  const endpointHost = summary.endpointHost?.toLowerCase() ?? '';
  const providerName = summary.providerName?.toLowerCase() ?? '';

  if (endpointHost.includes('amazonaws.com') || providerName.includes('aws')) {
    return 'aws-s3';
  }

  if (endpointHost.includes('cloudflarestorage.com') || providerName.includes('r2')) {
    return 'cloudflare-r2';
  }

  if (endpointHost.includes('backblazeb2.com') || providerName.includes('backblaze')) {
    return 'backblaze-b2';
  }

  if (endpointHost.includes('aliyuncs.com') || providerName.includes('oss') || providerName.includes('alibaba')) {
    return 'alibaba-oss';
  }

  if (endpointHost.includes('minio') || endpointHost.includes('9000') || providerName.includes('minio')) {
    return 'minio';
  }

  return 'custom';
}

function openCreateProfileModal(): void {
  profileModalMode.value = 'create';
  editingProfileId.value = null;
  resetProfileForm();
  profileModalVisible.value = true;
}

function openEditProfileModal(profile: StorageProviderProfileResponse): void {
  if (!isProviderTypeSupportedForForm(profile.providerType)) {
    message.error(t('settings.storage.messages.providerTypeNotSupported'));
    return;
  }

  profileModalMode.value = 'edit';
  editingProfileId.value = profile.id;
  resetProfileForm();

  profileForm.name = profile.name;
  profileForm.displayName = profile.displayName ?? '';
  profileForm.providerType = profile.providerType;
  profileForm.isEnabled = profile.isEnabled;

  if (profile.providerType === 'local') {
    profileForm.localRootPath = profile.configurationSummary.rootPath ?? '';
    profileForm.localPublicBaseUrl = profile.configurationSummary.publicBaseUrl ?? '';
  }

  if (profile.providerType === 's3-compatible') {
    profileForm.s3Bucket = profile.configurationSummary.bucketOrContainer ?? '';
    profileForm.s3Region = profile.configurationSummary.region ?? '';
    profileForm.s3PublicBaseUrl = profile.configurationSummary.publicBaseUrl ?? '';
    profileForm.s3ForcePathStyle = profile.configurationSummary.forcePathStyle ?? true;
    profileForm.s3VendorPreset = inferS3VendorPreset(profile.configurationSummary);
    profileForm.s3Endpoint = '';
    s3EndpointHostHint.value = profile.configurationSummary.endpointHost ?? '';
    s3ForcePathStyleTouched.value = true;
  }

  profileModalVisible.value = true;
}

function openGitHubRepoBrowser(profile: StorageProviderProfileResponse): void {
  if (profile.providerType !== 'github-repo') {
    return;
  }

  if (!profile.isEnabled) {
    message.warning(t('settings.storage.actionHints.githubRepoBrowseRequiresEnabled'));
    return;
  }

  githubRepoBrowserProfile.value = profile;
  githubRepoBrowserVisible.value = true;
}

function handleGitHubRepoBrowserVisibleChange(show: boolean): void {
  githubRepoBrowserVisible.value = show;

  if (!show) {
    githubRepoBrowserProfile.value = null;
  }
}

function closeProfileModal(): void {
  if (profileModalSubmitting.value) {
    return;
  }

  profileModalVisible.value = false;
}

function handleS3ForcePathStyleChange(value: boolean): void {
  profileForm.s3ForcePathStyle = value;
  s3ForcePathStyleTouched.value = true;
}

function applySelectedS3Preset(): void {
  if (profileForm.providerType !== 's3-compatible' || profileForm.s3VendorPreset === 'custom') {
    return;
  }

  const preset = s3VendorPresetDefinitions[profileForm.s3VendorPreset];
  const appliedFields: string[] = [];
  const preservedFields: string[] = [];

  if (!profileForm.s3Endpoint.trim()) {
    profileForm.s3Endpoint = preset.endpoint;
    appliedFields.push(t('providers.s3Presets.fields.endpoint'));
  } else {
    preservedFields.push(t('providers.s3Presets.fields.endpoint'));
  }

  if (!profileForm.s3Region.trim()) {
    profileForm.s3Region = preset.region;
    appliedFields.push(t('providers.s3Presets.fields.region'));
  } else {
    preservedFields.push(t('providers.s3Presets.fields.region'));
  }

  if (!s3ForcePathStyleTouched.value) {
    profileForm.s3ForcePathStyle = preset.forcePathStyle;
    appliedFields.push(t('providers.s3Presets.fields.forcePathStyle'));
  } else {
    preservedFields.push(t('providers.s3Presets.fields.forcePathStyle'));
  }

  const messages: string[] = [];
  if (appliedFields.length > 0) {
    messages.push(t('providers.s3Presets.messages.applied', { fields: appliedFields.join(' / ') }));
  }

  if (preservedFields.length > 0) {
    messages.push(t('providers.s3Presets.messages.preserved', { fields: preservedFields.join(' / ') }));
  }

  if (messages.length === 0) {
    message.info(t('providers.s3Presets.messages.noChange'));
    return;
  }

  message.info(messages.join('；'));
}

function validateProfileForm(): string | null {
  const name = profileForm.name.trim();
  if (!name) {
    return t('settings.storage.validation.nameRequired');
  }

  if (profileForm.providerType === 'local') {
    const localRootPath = profileForm.localRootPath.trim();
    if (!localRootPath) {
      return t('settings.storage.validation.localRootPathRequired');
    }

    const localPublicBaseUrl = profileForm.localPublicBaseUrl.trim();
    if (localPublicBaseUrl && !isAbsoluteHttpUrl(localPublicBaseUrl)) {
      return t('settings.storage.validation.publicBaseUrlInvalid');
    }

    return null;
  }

  const endpoint = profileForm.s3Endpoint.trim();
  const bucket = profileForm.s3Bucket.trim();
  const region = profileForm.s3Region.trim();

  if (!endpoint) {
    return t('settings.storage.validation.s3EndpointRequired');
  }

  if (!isAbsoluteHttpUrl(endpoint)) {
    return t('settings.storage.validation.s3EndpointInvalid');
  }

  if (!bucket) {
    return t('settings.storage.validation.s3BucketRequired');
  }

  if (!region) {
    return t('settings.storage.validation.s3RegionRequired');
  }

  const s3PublicBaseUrl = profileForm.s3PublicBaseUrl.trim();
  if (s3PublicBaseUrl && !isAbsoluteHttpUrl(s3PublicBaseUrl)) {
    return t('settings.storage.validation.publicBaseUrlInvalid');
  }

  const accessKey = profileForm.s3AccessKey.trim();
  const secretKey = profileForm.s3SecretKey.trim();

  if (!isEditMode.value) {
    if (!accessKey) {
      return t('settings.storage.validation.s3AccessKeyRequired');
    }

    if (!secretKey) {
      return t('settings.storage.validation.s3SecretKeyRequired');
    }

    return null;
  }

  if ((accessKey && !secretKey) || (!accessKey && secretKey)) {
    return t('settings.storage.validation.s3SecretPairRequired');
  }

  return null;
}

function buildLocalConfiguration(): Record<string, unknown> {
  const configuration: Record<string, unknown> = {
    rootPath: profileForm.localRootPath.trim(),
    createDirectoryIfMissing: true,
  };

  const publicBaseUrl = normalizeOptionalString(profileForm.localPublicBaseUrl);
  if (publicBaseUrl) {
    configuration.publicBaseUrl = publicBaseUrl;
  }

  return configuration;
}

function resolveS3ProviderName(): string {
  if (profileForm.s3VendorPreset === 'custom') {
    return 's3';
  }

  return profileForm.s3VendorPreset;
}

function buildS3Configuration(): Record<string, unknown> {
  const configuration: Record<string, unknown> = {
    providerName: resolveS3ProviderName(),
    endpoint: profileForm.s3Endpoint.trim(),
    bucket: profileForm.s3Bucket.trim(),
    region: profileForm.s3Region.trim(),
    forcePathStyle: profileForm.s3ForcePathStyle,
  };

  const publicBaseUrl = normalizeOptionalString(profileForm.s3PublicBaseUrl);
  if (publicBaseUrl) {
    configuration.publicBaseUrl = publicBaseUrl;
  }

  return configuration;
}

function buildS3SecretConfiguration(): Record<string, unknown> {
  return {
    accessKey: profileForm.s3AccessKey.trim(),
    secretKey: profileForm.s3SecretKey.trim(),
  };
}

async function submitProfileModal(): Promise<void> {
  const validationError = validateProfileForm();
  if (validationError) {
    message.error(validationError);
    return;
  }

  profileModalSubmitting.value = true;

  try {
    if (!isEditMode.value) {
      const createRequest: CreateStorageProviderProfileRequest = {
        name: profileForm.name.trim(),
        displayName: normalizeOptionalString(profileForm.displayName),
        providerType: profileForm.providerType,
        isEnabled: profileForm.isEnabled,
        configuration: profileForm.providerType === 'local'
          ? buildLocalConfiguration()
          : buildS3Configuration(),
      };

      if (profileForm.providerType === 's3-compatible') {
        createRequest.secretConfiguration = buildS3SecretConfiguration();
      }

      await createStorageProviderProfile(createRequest);
      message.success(t('settings.storage.messages.createSuccess'));
    } else {
      if (!editingProfileId.value) {
        message.error(t('settings.storage.messages.missingEditingProfile'));
        return;
      }

      const updateRequest: UpdateStorageProviderProfileRequest = {
        name: profileForm.name.trim(),
        displayName: normalizeOptionalString(profileForm.displayName),
        isEnabled: profileForm.isEnabled,
        configuration: profileForm.providerType === 'local'
          ? buildLocalConfiguration()
          : buildS3Configuration(),
      };

      if (profileForm.providerType === 's3-compatible') {
        const nextAccessKey = profileForm.s3AccessKey.trim();
        const nextSecretKey = profileForm.s3SecretKey.trim();
        if (nextAccessKey && nextSecretKey) {
          updateRequest.secretConfiguration = buildS3SecretConfiguration();
        }
      }

      await updateStorageProviderProfile(editingProfileId.value, updateRequest);
      message.success(t('settings.storage.messages.updateSuccess'));
    }

    profileModalVisible.value = false;
    await fetchStorageOverview();
  } catch (error) {
    message.error(`${t('settings.storage.messages.saveFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    profileModalSubmitting.value = false;
  }
}

async function runProfileAction(
  profileId: string,
  actionType: ProfileActionType,
  action: () => Promise<void>,
): Promise<void> {
  if (profileActionLoadingKey.value) {
    return;
  }

  const actionKey = createProfileActionKey(profileId, actionType);
  profileActionLoadingKey.value = actionKey;

  try {
    await action();
  } finally {
    profileActionLoadingKey.value = null;
  }
}

function confirmSetDefaultProfile(profile: StorageProviderProfileResponse): void {
  const setDefaultState = getSetDefaultCapabilityState(profile.isEnabled, profile.capabilities, t);

  if (!setDefaultState.canSetDefault) {
    message.warning(setDefaultState.disabledReason ?? t('settings.storage.messages.setDefaultRequiresEnabled'));
    return;
  }

  const profileName = getProfileDisplayName(profile);
  const confirmationContent = setDefaultState.warningHint
    ? `${t('settings.storage.confirmations.setDefault', { name: profileName })} ${setDefaultState.warningHint}`
    : t('settings.storage.confirmations.setDefault', { name: profileName });

  dialog.warning({
    title: t('settings.storage.actions.setDefault'),
    content: confirmationContent,
    positiveText: t('settings.storage.actions.setDefault'),
    negativeText: t('common.cancel'),
    onPositiveClick: async () => {
      await runProfileAction(profile.id, 'set-default', async () => {
        try {
          await setDefaultStorageProviderProfile(profile.id);
          message.success(t('settings.storage.messages.setDefaultSuccess'));
          await fetchStorageOverview();
        } catch (error) {
          message.error(`${t('settings.storage.messages.setDefaultFailed')}: ${extractApiErrorMessage(error)}`);
        }
      });
    },
  });
}

function confirmDeleteProfile(profile: StorageProviderProfileResponse): void {
  const profileName = getProfileDisplayName(profile);
  dialog.warning({
    title: t('settings.storage.actions.delete'),
    content: t('settings.storage.confirmations.delete', { name: profileName }),
    positiveText: t('settings.storage.actions.delete'),
    negativeText: t('common.cancel'),
    onPositiveClick: async () => {
      await runProfileAction(profile.id, 'delete', async () => {
        try {
          await deleteStorageProviderProfile(profile.id);
          message.success(t('settings.storage.messages.deleteSuccess'));
          await fetchStorageOverview();
        } catch (error) {
          message.error(`${t('settings.storage.messages.deleteFailed')}: ${extractApiErrorMessage(error)}`);
        }
      });
    },
  });
}

async function fetchStorageOverview(): Promise<void> {
  storageLoading.value = true;
  storageLoadErrorMessage.value = '';

  try {
    storageOverview.value = await getStorageProviderOverview();
  } catch (error) {
    storageLoadErrorMessage.value = extractApiErrorMessage(error);
  } finally {
    storageLoading.value = false;
  }
}

onMounted(() => {
  void fetchStorageOverview();
});
</script>

<template>
  <div>
    <page-header :title="t('providers.title')" :description="t('providers.description')">
      <template #actions>
        <n-space :wrap="true">
          <n-button size="small" :loading="storageLoading" @click="fetchStorageOverview">
            {{ t('common.refresh') }}
          </n-button>
          <n-button size="small" type="primary" secondary :disabled="storageLoading || profileModalSubmitting" @click="openCreateProfileModal">
            {{ t('settings.storage.actions.create') }}
          </n-button>
        </n-space>
      </template>
    </page-header>

    <n-card class="providers-card" :title="t('providers.overview.title')">

      <n-space vertical :size="12">
        <n-alert v-if="storageHasError" type="error" :show-icon="false">
          <div class="providers-error-row">
            <span>{{ t('settings.storage.loadFailed') }}: {{ storageLoadErrorMessage }}</span>
            <n-button text size="small" :loading="storageLoading" @click="fetchStorageOverview">
              {{ t('common.retry') }}
            </n-button>
          </div>
        </n-alert>

        <n-alert type="info" :show-icon="false">
          {{ t('providers.overview.runtimeBackground') }}
        </n-alert>

        <template v-if="storageLoading && !storageOverview">
          <n-skeleton text :repeat="4" />
          <n-skeleton text :repeat="7" />
        </template>

        <template v-else-if="storageOverview">
          <n-alert
            :type="alignmentPresentation.type"
            :title="t(alignmentPresentation.titleKey)"
          >
            <div class="alignment-message">{{ storageOverview.alignment.message }}</div>
            <n-space wrap :size="8" class="alignment-meta">
              <n-tag size="small">{{ storageOverview.alignment.code }}</n-tag>
              <n-tag size="small" type="info" :bordered="false">
                {{ t('settings.storage.alignment.runtimeSelectionSource') }}:
                {{ storageOverview.alignment.runtimeSelectionSource }}
              </n-tag>
            </n-space>
          </n-alert>

          <div class="providers-overview-grid">
            <div class="providers-section">
              <h3 class="providers-section__title">{{ t('providers.overview.runtimeTitle') }}</h3>
              <n-descriptions bordered size="small" :column="isMobile ? 1 : 2">
                <n-descriptions-item :label="t('settings.storage.runtime.providerType')">
                  {{ storageOverview.runtime.providerType }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.runtime.providerName')">
                  {{ storageOverview.runtime.providerName }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.runtime.isConfigurationDriven')">
                  {{ storageOverview.runtime.isConfigurationDriven ? t('common.yes') : t('common.no') }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.runtime.matchesDefaultProfileType')">
                  {{ formatBoolean(storageOverview.runtime.matchesDefaultProfileType) }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.runtime.capabilities')">
                  <n-space wrap :size="6">
                    <n-tag
                      v-for="tag in runtimeCapabilityLabels"
                      :key="`runtime-${tag.key}`"
                      size="small"
                      :type="tag.type"
                      :bordered="false"
                    >
                      {{ tag.label }}
                    </n-tag>
                    <span v-if="runtimeCapabilityLabels.length === 0">-</span>
                  </n-space>
                  <div class="capability-source-hint">
                    {{ t('settings.storage.runtime.capabilityHint') }}
                  </div>
                </n-descriptions-item>
              </n-descriptions>
            </div>

            <div class="providers-section">
              <h3 class="providers-section__title">{{ t('providers.overview.defaultWriteTargetTitle') }}</h3>
              <n-descriptions
                v-if="storageOverview.defaultProfile"
                bordered
                size="small"
                :column="isMobile ? 1 : 2"
              >
                <n-descriptions-item :label="t('settings.storage.defaultProfile.name')">
                  {{ storageOverview.defaultProfile.displayName || storageOverview.defaultProfile.name }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.defaultProfile.providerType')">
                  {{ storageOverview.defaultProfile.providerType }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.defaultProfile.isEnabled')">
                  {{ storageOverview.defaultProfile.isEnabled ? t('common.yes') : t('common.no') }}
                </n-descriptions-item>
                <n-descriptions-item :label="t('settings.storage.defaultProfile.capabilities')">
                  <n-space wrap :size="6">
                    <n-tag
                      v-for="tag in defaultProfileCapabilityLabels"
                      :key="`default-${tag.key}`"
                      size="small"
                      :type="tag.type"
                      :bordered="false"
                    >
                      {{ tag.label }}
                    </n-tag>
                    <span v-if="defaultProfileCapabilityLabels.length === 0">-</span>
                  </n-space>
                  <div class="capability-source-hint">
                    {{ t('providers.overview.defaultWriteTargetHint') }}
                  </div>
                </n-descriptions-item>
              </n-descriptions>

              <n-alert v-else type="info" :show-icon="false">
                {{ t('settings.storage.defaultProfile.missing') }}
              </n-alert>
            </div>
          </div>

          <div class="providers-section">
            <h3 class="providers-section__title">{{ t('providers.profileManagement.title') }}</h3>
            <div class="provider-legend-row">
              <n-space wrap :size="8">
                <n-tag size="small" type="success" :bordered="false">{{ t('providers.legend.mainline') }}</n-tag>
                <n-tag size="small" type="info" :bordered="false">{{ t('providers.legend.platform') }}</n-tag>
                <n-tag size="small" type="warning" :bordered="false">{{ t('providers.legend.skeleton') }}</n-tag>
              </n-space>
              <span class="provider-legend-meta">{{ profileCountText }}</span>
            </div>

            <n-empty
              v-if="!hasProfiles"
              :description="t('settings.storage.profileTable.empty')"
              style="padding: 28px 0"
            />

            <div v-else class="storage-table-wrapper">
              <n-data-table
                size="small"
                :columns="profileColumns"
                :data="storageOverview.profiles"
                :row-key="(row) => row.id"
                :scroll-x="STORAGE_TABLE_SCROLL_X"
              />
            </div>
          </div>
        </template>
      </n-space>
    </n-card>

    <n-modal
      v-model:show="profileModalVisible"
      preset="card"
      class="profile-modal"
      :title="profileModalTitle"
      :mask-closable="!profileModalSubmitting"
      :closable="!profileModalSubmitting"
    >
      <n-form label-placement="top">
        <n-form-item :label="t('settings.storage.form.name')">
          <n-input
            v-model:value="profileForm.name"
            :placeholder="t('settings.storage.form.namePlaceholder')"
          />
        </n-form-item>

        <n-form-item :label="t('settings.storage.form.displayName')">
          <n-input
            v-model:value="profileForm.displayName"
            :placeholder="t('settings.storage.form.displayNamePlaceholder')"
          />
        </n-form-item>

        <n-form-item :label="t('settings.storage.form.providerType')">
          <n-select
            v-model:value="profileForm.providerType"
            :options="providerTypeOptions"
            :disabled="isEditMode"
          />
        </n-form-item>

        <n-form-item :label="t('settings.storage.form.isEnabled')">
          <n-switch v-model:value="profileForm.isEnabled" />
        </n-form-item>

        <template v-if="profileForm.providerType === 'local'">
          <n-form-item :label="t('settings.storage.form.local.rootPath')">
            <n-input
              v-model:value="profileForm.localRootPath"
              :placeholder="t('settings.storage.form.local.rootPathPlaceholder')"
            />
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.local.publicBaseUrl')">
            <n-input
              v-model:value="profileForm.localPublicBaseUrl"
              :placeholder="t('settings.storage.form.local.publicBaseUrlPlaceholder')"
            />
          </n-form-item>
        </template>

        <template v-else>
          <n-form-item :label="t('providers.s3Presets.label')">
            <n-space class="s3-preset-row" :size="8" :wrap="true">
              <n-select
                v-model:value="profileForm.s3VendorPreset"
                class="s3-preset-select"
                :options="s3VendorPresetOptions"
              />
              <n-button
                size="small"
                secondary
                :disabled="profileForm.s3VendorPreset === 'custom'"
                @click="applySelectedS3Preset"
              >
                {{ t('providers.s3Presets.applyToEmpty') }}
              </n-button>
            </n-space>
            <div v-if="selectedS3PresetDefinition" class="form-hint form-hint--stack">
              <div>{{ t('providers.s3Presets.endpointHint', { endpoint: selectedS3PresetDefinition.endpoint }) }}</div>
              <div>{{ selectedS3PresetRegionHint }}</div>
              <div>{{ selectedS3PresetPublicBaseUrlHint }}</div>
            </div>
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.s3.endpoint')">
            <n-input
              v-model:value="profileForm.s3Endpoint"
              :placeholder="t('settings.storage.form.s3.endpointPlaceholder')"
            />
            <div v-if="isEditMode && s3EndpointHostHint" class="form-hint">
              {{ t('settings.storage.form.s3.endpointHint', { endpointHost: s3EndpointHostHint }) }}
            </div>
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.s3.bucket')">
            <n-input
              v-model:value="profileForm.s3Bucket"
              :placeholder="t('settings.storage.form.s3.bucketPlaceholder')"
            />
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.s3.region')">
            <n-input
              v-model:value="profileForm.s3Region"
              :placeholder="t('settings.storage.form.s3.regionPlaceholder')"
            />
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.s3.publicBaseUrl')">
            <n-input
              v-model:value="profileForm.s3PublicBaseUrl"
              :placeholder="t('settings.storage.form.s3.publicBaseUrlPlaceholder')"
            />
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.s3.forcePathStyle')">
            <n-switch :value="profileForm.s3ForcePathStyle" @update:value="handleS3ForcePathStyleChange" />
          </n-form-item>

          <n-alert v-if="isEditMode" type="info" :show-icon="false" class="form-alert">
            {{ t('settings.storage.form.s3.secretUpdateHint') }}
          </n-alert>

          <n-form-item :label="t('settings.storage.form.s3.accessKey')">
            <n-input
              v-model:value="profileForm.s3AccessKey"
              type="password"
              show-password-on="mousedown"
              :placeholder="t('settings.storage.form.s3.accessKeyPlaceholder')"
            />
          </n-form-item>

          <n-form-item :label="t('settings.storage.form.s3.secretKey')">
            <n-input
              v-model:value="profileForm.s3SecretKey"
              type="password"
              show-password-on="mousedown"
              :placeholder="t('settings.storage.form.s3.secretKeyPlaceholder')"
            />
          </n-form-item>
        </template>
      </n-form>

      <template #footer>
        <div class="profile-modal-footer">
          <n-space justify="end" :wrap="true">
            <n-button :disabled="profileModalSubmitting" @click="closeProfileModal">
              {{ t('common.cancel') }}
            </n-button>
            <n-button type="primary" :loading="profileModalSubmitting" @click="submitProfileModal">
              {{ profileModalSubmitText }}
            </n-button>
          </n-space>
        </div>
      </template>
    </n-modal>

    <git-hub-repo-profile-browser-drawer
      :show="githubRepoBrowserVisible"
      :profile="githubRepoBrowserProfile"
      @update:show="handleGitHubRepoBrowserVisibleChange"
    />
  </div>
</template>

<style scoped>
.providers-card + .providers-card {
  margin-top: 16px;
}

.providers-error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.alignment-message {
  line-height: 1.6;
}

.alignment-meta {
  margin-top: 8px;
}

.providers-overview-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.providers-section {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 12px;
  background: #fafafa;
}

.providers-section__title {
  margin: 0 0 10px;
  font-size: 15px;
  font-weight: 600;
  color: #111827;
}

.provider-legend-row {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  flex-wrap: wrap;
  align-items: center;
}

.provider-legend-meta {
  color: #6b7280;
  font-size: 13px;
}

.storage-table-wrapper {
  overflow-x: auto;
}

.provider-type-cell {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.provider-type-name {
  color: #111827;
  font-weight: 600;
}

.profile-name-cell {
  min-width: 0;
}

.profile-name-main {
  font-weight: 600;
  color: #111827;
  word-break: break-word;
}

.profile-name-sub {
  margin-top: 4px;
  color: #6b7280;
  font-size: 12px;
  word-break: break-all;
}

.profile-summary-cell {
  color: #374151;
  line-height: 1.5;
  white-space: normal;
  word-break: break-all;
}

.capability-cell {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.capability-limitations {
  font-size: 12px;
  color: #9a3412;
  line-height: 1.5;
}

.capability-source-hint {
  margin-top: 8px;
  font-size: 12px;
  color: #6b7280;
  line-height: 1.5;
}

.profile-actions-cell {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.profile-action-hints {
  color: #6b7280;
  font-size: 12px;
  line-height: 1.5;
}

.profile-modal {
  width: min(760px, calc(100vw - 24px));
}

.profile-modal-footer {
  width: 100%;
}

.s3-preset-row {
  width: 100%;
  align-items: center;
}

.s3-preset-select {
  width: min(260px, 100%);
}

.form-hint {
  margin-top: 6px;
  color: #6b7280;
  font-size: 12px;
  line-height: 1.5;
}

.form-hint--stack {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-alert {
  margin-bottom: 12px;
}

@media (max-width: 1024px) {
  .providers-overview-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .providers-section {
    padding: 10px;
  }

  .providers-section__title {
    margin-bottom: 8px;
  }

  .s3-preset-select {
    width: 100%;
  }

  .s3-preset-row :deep(.n-space-item) {
    width: 100%;
  }

  .s3-preset-row :deep(.n-space-item .n-button) {
    width: 100%;
  }

  .profile-modal-footer :deep(.n-space) {
    width: 100%;
  }

  .profile-modal-footer :deep(.n-space-item) {
    flex: 1 1 auto;
  }

  .profile-modal-footer :deep(.n-space-item .n-button) {
    width: 100%;
  }
}
</style>
