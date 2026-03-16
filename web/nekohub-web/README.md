# NekoHub Web

NekoHub 前端基于 Vue 3 + TypeScript + Vite + Pinia + Vue Router + Naive UI，当前包含两套入口：

- 公开站
  - `/gallery`
  - `/gallery/:id`
- 管理台
  - `/login`
  - `/assets`
  - `/assets/upload`
  - `/assets/:id`
  - `/providers`
  - `/ai-providers`
  - `/settings`
  - `/users`

## 当前前端模型

- 公开站匿名访问，只消费公开接口
- 管理台使用 JWT 登录，不再依赖浏览器本地 API key
- API key 仅保留给 MCP / 机器调用，不是浏览器管理台的登录凭证
- `RequiredConfigModal` 只负责提示 `apiBaseUrl`，不是鉴权入口
- `/api/v1/system/bootstrap` 里的 `apiKeyRequired` 只是后端兼容状态信息，不是浏览器登录方式

前端与后端的主要接口对齐如下：

- 公开站
  - `/api/v1/public/assets`
  - `/api/v1/public/assets/{id}`
- 登录与会话
  - `/api/v1/auth/login`
  - `/api/v1/auth/refresh`
  - `/api/v1/auth/me`
  - `/api/v1/auth/logout`
- 管理台
  - `/api/v1/assets`
  - `/api/v1/system/storage`
  - `/api/v1/system/ai/providers`
  - `/api/v1/users`
  - `/api/v1/system/bootstrap`

## 开发启动

```bash
npm install
npm run dev
```

默认地址：

- Vite dev server：`http://localhost:5173`

## 构建与预览

```bash
npm run build
npm run preview
```

默认地址：

- Vite preview：`http://localhost:4173`

当前 Docker 镜像运行的是 `vite preview`。

## 运行时配置

### `VITE_API_BASE_URL`

- 前端请求后端的基础地址
- 为空时走同源
- 分域部署时应显式填写 API 地址

### `VITE_MAX_UPLOAD_SIZE_BYTES`

- 上传前端校验上限
- 默认 `10485760`

### `VITE_BASE_PATH`

- 前端部署子路径
- 未设置时默认 `/`

### `VITE_ALLOWED_HOSTS`

- `vite preview` 允许访问的 Host
- 可填 `true`、`*`，或逗号分隔的域名列表
- 通过域名访问 preview 时，这个值必须与部署域名匹配，或者直接设为 `true`

## 会话与本地存储

浏览器本地目前只保存两类数据：

- `apiBaseUrl`
- 当前用户自己的 `accessToken` / `refreshToken`

这意味着：

- 前端现在已经是“按用户登录”
- 不是“所有浏览器共用一个 API key”
- `apiBaseUrl` 仍然是按浏览器保存，因为部署时可能前后端分域

## 路由与权限

- 路由守卫会拦截管理台页面，未登录时跳转 `/login`
- 页面和侧边栏导航按权限显隐
- 登录后会根据权限选择默认落点，例如 `/assets`、`/providers`、`/users`

## 认证行为

- 请求拦截器自动附加 `Authorization: Bearer <accessToken>`
- 响应拦截器在遇到 `401` 时会自动串行刷新 refresh token，并重试挂起请求
- refresh 失败后清空本地会话并跳回登录页

## 和公开站相关的边界

- 公开站只展示公开资产
- 不做匿名私有资产预览
- 不需要浏览器里预先配置 API key

## GitHub Actions / 子路径部署

- 配置了 `VITE_BASE_PATH` 时，构建产物会使用该 base path
- 在 GitHub Actions 环境下，如果仓库名不是 `*.github.io`，会自动推导为 `/<repo-name>/`
