# NekoHub

NekoHub 是一个面向图片资产管理的后端服务，提供上传、存储、访问、元数据维护、基础后处理，以及面向 Agent 的 MCP 接口；仓库同时包含一个 Vue 3 管理面板。

## 项目结构

- `NekoHub.slnx`：主解决方案，只包含活跃的 `src/` 项目
- `src/NekoHub.Api`：ASP.NET Core 入口、Controllers、MCP、鉴权、中间件、配置绑定
- `src/NekoHub.Application`：应用服务、Commands/Queries、DTO、抽象接口、配置校验
- `src/NekoHub.Domain`：领域模型与枚举，不依赖基础设施
- `src/NekoHub.Infrastructure`：EF Core 持久化、存储 provider、metadata 提取、skill pipeline
- `tests/NekoHub.Api.IntegrationTests`：基于 `WebApplicationFactory` 的后端集成测试
- `web/nekohub-web`：Vue 3 + TypeScript + Vite 管理面板

## 当前能力

- 资产接口：上传、详情、分页查询、元数据 PATCH、单删、批量删除、内容访问、使用统计
- 可见性：`isPublic`，上传与 PATCH 都支持
- 存储：`local` / `s3-compatible`
- Provider Profile：创建、更新、删除、设默认、能力模型查询
- `github-repo` profile 显式管理 API：`browse`、`single-file upsert`
- 资产与 profile 绑定：`StorageProviderProfileId`
- 后处理：`AssetDerivative`、`AssetStructuredResult`
- Skill Pipeline：同步顺序执行，当前内置 thumbnail 与 basic caption
- MCP：Tools / Resources / Prompts
- API Key 鉴权：覆盖 `/api/v1/assets`、`/api/v1/system/storage` 与 `/mcp`

## 架构与技术栈

- 后端：.NET 10、ASP.NET Core Controller API、OpenAPI
- 持久化：EF Core 10，支持 SQLite 与 PostgreSQL
- 对象存储：本地文件系统、S3-compatible、GitHub Repo experimental provider
- 图像处理：ImageSharp
- 前端：Vue 3、TypeScript、Vite、Pinia、Vue Router、Naive UI
- 测试：xUnit、FluentAssertions、ASP.NET Core `WebApplicationFactory`、Testcontainers

## 运行方式

### Docker Compose

推荐使用 compose 作为完整联调方式：

```bash
cp .env.example .env
# 修改 Auth__ApiKey__Keys__0
docker compose up -d
curl http://localhost:5121/api/v1/system/ping
```

- 后端：`http://localhost:5121`
- 前端：`http://localhost:5173`
- 默认持久化：PostgreSQL

### 本地源码

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

开发环境默认使用：

- `http://localhost:5121`
- SQLite 数据库：`src/NekoHub.Api/storage/nekohub.dev.db`
- API Key 关闭

生产或 compose 环境建议使用：

- PostgreSQL
- 显式启用 API Key
- 环境变量注入敏感配置

## 启动时行为

应用启动时会执行以下初始化逻辑：

- 按当前数据库 provider 执行 EF Core migrations
- SQLite 模式下自动创建数据库目录
- 全新数据库若没有 default write profile，会按当前 `Storage:Provider` 自动 bootstrap 一个最小 profile

这个 bootstrap 只初始化数据库中的“默认写入 profile”，不会把全局 runtime provider 改成热切换模式。

## Provider 与 Runtime 语义

- `default profile` 表示数据库层面的默认写入目标
- 全局 runtime provider 目前仍由配置驱动
- 上传支持可选 `storageProviderProfileId`
- 未指定 profile 时，优先使用数据库 default write profile，再回退到配置里的默认 provider
- 读取优先按资产绑定的 `StorageProviderProfileId` 解析，旧资产再回退历史 `StorageProvider`
- `GET /api/v1/system/storage/providers` 会返回 `defaultProfile`、`runtime`、`alignment`
- 前端统一通过 `/providers` 管理 provider profiles

## 最小调用示例

### HTTP 上传

```bash
curl -X POST "http://localhost:5121/api/v1/assets" \
  -H "Authorization: Bearer replace-with-strong-key" \
  -F "file=@./cat.png;type=image/png" \
  -F "description=cat avatar" \
  -F "altText=orange cat looking at camera" \
  -F "isPublic=true"
```

### MCP tools/list

```bash
curl -X POST "http://localhost:5121/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer replace-with-strong-key" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list"
  }'
```

## 开发与维护提示

- 活跃入口始终是 `src/NekoHub.Api`
- 运行与构建请使用 `NekoHub.slnx`
- 修改 `AssetDbContext` 或实体映射后，要及时生成并提交 migration
- S3 相关集成测试依赖本机 Docker / Testcontainers
- 运行时数据默认位于 `src/NekoHub.Api/storage/`

## 部署文档

- 详见 [docs/DEPLOYMENT.md](./docs/DEPLOYMENT.md)
- 环境变量模板见 `.env.example`

## 已知边界

- 复杂权限系统（OAuth/RBAC/多租户）未实现
- 异步队列、重试死信、分布式编排未实现
- 私有下载代理、签名 URL 未实现
- `github-repo` 为 experimental / platform-backed provider，不推荐 primary storage
- execution history 独立查询 API 未提供

## 许可证

[Apache License 2.0](./LICENSE)
