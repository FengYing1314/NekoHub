# NekoHub

NekoHub 是一个资产后端服务，当前稳定能力聚焦图片资产，提供上传、存储、访问、元数据维护、基础处理和 MCP 接口。

## 当前能力

- 资产接口：上传、详情、分页查询、元数据 PATCH、单删、批量删除、内容访问、使用统计
- 可见性：`isPublic`（上传与 PATCH 可设置）
- 存储：`local` / `s3-compatible`
- 持久化：SQLite / PostgreSQL（EF Core Migration）
- Provider Profile：创建、更新、删除、设默认、能力模型查询
- `github-repo` profile 显式管理 API：`browse`、`single-file upsert`（不接管全局 runtime）
- 资产与 profile 绑定：`StorageProviderProfileId` 已支持
- 后处理：`AssetDerivative`、`AssetStructuredResult`
- Skill Pipeline：同步顺序执行
- MCP：Tools / Resources / Prompts
- API Key 鉴权：覆盖 `/api/v1/assets` 与 `/mcp`

## 快速开始

### Docker Compose（推荐）

```bash
cp .env.example .env
# 修改 Auth__ApiKey__Keys__0
docker compose up -d
curl http://localhost:5121/api/v1/system/ping
```

前端：`http://localhost:5173`

默认 compose 使用 PostgreSQL 持久化；SQLite 仍可用于本地轻量开发（手动切换 `.env` 中数据库配置）。

首次启动全新数据库时，系统会按当前 `Storage:Provider` 自动 bootstrap 一个最小 default write profile（local/s3-compatible）。

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

## Provider 与 runtime 说明

- `default profile` 语义是默认写入目标
- 全局 runtime provider 当前仍以配置驱动为主
- bootstrap 仅初始化 DB 管理面的默认写入 profile，不等于 runtime 热切换
- 上传支持可选 `storageProviderProfileId`
- 读取优先按资产绑定 `StorageProviderProfileId`，旧资产再回退历史字段
- `GET /api/v1/system/storage/providers` 返回 `defaultProfile/runtime/alignment`，用于观察默认写入目标与 runtime 背景关系
- 前端统一通过 `/providers` 管理 provider profiles（新增/编辑/删除/设默认）

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
