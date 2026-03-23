# NekoHub

NekoHub 是一个 **LLM-friendly asset backend alpha**。当前首发版本聚焦图片资产，提供上传、存储、访问、基础处理与 MCP 接入能力。

长期来看，NekoHub 将继续演进为支持图片、视频、音频、Excel 等多类型文件的全媒体资产处理平台；但当前 alpha 版本已完成并对外提供的能力，仍以图片资产为准。

## 项目定位

- 当前阶段：`alpha`
- 核心场景：图片资产后端 + Agent/LLM 友好接入
- 设计重点：稳定契约、清晰边界、可演进架构（Clean Architecture）

## 当前 alpha 已支持能力

- HTTP 资产主链路：上传、详情、分页、删除、内容访问
- Local 与 S3-compatible 双存储模式
- SQLite + EF Core Migration 持久化
- 图片基础元数据：`contentType / extension / size / width / height / checksumSha256`
- 后处理结果：
  - 文件型派生结果（`AssetDerivative`，如缩略图）
  - 结构化结果（`AssetStructuredResult`，如基础 caption）
- Skill Pipeline（同步顺序执行）
- Skill 执行记录持久化（`SkillExecution + StepResult`）
- MCP Alpha 接口面：
  - Tool Surface
  - Resource Surface
  - Prompt Surface
- API Key 认证边界（覆盖 `/api/v1/assets` 与 `/mcp`）

## 当前 alpha 边界

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

3. 启动 Local 模式：

```bash
docker compose up --build nekohub
```

4. 健康检查：

```bash
curl http://localhost:5121/api/v1/system/ping
```

### 方式 B：本地源码运行

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

默认地址示例：`http://localhost:5121`（以启动日志为准）。

## 最小调用示例

### HTTP 示例（上传图片）

```bash
curl -X POST "http://localhost:5121/api/v1/assets" \
  -H "Authorization: Bearer replace-with-strong-key" \
  -F "file=@./cat.png;type=image/png" \
  -F "description=cat avatar" \
  -F "altText=orange cat looking at camera"
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

## MCP Alpha 能力清单

### Tool Surface

- `get_asset`
- `list_assets`
- `get_asset_content_url`
- `upload_asset`
- `delete_asset`
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
- Docker Compose（Local / S3-compatible）

详细步骤请看：[docs/DEPLOYMENT.md](./docs/DEPLOYMENT.md)。

## 配置要点

- 默认数据库：SQLite
- 默认最简模式：Local 存储 + SQLite
- S3-compatible 模式：对象存储走 S3，数据库仍使用 SQLite
- 推荐用环境变量覆盖敏感配置，不要把真实密钥写入仓库

参考模板：`.env.example`

## 已知限制（alpha）

- `upload_asset` 当前使用 `contentBase64`（阶段性策略，不代表最终二进制传输方案）
- Local 模式通过 `/content` 暴露静态文件访问（公开 URL 语义）
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
