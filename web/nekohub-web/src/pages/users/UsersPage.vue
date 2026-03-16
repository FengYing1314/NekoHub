<script setup lang="ts">
import { computed, h, onMounted, reactive, ref } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import {
  NAlert,
  NButton,
  NCard,
  NCheckbox,
  NCheckboxGroup,
  NDataTable,
  NEmpty,
  NForm,
  NFormItem,
  NInput,
  NModal,
  NResult,
  NSelect,
  NSpace,
  NSwitch,
  NTag,
  useDialog,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PageHeader from '../../components/common/PageHeader.vue';
import { extractApiErrorMessage } from '../../api/client/error';
import {
  createUser,
  listUsers,
  resetUserPassword,
  updateUser,
  updateUserPermissions,
} from '../../api/users/users.api';
import { PERMISSION_OPTIONS, PERMISSIONS } from '../../constants/permissions';
import { useAuthPermissions } from '../../composables/useAuthPermissions';
import type { PermissionKey, UserRole } from '../../types/auth';
import type { UserListItemResponse } from '../../types/users';
import { formatDateTime } from '../../utils/format';

type UserModalMode = 'create' | 'edit';

const PERMISSION_LABEL_KEYS: Record<PermissionKey, string> = {
  [PERMISSIONS.assetsRead]: 'users.permissions.assetsRead',
  [PERMISSIONS.assetsCreate]: 'users.permissions.assetsCreate',
  [PERMISSIONS.assetsUpdate]: 'users.permissions.assetsUpdate',
  [PERMISSIONS.assetsDelete]: 'users.permissions.assetsDelete',
  [PERMISSIONS.providersRead]: 'users.permissions.providersRead',
  [PERMISSIONS.providersCreate]: 'users.permissions.providersCreate',
  [PERMISSIONS.providersUpdate]: 'users.permissions.providersUpdate',
  [PERMISSIONS.providersDelete]: 'users.permissions.providersDelete',
  [PERMISSIONS.aiProvidersRead]: 'users.permissions.aiProvidersRead',
  [PERMISSIONS.aiProvidersCreate]: 'users.permissions.aiProvidersCreate',
  [PERMISSIONS.aiProvidersUpdate]: 'users.permissions.aiProvidersUpdate',
  [PERMISSIONS.aiProvidersDelete]: 'users.permissions.aiProvidersDelete',
  [PERMISSIONS.settingsRead]: 'users.permissions.settingsRead',
  [PERMISSIONS.settingsUpdate]: 'users.permissions.settingsUpdate',
  [PERMISSIONS.usersRead]: 'users.permissions.usersRead',
  [PERMISSIONS.usersCreate]: 'users.permissions.usersCreate',
  [PERMISSIONS.usersUpdate]: 'users.permissions.usersUpdate',
  [PERMISSIONS.usersDisable]: 'users.permissions.usersDisable',
  [PERMISSIONS.usersManagePermissions]: 'users.permissions.usersManagePermissions',
};

const { t } = useI18n();
const message = useMessage();
const dialog = useDialog();
const { authStore, can } = useAuthPermissions();

const loading = ref(false);
const loadErrorMessage = ref('');
const users = ref<UserListItemResponse[]>([]);
const userModalVisible = ref(false);
const userModalMode = ref<UserModalMode>('create');
const userModalSubmitting = ref(false);
const editingUser = ref<UserListItemResponse | null>(null);
const permissionsModalVisible = ref(false);
const permissionsModalSubmitting = ref(false);
const permissionsUser = ref<UserListItemResponse | null>(null);
const permissionDraft = ref<PermissionKey[]>([]);

const formModel = reactive({
  username: '',
  password: '',
  role: 'user' as UserRole,
  isActive: true,
});

const validationState = reactive({
  username: '',
  password: '',
  role: '',
});

const canCreateUser = computed(() => can(PERMISSIONS.usersCreate));
const canUpdateUser = computed(() => can(PERMISSIONS.usersUpdate));
const canDisableUser = computed(() => can(PERMISSIONS.usersDisable));
const canManagePermissions = computed(() => can(PERMISSIONS.usersManagePermissions));
const isCurrentSuperAdmin = computed(() => authStore.role === 'superAdmin');
const isCurrentAdmin = computed(() => authStore.role === 'admin');
const isUsersReadOnly = computed(() => (
  !canCreateUser.value
  && !canUpdateUser.value
  && !canDisableUser.value
  && !canManagePermissions.value
));

const roleOptions = computed(() => {
  if (isCurrentSuperAdmin.value) {
    return [
      { label: t('auth.role.admin'), value: 'admin' },
      { label: t('auth.role.user'), value: 'user' },
    ];
  }

  return [{ label: t('auth.role.user'), value: 'user' }];
});

const userModalTitle = computed(() => (
  userModalMode.value === 'create'
    ? t('users.createTitle')
    : t('users.editTitle')
));

const hasLoadError = computed(() => loadErrorMessage.value.length > 0);
const showLoadErrorResult = computed(() => hasLoadError.value && users.value.length === 0);
const isEmpty = computed(() => !loading.value && !hasLoadError.value && users.value.length === 0);

const permissionOptions = computed(() => PERMISSION_OPTIONS.map((permission) => ({
  label: t(PERMISSION_LABEL_KEYS[permission]),
  value: permission,
})));

function clearValidation(): void {
  validationState.username = '';
  validationState.password = '';
  validationState.role = '';
}

function canOperateTarget(target: UserListItemResponse): boolean {
  if (isCurrentSuperAdmin.value) {
    return true;
  }

  if (isCurrentAdmin.value) {
    return target.role === 'user';
  }

  return false;
}

function validateUserForm(): boolean {
  clearValidation();
  const username = formModel.username.trim();
  if (!username) {
    validationState.username = t('users.validation.usernameRequired');
  }

  if (userModalMode.value === 'create' && !formModel.password) {
    validationState.password = t('users.validation.passwordRequired');
  }

  if (!formModel.role) {
    validationState.role = t('users.validation.roleRequired');
  }

  return !validationState.username && !validationState.password && !validationState.role;
}

function resetUserForm(): void {
  formModel.username = '';
  formModel.password = '';
  formModel.role = 'user';
  formModel.isActive = true;
  clearValidation();
}

async function loadUsers(showErrorMessage = true): Promise<void> {
  loading.value = true;
  loadErrorMessage.value = '';

  try {
    users.value = await listUsers();
  } catch (error) {
    loadErrorMessage.value = extractApiErrorMessage(error);
    if (showErrorMessage) {
      message.error(`${t('users.messages.loadFailed')}: ${loadErrorMessage.value}`);
    }
  } finally {
    loading.value = false;
  }
}

function openCreateModal(): void {
  if (!canCreateUser.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  userModalMode.value = 'create';
  editingUser.value = null;
  resetUserForm();
  userModalVisible.value = true;
}

function openEditModal(target: UserListItemResponse): void {
  if (!canUpdateUser.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!canOperateTarget(target)) {
    message.warning(t('users.messages.operationForbidden'));
    return;
  }

  userModalMode.value = 'edit';
  editingUser.value = target;
  formModel.username = target.username;
  formModel.password = '';
  formModel.role = target.role;
  formModel.isActive = target.isActive;
  clearValidation();
  userModalVisible.value = true;
}

async function handleSaveUser(): Promise<void> {
  if (userModalMode.value === 'create' && !canCreateUser.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (userModalMode.value === 'edit' && !canUpdateUser.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!validateUserForm()) {
    message.warning(t('users.validation.fixErrors'));
    return;
  }

  userModalSubmitting.value = true;
  try {
    if (userModalMode.value === 'create') {
      await createUser({
        username: formModel.username.trim(),
        password: formModel.password,
        role: formModel.role,
        isActive: formModel.isActive,
      });
      message.success(t('users.messages.createSuccess'));
    } else if (editingUser.value) {
      await updateUser(editingUser.value.id, {
        username: formModel.username.trim(),
        role: formModel.role,
        isActive: formModel.isActive,
      });
      message.success(t('users.messages.updateSuccess'));
    }

    userModalVisible.value = false;
    await loadUsers(false);
  } catch (error) {
    message.error(`${t('users.messages.saveFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    userModalSubmitting.value = false;
  }
}

async function handleToggleUserActive(target: UserListItemResponse): Promise<void> {
  if (!canDisableUser.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!canOperateTarget(target)) {
    message.warning(t('users.messages.operationForbidden'));
    return;
  }

  try {
    await updateUser(target.id, {
      isActive: !target.isActive,
    });
    message.success(t('users.messages.updateSuccess'));
    await loadUsers(false);
  } catch (error) {
    message.error(`${t('users.messages.updateFailed')}: ${extractApiErrorMessage(error)}`);
  }
}

function handleResetPassword(target: UserListItemResponse): void {
  if (!canUpdateUser.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!canOperateTarget(target)) {
    message.warning(t('users.messages.operationForbidden'));
    return;
  }

  const draft = ref('');

  dialog.warning({
    title: t('users.resetPasswordTitle', { username: target.username }),
    positiveText: t('users.resetPasswordAction'),
    negativeText: t('common.cancel'),
    content: () => h(
      NForm,
      { labelPlacement: 'top' },
      {
        default: () => [
          h(NFormItem, { label: t('users.newPassword') }, {
            default: () => h(NInput, {
              type: 'password',
              showPasswordOn: 'click',
              value: draft.value,
              placeholder: t('users.newPasswordPlaceholder'),
              'onUpdate:value': (value: string) => {
                draft.value = value;
              },
            }),
          }),
        ],
      },
    ),
    onPositiveClick: async () => {
      if (!draft.value) {
        message.warning(t('users.validation.passwordRequired'));
        return false;
      }

      try {
        await resetUserPassword(target.id, {
          newPassword: draft.value,
        });
        message.success(t('users.messages.resetPasswordSuccess'));
        return true;
      } catch (error) {
        message.error(`${t('users.messages.resetPasswordFailed')}: ${extractApiErrorMessage(error)}`);
        return false;
      }
    },
  });
}

function openPermissionsModal(target: UserListItemResponse): void {
  if (!canManagePermissions.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!canOperateTarget(target)) {
    message.warning(t('users.messages.operationForbidden'));
    return;
  }

  permissionsUser.value = target;
  permissionDraft.value = [...target.permissions];
  permissionsModalVisible.value = true;
}

async function savePermissions(): Promise<void> {
  if (!canManagePermissions.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!permissionsUser.value) {
    return;
  }

  permissionsModalSubmitting.value = true;
  try {
    await updateUserPermissions(permissionsUser.value.id, {
      permissions: [...permissionDraft.value],
    });
    message.success(t('users.messages.permissionsUpdated'));
    permissionsModalVisible.value = false;
    await loadUsers(false);
  } catch (error) {
    message.error(`${t('users.messages.permissionsUpdateFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    permissionsModalSubmitting.value = false;
  }
}

const columns = computed<DataTableColumns<UserListItemResponse>>(() => [
  {
    title: t('users.columns.username'),
    key: 'username',
    minWidth: 180,
  },
  {
    title: t('users.columns.role'),
    key: 'role',
    width: 120,
    render: (row) => {
      const normalizedRole = (() => {
        const raw = String(row.role ?? '').trim().toLowerCase();
        if (raw === 'superadmin' || raw === 'super_admin') {
          return 'superAdmin';
        }
        if (raw === 'admin') {
          return 'admin';
        }
        return 'user';
      })();
      const roleLabel = t(`auth.role.${normalizedRole}`);
      return h(
        NTag,
        { size: 'small', bordered: false, type: normalizedRole === 'user' ? 'default' : 'info' },
        { default: () => roleLabel },
      );
    },
  },
  {
    title: t('users.columns.status'),
    key: 'isActive',
    width: 120,
    render: (row) => h(
      NTag,
      { size: 'small', bordered: false, type: row.isActive ? 'success' : 'warning' },
      { default: () => (row.isActive ? t('common.enabled') : t('common.disabled')) },
    ),
  },
  {
    title: t('users.columns.createdAt'),
    key: 'createdAtUtc',
    width: 180,
    render: (row) => formatDateTime(row.createdAtUtc),
  },
  {
    title: t('users.columns.permissions'),
    key: 'permissions',
    minWidth: 220,
    render: (row) => t('users.permissionCount', { count: row.permissions.length }),
  },
  {
    title: t('users.columns.actions'),
    key: 'actions',
    minWidth: 280,
    render: (row) => {
      const editable = canOperateTarget(row);
      return h(
        NSpace,
        { size: 8 },
        {
          default: () => [
            canUpdateUser.value
              ? h(
                NButton,
                {
                  size: 'small',
                  quaternary: true,
                  disabled: !editable || userModalSubmitting.value,
                  onClick: () => openEditModal(row),
                },
                { default: () => t('users.editAction') },
              )
              : null,
            canManagePermissions.value
              ? h(
                NButton,
                {
                  size: 'small',
                  quaternary: true,
                  disabled: !editable || permissionsModalSubmitting.value,
                  onClick: () => openPermissionsModal(row),
                },
                { default: () => t('users.permissionsAction') },
              )
              : null,
            canUpdateUser.value
              ? h(
                NButton,
                {
                  size: 'small',
                  quaternary: true,
                  disabled: !editable,
                  onClick: () => handleResetPassword(row),
                },
                { default: () => t('users.resetPasswordAction') },
              )
              : null,
            canDisableUser.value
              ? h(
                NSwitch,
                {
                  value: row.isActive,
                  disabled: !editable,
                  'onUpdate:value': () => {
                    void handleToggleUserActive(row);
                  },
                },
              )
              : null,
          ],
        },
      );
    },
  },
]);

onMounted(() => {
  void loadUsers();
});
</script>

<template>
  <div>
    <page-header :title="t('users.title')" :description="t('users.description')">
      <template #actions>
        <n-button v-if="canCreateUser" type="primary" @click="openCreateModal">
          {{ t('users.createAction') }}
        </n-button>
      </template>
    </page-header>

    <n-alert v-if="isCurrentAdmin" type="info" style="margin-bottom: 12px">
      {{ t('users.adminScopeHint') }}
    </n-alert>

    <n-alert v-if="isUsersReadOnly" type="info" style="margin-bottom: 12px">
      {{ t('common.readOnlyNotice') }}
    </n-alert>

    <n-card class="section-card">
      <template #header-extra>
        <n-button text :loading="loading" @click="loadUsers()">
          {{ t('common.refresh') }}
        </n-button>
      </template>

      <n-alert v-if="hasLoadError && !showLoadErrorResult" type="warning" :show-icon="false" style="margin-bottom: 12px">
        {{ t('users.messages.loadFailed') }}: {{ loadErrorMessage }}
      </n-alert>

      <n-result
        v-if="showLoadErrorResult"
        status="error"
        :title="t('users.messages.loadFailed')"
        :description="loadErrorMessage"
      >
        <template #footer>
          <n-button type="primary" @click="loadUsers()">{{ t('common.retry') }}</n-button>
        </template>
      </n-result>

      <n-empty v-else-if="isEmpty" :description="t('users.empty')" />

      <div v-else class="table-wrapper">
        <n-data-table
          :loading="loading"
          :columns="columns"
          :data="users"
          :row-key="(row) => row.id"
          :scroll-x="1280"
        />
      </div>
    </n-card>

    <n-modal
      v-model:show="userModalVisible"
      preset="card"
      class="user-modal"
      :title="userModalTitle"
      :mask-closable="false"
      :style="{ width: 'min(620px, calc(100vw - 20px))' }"
    >
      <n-form label-placement="top">
        <n-form-item
          :label="t('users.username')"
          :validation-status="validationState.username ? 'error' : undefined"
          :feedback="validationState.username || undefined"
        >
          <n-input
            v-model:value="formModel.username"
            :disabled="userModalSubmitting"
            :placeholder="t('users.usernamePlaceholder')"
          />
        </n-form-item>

        <n-form-item
          v-if="userModalMode === 'create'"
          :label="t('users.password')"
          :validation-status="validationState.password ? 'error' : undefined"
          :feedback="validationState.password || undefined"
        >
          <n-input
            v-model:value="formModel.password"
            type="password"
            show-password-on="click"
            :disabled="userModalSubmitting"
            :placeholder="t('users.passwordPlaceholder')"
          />
        </n-form-item>

        <n-form-item
          :label="t('users.role')"
          :validation-status="validationState.role ? 'error' : undefined"
          :feedback="validationState.role || undefined"
        >
          <n-select
            v-model:value="formModel.role"
            :options="roleOptions"
            :disabled="userModalSubmitting"
          />
        </n-form-item>

        <n-form-item :label="t('users.status')">
          <n-switch v-model:value="formModel.isActive" :disabled="userModalSubmitting">
            <template #checked>{{ t('common.enabled') }}</template>
            <template #unchecked>{{ t('common.disabled') }}</template>
          </n-switch>
        </n-form-item>

        <n-space justify="end">
          <n-button :disabled="userModalSubmitting" @click="userModalVisible = false">
            {{ t('common.cancel') }}
          </n-button>
          <n-button type="primary" :loading="userModalSubmitting" @click="handleSaveUser">
            {{ t('common.save') }}
          </n-button>
        </n-space>
      </n-form>
    </n-modal>

    <n-modal
      v-model:show="permissionsModalVisible"
      preset="card"
      class="permissions-modal"
      :title="t('users.permissionsTitle', { username: permissionsUser?.username || '-' })"
      :mask-closable="false"
      :style="{ width: 'min(760px, calc(100vw - 20px))' }"
    >
      <n-space vertical :size="12">
        <n-checkbox-group v-model:value="permissionDraft">
          <div class="permissions-grid">
            <label v-for="option in permissionOptions" :key="option.value" class="permission-item">
              <n-checkbox :value="option.value">{{ option.label }}</n-checkbox>
            </label>
          </div>
        </n-checkbox-group>

        <n-space justify="end">
          <n-button :disabled="permissionsModalSubmitting" @click="permissionsModalVisible = false">
            {{ t('common.cancel') }}
          </n-button>
          <n-button type="primary" :loading="permissionsModalSubmitting" @click="savePermissions">
            {{ t('common.save') }}
          </n-button>
        </n-space>
      </n-space>
    </n-modal>
  </div>
</template>

<style scoped>
.section-card :deep(.n-card-header__main) {
  font-weight: 600;
}

.table-wrapper {
  overflow-x: auto;
}

.user-modal :deep(.n-card),
.permissions-modal :deep(.n-card) {
  border-radius: 14px;
}

.user-modal :deep(.n-card__content),
.permissions-modal :deep(.n-card__content) {
  max-height: min(74vh, 760px);
  overflow-y: auto;
}

.permissions-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 12px;
}

.permission-item {
  display: flex;
  align-items: center;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--app-border);
  border-radius: 12px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(248, 250, 252, 0.92));
  min-width: 0;
}

@media (max-width: 768px) {
  .user-modal :deep(.n-card__content),
  .permissions-modal :deep(.n-card__content) {
    max-height: 76vh;
  }

  .permissions-grid {
    grid-template-columns: 1fr;
  }
}
</style>
