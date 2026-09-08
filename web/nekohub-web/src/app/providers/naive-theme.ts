import type { GlobalThemeOverrides } from 'naive-ui';

export const themeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor: '#2563eb',
    primaryColorHover: '#3b82f6',
    primaryColorPressed: '#1d4ed8',
    primaryColorSuppl: '#2563eb',
    borderRadius: '10px',
    borderRadiusSmall: '6px',
    textColorBase: '#1f2937',
    fontFamily: "'Noto Sans SC', 'PingFang SC', 'Microsoft YaHei', sans-serif",
  },
  Card: {
    borderRadius: '16px',
    titleFontWeight: '600',
    paddingMedium: '20px',
    paddingSmall: '16px',
  },
  Dialog: {
    borderRadius: '20px',
  },
  Button: {
    fontWeight: '500',
  },
};
