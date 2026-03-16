# NekoHub 部署说明

这份文档描述当前仓库已经实现并验证通过的部署方式，重点说明公开站、鉴权模型、部署级配置与用户级数据的边界。

## 1. 先理解现在的系统模型

NekoHub 现在有两套前端入口，共用一套后端：

- 公开站：`/gallery`
  - 匿名访问
  - 只显示公开资产
  - 同一个部署里的所有访客看到的是同一份公开内容，不按人区分
- 管理台：`/login`、`/assets`、`/workflows`、`/providers`、`/ai-providers`、`/settings`、`/users`
  - 需要用户登录
  - 浏览器端使用 `JWT Access Token + Refresh Token`
  - 页面和按钮按权限显隐

鉴权边界如下：

- 公开接口
  - `GET /api/v1/system/bootstrap`
  - `GET /api/v1/public/assets`
  - `GET /api/v1/public/assets/{id}`
  - `GET /content/{storageKey}`
- 用户登录接口
  - `POST /api/v1/auth/login`
  - `POST /api/v1/auth/refresh`
  - `POST /api/v1/auth/logout`
  - `GET /api/v1/auth/me`
- 管理接口
  - `/api/v1/assets`
  - `/api/v1/system/storage`
  - `/api/v1/system/ai/providers`
  - `/api/v1/users`
  - 这些接口接受 `JWT` 或 `API key`
- 机器接口
  - `/mcp`
  - 仍然是 `API key only`

补充说明：

- `GET /api/v1/system/bootstrap` 返回的 `apiKeyRequired` 字段仍然存在
- 这个字段表示后端是否启用了 API key 兼容能力
- 它不代表浏览器管理台现在还靠 API key 登录

当前系统的关键结论如下：

- “为什么会有公开站”
  - 因为产品本身分成“公开展示”和“登录后管理”两部分
- “是不是每个人看到都不一样”
  - 公开站不是按用户区分，而是按资产 `isPublic` 区分
- “现在前后端是不是只靠 API key 鉴权”
  - 不是
  - 浏览器管理台已经改成 JWT 用户登录
  - API key 只保留给 MCP、脚本和兼容旧集成

## 2. 哪些配置是一次性的，哪些是按用户的

这部分最容易混淆，直接分开看：

### 部署级配置

这些是在部署这套服务时配置一次的：

- PostgreSQL 连接串
- JWT Secret / Issuer / Audience
- 存储配置
- `Storage__PublicBaseUrl`
- 首次种子 `SuperAdmin` 用户名和密码
- 可选的 API key

### 用户级数据

这些是数据库里的业务数据，不是部署时每个用户都要配环境变量：

- 用户名
- 密码哈希
- 角色
- 权限授予
- refresh token

### 浏览器本地数据

浏览器里现在只保存：

- `apiBaseUrl`
- 当前登录用户的 access token / refresh token

也就是说：

- “创建第一个管理员”是部署级动作
- “每个用户登录自己的账号”是数据库用户动作
- “浏览器里存的配置”只是当前浏览器如何找到 API，不再是整套系统的鉴权方式

## 3. 最简单的部署方式

推荐直接用仓库根目录的 `compose.yaml`。

先复制环境变量模板：

```bash
cp .env.example .env
```

最少先改这几项：

```env
Auth__Jwt__Secret=请替换成至少32字符的强随机字符串
Auth__BootstrapSuperAdmin__Username=admin
Auth__BootstrapSuperAdmin__Password=请替换成强密码
FRONTEND_VITE_API_BASE_URL=http://localhost:5121
```

若需保留 MCP 或脚本调用，再额外配置：

```env
Auth__ApiKey__Enabled=true
Auth__ApiKey__Keys__0=请替换成强随机 API key
```

然后启动：

```bash
docker compose up -d --build
```

补充说明：

- 当前 `compose.yaml` 会把运行时数据与 ASP.NET Core Data Protection keys 一起持久化到 `./data`
- 其中 Data Protection keys 实际路径为 `./data/keys`
- 不再单独挂载 `./keys`，这样可以避免容器内应用用户在部分宿主环境下写 key ring 时遇到权限问题

默认端口：

- 前端：`http://localhost:5173`
- 后端：`http://localhost:5121`
- PostgreSQL：`localhost:5432`

## 4. 启动后怎么验证

先验证后端活着：

```bash
curl http://localhost:5121/api/v1/system/ping
curl http://localhost:5121/api/v1/system/bootstrap
```

再验证公开站接口：

```bash
curl "http://localhost:5121/api/v1/public/assets?page=1&pageSize=20"
```

再验证登录：

```bash
curl -X POST "http://localhost:5121/api/v1/auth/login" ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"admin\",\"password\":\"<superadmin-password>\"}"
```

浏览器入口：

- `http://localhost:5173/gallery`：公开站
- `http://localhost:5173/login`：登录页
- 登录后进入 `/assets`、`/workflows`、`/providers`、`/users` 等管理页面

## 5. 最重要的环境变量

| 变量 | 是否必须 | 作用 |
| --- | --- | --- |
| `Persistence__Database__ConnectionString` | 是 | PostgreSQL 连接串 |
| `Auth__Jwt__Secret` | 是 | 浏览器管理台 JWT 签名密钥 |
| `Auth__Jwt__Issuer` | 否 | JWT Issuer，默认 `NekoHub` |
| `Auth__Jwt__Audience` | 否 | JWT Audience，默认 `NekoHub.Admin` |
| `Auth__Jwt__AccessTokenMinutes` | 否 | access token 过期分钟数，默认 `15` |
| `Auth__Jwt__RefreshTokenDays` | 否 | refresh token 过期天数，默认 `30` |
| `Auth__BootstrapSuperAdmin__Username` | 首次部署必须 | 数据库中不存在 `SuperAdmin` 时创建初始管理员 |
| `Auth__BootstrapSuperAdmin__Password` | 首次部署必须 | 初始管理员密码 |
| `Auth__ApiKey__Enabled` | 否 | 是否启用 API key |
| `Auth__ApiKey__Keys__0` | 启用 API key 时建议配置 | MCP 和机器调用使用 |
| `Storage__Provider` | 是 | 当前运行时存储类型，例如 `local`、`s3`、`github-repo` |
| `Storage__PublicBaseUrl` | 是 | 公开内容地址前缀，公开站和公开内容跳转会用到 |
| `FRONTEND_VITE_API_BASE_URL` | 分域部署时必须 | 前端请求后端 API 的基础地址 |
| `FRONTEND_VITE_ALLOWED_HOSTS` | 域名访问 `vite preview` 时建议配置 | 允许访问前端 preview 的 Host |

说明：

- `compose.yaml` 已经通过 `.env` 把这些变量传进容器
- `Auth__BootstrapSuperAdmin__*` 不是“每个用户都要配一次”，而是“第一次把系统部署起来时需要配一次”
- 如果数据库里已经有 `SuperAdmin`，后续重启不会重复创建

## 6. 三种常见部署拓扑

### 方案 A：本机默认端口

```env
FRONTEND_VITE_API_BASE_URL=http://localhost:5121
Storage__PublicBaseUrl=http://localhost:5121/content
```

适合：

- 本地联调
- 单机部署
- 内网快速试跑

### 方案 B：前后端分域

前端：`https://nekohub.example.com`  
后端：`https://api.nekohub.example.com`

```env
FRONTEND_VITE_API_BASE_URL=https://api.nekohub.example.com
Storage__PublicBaseUrl=https://api.nekohub.example.com/content
FRONTEND_VITE_ALLOWED_HOSTS=nekohub.example.com
```

适合：

- 前后端独立域名
- 前端静态站点和 API 分开托管

### 方案 C：同域反向代理

如果使用 Nginx / Caddy 将前端和后端挂到同一个域名：

- 前端走 `/`
- 后端走 `/api` 和 `/content`

可以把：

```env
FRONTEND_VITE_API_BASE_URL=
Storage__PublicBaseUrl=https://nekohub.example.com/content
```

前端会走同源请求。

## 7. 为什么会出现 “此主机不被允许”

常见报错示例：

```text
请求被阻止。此主机 (“nekohub.fengying.xin”) 不被允许。
```

这不是后端鉴权报错，而是前端容器里 `vite preview` 的 Host 检查。

当前仓库已经支持通过 `FRONTEND_VITE_ALLOWED_HOSTS` 控制：

- 最省事：

```env
FRONTEND_VITE_ALLOWED_HOSTS=true
```

- 更严格：

```env
FRONTEND_VITE_ALLOWED_HOSTS=nekohub.fengying.xin
```

改完后重建前端容器：

```bash
docker compose up -d --build nekohub-web
```

## 8. 本地源码运行

后端：

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

前端：

```bash
cd web/nekohub-web
npm install
npm run dev
```

开发环境要点：

- `appsettings.Development.json` 默认关闭 API key
- 管理台仍然是 JWT 登录
- `RequiredConfigModal` 只在管理路由下提示 `apiBaseUrl`
- 公开站 `/gallery` 不会被这个弹窗阻塞

## 9. 生产环境最低建议

- 使用 PostgreSQL 持久化
- 配置强随机 `Auth__Jwt__Secret`
- 首次部署设置明确的 `SuperAdmin` 账号密码
- 如果还需要脚本 / MCP，单独设置强随机 `Auth__ApiKey__Keys__0`
- 对前端、API、`/content` 全部接上 HTTPS
- 分域部署时同时更新 `FRONTEND_VITE_API_BASE_URL` 和 `Storage__PublicBaseUrl`
- 如果是域名访问前端 preview，确认 `FRONTEND_VITE_ALLOWED_HOSTS`
- 如果要启用自动运行 workflow，先完成 `/workflows` 配置；若 workflow 包含 `ai-caption`，还需要提前配置 active AI provider

## 10. 关键结论

- 公开站存在是产品设计的一部分，不是部署异常
- 公开站不是“每个人都不一样”，而是“所有人共享公开资产视图”
- 浏览器管理台已经不是 API key 登录，而是 JWT 用户登录
- API key 现在主要是给 MCP 和机器调用保留的
- 部署只需要先配置一次数据库、JWT、初始管理员和存储
- 每个用户之后都是在数据库里管理，不是每个用户都配一遍环境变量

## 11. 相关文档

- Mintlify 文档站源码：`docs/mintlify/`
- Workflow 使用指南：`docs/mintlify/guides/workflows.mdx`
- Workflow API 文档：`docs/mintlify/api/workflows.mdx`
- AI Provider 文档：`docs/mintlify/guides/ai-skills.mdx`
