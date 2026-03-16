<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';

const route = useRoute();
const { t } = useI18n();

const isGalleryRoute = computed(() => route.path.startsWith('/gallery'));
</script>

<template>
  <div class="public-shell">
    <header class="public-header">
      <div class="public-header__inner">
        <router-link to="/gallery" class="brand-link">
          <span class="brand-link__eyebrow">{{ t('gallery.layout.tagline') }}</span>
          <span class="brand-link__title">{{ t('gallery.layout.brand') }}</span>
        </router-link>

        <nav class="public-nav">
          <router-link
            to="/gallery"
            class="public-nav__link"
            :class="{ 'public-nav__link--active': isGalleryRoute }"
          >
            {{ t('gallery.layout.explore') }}
          </router-link>
          <router-link to="/assets" class="public-nav__link public-nav__link--ghost">
            {{ t('gallery.layout.admin') }}
          </router-link>
        </nav>
      </div>
    </header>

    <main class="public-main">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.public-shell {
  min-height: 100vh;
  background:
    radial-gradient(circle at top left, rgba(245, 156, 11, 0.18), transparent 26%),
    radial-gradient(circle at top right, rgba(14, 116, 144, 0.16), transparent 24%),
    linear-gradient(180deg, #fcfbf7 0%, #f4f2ea 45%, #f7f8fb 100%);
}

.public-header {
  position: sticky;
  top: 0;
  z-index: 10;
  backdrop-filter: blur(16px);
  background: rgba(252, 251, 247, 0.78);
  border-bottom: 1px solid rgba(217, 214, 204, 0.9);
}

.public-header__inner {
  width: min(1180px, calc(100vw - 32px));
  margin: 0 auto;
  min-height: 76px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.brand-link {
  display: inline-flex;
  flex-direction: column;
  gap: 2px;
  color: #111827;
}

.brand-link__eyebrow {
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: #9a3412;
}

.brand-link__title {
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 24px;
  font-weight: 700;
  letter-spacing: -0.04em;
}

.public-nav {
  display: flex;
  align-items: center;
  gap: 10px;
}

.public-nav__link {
  padding: 10px 14px;
  border-radius: 999px;
  font-size: 14px;
  font-weight: 600;
  color: #1f2937;
  transition: background-color 0.2s ease, color 0.2s ease, transform 0.2s ease;
}

.public-nav__link:hover {
  transform: translateY(-1px);
  background: rgba(217, 119, 6, 0.1);
}

.public-nav__link--active {
  color: #7c2d12;
  background: rgba(217, 119, 6, 0.14);
}

.public-nav__link--ghost {
  color: #475569;
  background: rgba(255, 255, 255, 0.52);
  border: 1px solid rgba(203, 213, 225, 0.9);
}

.public-main {
  width: min(1180px, calc(100vw - 32px));
  margin: 0 auto;
  padding: 30px 0 48px;
}

@media (max-width: 768px) {
  .public-header__inner {
    width: calc(100vw - 24px);
    min-height: 68px;
    padding: 8px 0;
    flex-direction: column;
    align-items: stretch;
  }

  .public-nav {
    width: 100%;
  }

  .public-nav__link {
    flex: 1 1 0;
    text-align: center;
  }

  .public-main {
    width: calc(100vw - 24px);
    padding-top: 24px;
    padding-bottom: 32px;
  }
}
</style>
