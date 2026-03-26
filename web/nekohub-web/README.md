# NekoHub Web Admin

NekoHub 前端最小管理面板（Vue 3 + TypeScript + Vite）。

## 开发启动

```bash
npm install
npm run dev
```

默认地址：`http://localhost:5173`

## 构建

```bash
npm run build
```

## 运行时约定

- 默认 API Base URL 使用 `VITE_API_BASE_URL`（本地默认 `http://localhost:5121`）。
- 默认上传大小前置校验由 `VITE_MAX_UPLOAD_SIZE_BYTES` 控制。
