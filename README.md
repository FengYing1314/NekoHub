# NekoHub

NekoHub 是一个图片资产管理系统，仓库同时包含：

- 后端 API：上传、查询、存储、公开访问、技能处理、工作流、用户与权限管理、MCP
- 前端公开站：`/gallery`
- 前端管理台：`/login`、`/assets`、`/workflows`、`/providers`、`/ai-providers`、`/settings`、`/users`

当前项目已经不是“单一 API Key 的后台工具”，而是“公开站 + JWT 管理台 + API key 兼容机器调用”的完整形态。

## 当前产品模型

- 公开站 `/gallery`
  - 匿名访问
  - 只展示 `ready && isPublic` 的资产
  - 同一个部署里的所有访客看到的是同一份公开资产集合，不按访客身份区分
- 管理台
  - 浏览器端通过用户名密码登录
  - 使用 `Access Token + Refresh Token` 维持会话
  - 登录后按角色和权限显示页面与操作
- API key
  - 继续保留给 MCP、脚本、旧集成
  - 管理 REST 同时接受 `JWT` 或 `API key`
  - `/mcp` 仍是 `API key only`
- 首次部署
  - 通过环境变量种子创建唯一 `SuperAdmin`
  - 只在用户表为空时执行一次

补充一点：

- `GET /api/v1/system/bootstrap` 仍会返回 `apiKeyRequired`
- 这个字段反映的是后端当前是否启用了 API key 兼容能力
- 它不再是浏览器管理台的登录方式判断依据

## 前后端对齐状态

| 模块 | 前端入口 | 后端接口 | 当前状态 |
| --- | --- | --- | --- |
| 公开站 | `/gallery`、`/gallery/:id` | `GET /api/v1/public/assets`、`GET /api/v1/public/assets/{id}`、`GET /content/{storageKey}` | 已对齐 |
| 登录与会话 | `/login`、Pinia auth store、路由守卫 | `POST /api/v1/auth/login`、`POST /api/v1/auth/refresh`、`GET /api/v1/auth/me`、`POST /api/v1/auth/logout` | 已对齐 |
| 用户管理 | `/users` | `GET/POST/PATCH /api/v1/users`、`POST /api/v1/users/{id}/status`、`POST /api/v1/users/{id}/reset-password`、`PATCH /api/v1/users/{id}/permissions` | 已对齐 |
| 资产管理 | `/assets`、`/assets/upload`、`/assets/:id` | `/api/v1/assets`、`/api/v1/assets/{id}/content`、`GET /api/v1/assets/skills`、`POST /api/v1/assets/{id}/skills/{skillName}/run` | 已对齐 |
| 工作流 | `/workflows`、`/assets/:id` 中的 workflow 运行卡片 | `/api/v1/system/workflows`、`PATCH /api/v1/system/workflows/{id}/autorun`、`POST /api/v1/assets/{id}/workflows/{workflowId}/run` | 已对齐 |
| 存储 Provider | `/providers` | `/api/v1/system/storage/providers`、GitHub Repo browse/upsert 接口 | 已对齐 |
| AI Provider | `/ai-providers` | `/api/v1/system/ai/providers` | 已对齐 |
| 运行时配置 | `/settings`、`RequiredConfigModal` | `GET /api/v1/system/bootstrap` | 已对齐 |
| MCP / 机器调用 | 无浏览器管理 UI | `/mcp` | 按设计保留 API key only |

当前这套对齐关系的关键点是：

- 浏览器管理台不再依赖本地 API key 进入系统
- 浏览器里仍会保存 `apiBaseUrl`，因为前后端可能分域部署
- 浏览器里保存的是当前用户自己的 JWT 会话，不是“整站共享管理密码”
- 公开站与管理台共用一套后端和一份业务数据，但访问边界不同

## 项目结构

- `NekoHub.slnx`：主解决方案，只包含活跃的 `src/` 项目
- `src/NekoHub.Api`：ASP.NET Core 入口、Controllers、鉴权、中间件、配置绑定
- `src/NekoHub.Application`：应用服务、Commands/Queries、DTO、抽象接口
- `src/NekoHub.Domain`：领域模型与枚举
- `src/NekoHub.Infrastructure`：EF Core、存储 provider、密码哈希、初始化 bootstrap
- `tests/NekoHub.Api.IntegrationTests`：后端集成测试
- `web/nekohub-web`：Vue 3 + TypeScript + Vite 前端

## 快速启动

推荐直接使用仓库根目录的 `compose.yaml`：

```bash
cp .env.example .env
```

至少先改这几个值：

- `Auth__Jwt__Secret`
- `Auth__BootstrapSuperAdmin__Username`
- `Auth__BootstrapSuperAdmin__Password`
- `FRONTEND_VITE_API_BASE_URL`
- 若需保留机器访问，再配置 `Auth__ApiKey__Keys__0`

然后启动：

```bash
docker compose up -d --build
```

补充说明：

- 容器部署下，ASP.NET Core Data Protection keys 现在持久化在 `./data/keys`
- 不再使用单独的 `./keys` 挂载目录，避免容器内非 root 用户写入该目录时出现权限错误

默认地址：

- 前端：`http://localhost:5173`
- 后端：`http://localhost:5121`
- PostgreSQL：`localhost:5432`

启动后建议直接验证：

```bash
curl http://localhost:5121/api/v1/system/ping
curl http://localhost:5121/api/v1/system/bootstrap
```

浏览器入口：

- 公开站：`http://localhost:5173/gallery`
- 登录页：`http://localhost:5173/login`
- 管理台：登录后进入 `/assets` 等页面

更完整的部署说明见 [docs/DEPLOYMENT.md](./docs/DEPLOYMENT.md)。

## 本地开发

后端：

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

前端：

```bash
cd web/nekohub-web
npm ci
npm run dev
```

开发环境说明：

- `appsettings.Development.json` 默认关闭 API key
- 管理台依然走 JWT 登录
- 公开站始终可匿名访问
- `RequiredConfigModal` 只负责提示 `apiBaseUrl`，不是鉴权弹窗

## 核心配置

部署时最重要的环境变量：

- `Persistence__Database__ConnectionString`
- `Auth__Jwt__Secret`
- `Auth__BootstrapSuperAdmin__Username`
- `Auth__BootstrapSuperAdmin__Password`
- `Storage__Provider`
- `Storage__PublicBaseUrl`

可选但常用：

- `Auth__ApiKey__Enabled`
- `Auth__ApiKey__Keys__0`
- `FRONTEND_VITE_API_BASE_URL`

## 测试

```bash
dotnet build NekoHub.slnx
dotnet test tests/NekoHub.Api.IntegrationTests/NekoHub.Api.IntegrationTests.csproj

cd web/nekohub-web
npm run test -- --run
npm run build
```

测试前置条件：

- 后端集成测试默认通过 Testcontainers 启动 PostgreSQL，需要可用 Docker
- 如需跳过自动数据库容器，可显式设置 `NEKOHUB_TEST_DATABASE_CONNECTIONSTRING`
- S3 相关测试可通过 `NEKOHUB_RUN_S3_IT=true` 启用

## 后台任务与资产恢复

- 上传与后台处理任务在同一数据库提交中保存；工作流排队响应包含 `jobId`。
- 资产详情显示后台任务状态。失败任务可在核对当前图片后重试，重试会重新执行整个流程。
- 待执行任务会在重启后继续领取；执行中断的图片任务会标记失败，避免自动重复水印等非幂等处理。
- 文件删除记录先持久化，随后立即尝试清理；暂时失败会由后台自动重试。
- 格式转换保留的原图会登记为衍生物，可下载，并随资产删除。
- 原图变更会使旧缩略图和 caption 失效，已有缩略图会重新生成。
- 使用私有 S3 桶，公开文件统一通过 NekoHub `/content` 路由提供。具体配置与恢复边界见 [处理与恢复说明](./docs/PROCESSING.md)。
- 发布镜像和前端之前必须通过 `.github/workflows/verify.yml` 的后端测试、私有 S3 集成测试与前端测试/构建。

## 已知边界

- 公开站只服务公开资产，不做匿名私有预览
- 私有内容的浏览器访问走受保护的 `/api/v1/assets/{id}/content`
- 不做注册、忘记密码、邮箱验证、MFA、SSO
- 业务数据当前是共享的，不按用户做资源归属隔离
- `github-repo` / `github-releases` 更适合平台型或辅助型存储，不建议替代主对象存储

## 相关文档

- 部署说明：[docs/DEPLOYMENT.md](./docs/DEPLOYMENT.md)
- Mintlify 文档站源码：[docs/mintlify](./docs/mintlify)
- Workflow 使用指南：[docs/mintlify/guides/workflows.mdx](./docs/mintlify/guides/workflows.mdx)
- Workflow API 文档：[docs/mintlify/api/workflows.mdx](./docs/mintlify/api/workflows.mdx)
- 前端说明：[web/nekohub-web/README.md](./web/nekohub-web/README.md)

## 许可证

[Apache License 2.0](./LICENSE)
