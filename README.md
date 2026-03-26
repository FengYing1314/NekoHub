# NekoHub

NekoHub 是一个 **LLM-friendly image asset backend v1.0.0 baseline**。当前稳定版本聚焦图片资产，提供上传、存储、访问、元数据维护、基础处理与 MCP 接入能力。

长期来看，NekoHub 将继续演进为支持图片、视频、音频、Excel 等多类型文件的全媒体资产处理平台；但当前 `v1.0.0` 已完成并对外提供的能力，仍以图片资产后端基线为准。

## 项目定位

- 当前阶段：`v1.0.0 backend baseline`
- 核心场景：图片资产后端 + Agent/LLM 友好接入
- 设计重点：稳定契约、清晰边界、可演进架构（Clean Architecture）

## 当前 v1.0.0 后端已支持能力

- HTTP 资产主链路：上传、详情、分页、元数据 PATCH、单删、批量删除、内容访问、使用统计
- 基础可见性能力：`isPublic`（默认公开，可在上传与 PATCH 时设置）
- 资产列表查询：`query / contentType / status / orderBy / orderDirection`
- 前端最小管理面板（Vue 3 + TypeScript + Vite）：设置、资产列表、上传、详情
- Local 与 S3-compatible 双存储模式
- SQLite + EF Core Migration 持久化
- 图片基础元数据：`contentType / extension / size / width / height / checksumSha256`
- 后处理结果：
  - 文件型派生结果（`AssetDerivative`，如缩略图）
  - 结构化结果（`AssetStructuredResult`，如基础 caption）
- Skill Pipeline（同步顺序执行）
- Skill 执行记录持久化（`SkillExecution + StepResult + ParametersJson`）
- `run_asset_skill` 支持可选 `parameters` 输入并透传到 Skill 上下文
- MCP 接口面：
  - Tool Surface
  - Resource Surface
  - Prompt Surface
- API Key 认证边界（覆盖 `/api/v1/assets` 与 `/mcp`）

## 当前 v1.0.0 边界

- 不包含用户系统 / OAuth / RBAC / 多租户
- 不包含异步队列、分布式任务编排、重试死信体系
- 当前仅实现图片资产处理；视频、音频、Excel 等多类型文件仍属于后续演进方向

## 适合接入 Agent 的能力说明

NekoHub 当前可被 Agent 直接消费的能力：

- 可调用工具（MCP Tools）：读写资产、执行 Skill
- 可读取资源（MCP Resources）：资产详情、派生结果、结构化结果、Skill 描述
- 可获取提示模板（MCP Prompts）：资产检查、增强执行、产物审查
- 稳定的 API Key 鉴权边界

这使它适合作为 Agent 的“资产后端基础设施”，而不是完整自动化平台。

## Quick Start（推荐）

### 方式 A：Docker Compose（最短路径）

1. 复制环境变量模板：

```bash
cp .env.example .env
```

2. 修改 `.env` 中的 API Key（至少改 `Auth__ApiKey__Keys__0`）。

3. 直接拉取 GHCR 预构建镜像并启动 Local 模式：

```bash
docker compose up -d
```

4. 健康检查：

```bash
curl http://localhost:5121/api/v1/system/ping
```

5. 访问前端管理面板：

- `http://localhost:5173`
- 默认通过 `FRONTEND_VITE_API_BASE_URL` 直连后端（默认 `http://localhost:5121`）

### 方式 B：本地源码运行

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

默认地址示例：

- 后端：`http://localhost:5121`
- 前端：`http://localhost:5173`

## 最小调用示例

### HTTP 示例（上传图片）

```bash
curl -X POST "http://localhost:5121/api/v1/assets" \
  -H "Authorization: Bearer replace-with-strong-key" \
  -F "file=@./cat.png;type=image/png" \
  -F "description=cat avatar" \
  -F "altText=orange cat looking at camera" \
  -F "isPublic=true"
```

### MCP 示例（列出可用工具）

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

## API Key 使用说明

- 认证 Header：`Authorization: Bearer <API_KEY>`
- 受保护入口：
  - `/api/v1/assets`
  - `/mcp`
- 支持多 Key：
  - `Auth__ApiKey__Keys__0`
  - `Auth__ApiKey__Keys__1`
  - ...

生产环境建议开启 API Key 并使用高强度密钥；开发环境可按配置选择是否关闭。

## MCP 能力清单

### Tool Surface

- `get_asset`
- `list_assets`
- `patch_asset`
- `get_asset_content_url`
- `upload_asset`
- `delete_asset`
- `batch_delete_assets`
- `get_asset_usage_stats`
- `list_skills`
- `run_asset_skill`

### Resource Surface

- `asset://{id}`
- `asset://{id}/derivatives`
- `asset://{id}/structured-results`
- `skill://{name}`

### Prompt Surface

- `inspect_asset`
- `enrich_asset`
- `review_asset_outputs`

## 部署路径

- 本地源码部署
- Docker 单容器部署
- Docker Compose（默认一键启动前端 + 后端，前后端分离）

默认镜像：

- `ghcr.io/fengying1314/nekohub:latest`

详细步骤请看：[docs/DEPLOYMENT.md](./docs/DEPLOYMENT.md)。

## GitHub Pages（前端演示/备用入口）

- 前端 `web/nekohub-web` 可通过 GitHub Pages 作为静态演示站发布。
- Pages 仅托管前端静态文件，不承载后端 API。
- 首次打开演示站后，请在设置页手动填写：
  - API Base URL（你的后端公开地址）
  - API Key
- Pages 仅用于演示与备用入口，不替代当前后端 Docker/Compose 部署链路。

## 配置要点

- 默认数据库：SQLite
- 默认最简模式：Local 存储 + SQLite
- S3-compatible 模式：对象存储走 S3，数据库仍使用 SQLite
- 当前公开内容 URL 默认由 `Storage:PublicBaseUrl` + `/content/{storageKey}` 组成
- 资产默认 `isPublic = true`
- 前端通过 `VITE_API_BASE_URL` 显式访问后端 API（compose 里由 `FRONTEND_VITE_API_BASE_URL` 注入）
- 前端默认不内置固定 API 地址，公开演示场景下由设置页填写目标后端地址
- 后端 CORS 在当前 alpha 默认全开放（`AllowAnyOrigin/AllowAnyHeader/AllowAnyMethod`），优先降低自部署接入成本
- 当前安全边界依赖 API Key（`Authorization: Bearer <API_KEY>`），不是 Cookie/Session 浏览器会话认证
- 后续进入更严格部署场景时，可再切换为来源白名单模式
- 推荐用环境变量覆盖敏感配置，不要把真实密钥写入仓库
- 若需要统一域名 / HTTPS / 反向代理，请在仓库外层自行配置（Nginx/Caddy/Traefik 等）

参考模板：`.env.example`

## 已知限制（v1.0.0）

- `upload_asset` 当前使用 `contentBase64`（阶段性策略，不代表最终二进制传输方案）
- 当前仅提供基础 `public / private` 语义：
  - 公开资产可通过 `/content/...` 公开内容 URL 访问
  - 私有资产不会再返回公开内容 URL，`/api/v1/assets/{id}/content` 与 MCP `get_asset_content_url` 也会统一返回 `asset_not_found`
- 当前未实现复杂权限系统、签名 URL、私有预览代理、鉴权下载代理
- 默认数据库为 SQLite（首发阶段优先简单可部署）
- Skill 执行为进程内同步顺序执行（无异步队列/重试机制）
- 当前不提供 execution history 独立查询 API（仅提供 `latestExecutionSummary` 轻量摘要）

## Future Work（非当前已完成）

- execution history 查询与重跑能力
- 异步 worker / 更强可观测性
- 更多可插拔处理能力（OCR、tagging 等）
- 非图片资产类型支持（视频/音频/Excel 等）

## 技术栈

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + SQLite
- Local 文件存储 + S3-compatible 对象存储
- SixLabors.ImageSharp
- Docker / Docker Compose

## 许可证

本项目采用 [Apache License 2.0](./LICENSE)。
