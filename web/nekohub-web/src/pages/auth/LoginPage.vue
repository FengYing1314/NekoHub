<script setup lang="ts">
import { reactive, ref } from 'vue';
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
const formModel = reactive({
  username: '',
  password: '',
});

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
  if (!formModel.username.trim() || !formModel.password) {
    message.warning(t('auth.login.validation.required'));
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
    message.error(`${t('auth.login.failed')}: ${extractApiErrorMessage(error)}`);
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

        <n-form label-placement="top">
          <n-form-item :label="t('auth.login.username')">
            <n-input
              v-model:value="formModel.username"
              autocomplete="username"
              :placeholder="t('auth.login.usernamePlaceholder')"
              @keyup.enter="handleLogin"
            />
          </n-form-item>
          <n-form-item :label="t('auth.login.password')">
            <n-input
              v-model:value="formModel.password"
              type="password"
              show-password-on="click"
              autocomplete="current-password"
              :placeholder="t('auth.login.passwordPlaceholder')"
              @keyup.enter="handleLogin"
            />
          </n-form-item>

          <n-button type="primary" block :loading="loading" @click="handleLogin">
            {{ t('auth.login.submit') }}
          </n-button>
        </n-form>
      </n-space>
    </n-card>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  padding: 20px;
  overflow: hidden;
  background: radial-gradient(circle at 20% 20%, rgba(14, 165, 233, 0.16), transparent 48%),
    radial-gradient(circle at 80% 72%, rgba(16, 185, 129, 0.16), transparent 46%),
    linear-gradient(140deg, #f8fafc, #eef2ff 50%, #f0fdf4);
}

.login-bg-shape {
  position: absolute;
  width: 240px;
  height: 240px;
  border-radius: 999px;
  filter: blur(2px);
  opacity: 0.8;
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
  width: min(420px, calc(100vw - 24px));
  border-radius: 20px;
  backdrop-filter: blur(8px);
  background: rgba(255, 255, 255, 0.92);
  box-shadow: 0 18px 52px rgba(15, 23, 42, 0.14);
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
  color: #0f172a;
}

@media (max-width: 768px) {
  .login-title {
    font-size: 24px;
  }

  .login-page {
    padding: 12px;
  }
}
</style>
