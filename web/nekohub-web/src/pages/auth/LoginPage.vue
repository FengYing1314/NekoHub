<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import {
  NButton,
  NCard,
  NForm,
  NFormItem,
  NInput,
  NSpace,
  NText,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '../../stores/auth.store';
import { useAppConfigStore } from '../../stores/app-config';
import { extractApiErrorMessage } from '../../api/client/error';

const { t } = useI18n();
const message = useMessage();
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const appConfigStore = useAppConfigStore();

const loading = ref(false);
const submitted = ref(false);
const loginError = ref('');
const formModel = reactive({
  username: '',
  password: '',
});
const usernameInvalid = computed(() => submitted.value && !formModel.username.trim());
const passwordInvalid = computed(() => submitted.value && !formModel.password);

function normalizeRedirectPath(): string {
  const rawRedirect = route.query.redirect;
  if (typeof rawRedirect !== 'string') {
    return '/assets';
  }

  if (!rawRedirect.startsWith('/')) {
    return '/assets';
  }

  return rawRedirect;
}

async function handleLogin(): Promise<void> {
  if (loading.value) {
    return;
  }
  submitted.value = true;
  loginError.value = '';
  if (usernameInvalid.value || passwordInvalid.value) {
    return;
  }

  loading.value = true;
  try {
    await authStore.login({
      username: formModel.username.trim(),
      password: formModel.password,
    });

    message.success(t('auth.login.success'));
    await router.push(normalizeRedirectPath());
  } catch (error) {
    loginError.value = `${t('auth.login.failed')}: ${extractApiErrorMessage(error)}`;
  } finally {
    loading.value = false;
  }
}

function handleOpenConfigModal(): void {
  appConfigStore.openConfigModal();
}
</script>

<template>
  <div class="login-page">
    <div class="login-bg-shape login-bg-shape--top" />
    <div class="login-bg-shape login-bg-shape--bottom" />
    <n-button class="login-config-button" tertiary @click="handleOpenConfigModal">
      {{ t('auth.login.configEntry') }}
    </n-button>

    <n-card class="login-card" :bordered="false">
      <n-space vertical :size="18">
        <div class="login-head">
          <h1 class="login-title">{{ t('auth.login.heading') }}</h1>
          <n-text depth="3">{{ t('auth.login.description') }}</n-text>
        </div>

        <n-form label-placement="top" @submit.prevent="handleLogin">
          <n-form-item
            :label="t('auth.login.username')"
            :validation-status="usernameInvalid ? 'error' : undefined"
            :feedback="usernameInvalid ? t('auth.login.validation.usernameRequired') : undefined"
          >
            <n-input
              v-model:value="formModel.username"
              autocomplete="username"
              :placeholder="t('auth.login.usernamePlaceholder')"
              :input-props="{ 'aria-label': t('auth.login.username'), 'aria-invalid': usernameInvalid }"
              @update:value="loginError = ''"
            />
          </n-form-item>
          <n-form-item
            :label="t('auth.login.password')"
            :validation-status="passwordInvalid ? 'error' : undefined"
            :feedback="passwordInvalid ? t('auth.login.validation.passwordRequired') : undefined"
          >
            <n-input
              v-model:value="formModel.password"
              type="password"
              show-password-on="click"
              autocomplete="current-password"
              :placeholder="t('auth.login.passwordPlaceholder')"
              :input-props="{ 'aria-label': t('auth.login.password'), 'aria-invalid': passwordInvalid }"
              @update:value="loginError = ''"
            />
          </n-form-item>

          <p v-if="loginError" class="login-error" role="alert">{{ loginError }}</p>
          <n-button type="primary" attr-type="submit" block :loading="loading">
            {{ t('auth.login.submit') }}
          </n-button>
        </n-form>
      </n-space>
    </n-card>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100dvh;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  padding: 80px var(--app-space-lg) var(--app-space-lg);
  overflow: hidden;
  background: radial-gradient(circle at 20% 20%, rgba(14, 165, 233, 0.08), transparent 48%),
    radial-gradient(circle at 80% 72%, rgba(16, 185, 129, 0.08), transparent 46%), var(--app-bg-mid);
}

.login-bg-shape {
  position: absolute;
  width: 240px;
  height: 240px;
  border-radius: 999px;
  filter: blur(2px);
  opacity: 0.35;
}

.login-bg-shape--top {
  top: -72px;
  right: 12%;
  background: rgba(59, 130, 246, 0.26);
}

.login-bg-shape--bottom {
  bottom: -80px;
  left: 8%;
  background: rgba(16, 185, 129, 0.26);
}

.login-config-button {
  position: absolute;
  top: 16px;
  right: 16px;
  z-index: 1;
  color: #0f172a;
  background: rgba(255, 255, 255, 0.72);
  backdrop-filter: blur(8px);
}

.login-card {
  width: min(420px, 100%);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius-panel);
  backdrop-filter: blur(8px);
  background: rgba(255, 255, 255, 0.92);
  box-shadow: var(--app-shadow);
}

.login-head {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.login-title {
  margin: 0;
  font-size: 28px;
  line-height: 1.2;
  color: var(--app-text-strong);
}

.login-error {
  margin: 0 0 var(--app-space-md);
  padding: 12px;
  border: 1px solid #fecaca;
  border-radius: var(--app-radius-control);
  background: #fef2f2;
  color: #991b1b;
  overflow-wrap: anywhere;
  font-size: 14px;
}

@media (max-width: 768px) {
  .login-title {
    font-size: 24px;
  }

  .login-page {
    padding: 72px 12px 24px;
  }
}
</style>
