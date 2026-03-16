# NekoHub

NekoHub 是一个基于 `.NET 10 + ASP.NET Core Web API` 的媒体资产后端服务，首发版本聚焦“图片上传、存储、访问与基础后处理”。

## 版本定位

- 当前版本：`v0.x` 首发版本（可运行、可部署）。
- 项目定位：资产平台基础层，不是最终形态。
- 当前重点：稳定 API、稳定持久化、Local/S3-compatible 双存储模式。

## 核心功能

- 图片上传、详情查询、分页查询、删除。
- 内容访问统一 `307` 重定向语义。
- Local 与 S3-compatible 存储支持。
- 基础元数据：`contentType / extension / size / width / height / checksumSha256`。
- 后处理骨架：
  - 文件型结果：缩略图（`thumbnail_256`）。
  - 结构化结果：基础描述（`basic_caption`）。
- 详情读模型返回 `derivatives` 与 `structuredResults` 摘要。

## 技术栈

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + SQLite
- Local 文件存储 + S3-compatible 对象存储（AWS S3 API）
- SixLabors.ImageSharp（图像处理）
- Docker / Docker Compose

## 目录结构

```text
src/
  NekoHub.Api            # HTTP 接口与应用入口
  NekoHub.Application    # 用例、服务、抽象接口
  NekoHub.Domain         # 领域模型
  NekoHub.Infrastructure # EF Core、存储实现、处理器实现
Dockerfile
compose.yaml
```

## 快速开始

### 前置要求

- .NET 10 SDK
- Docker（可选）

### 本地源码运行

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

默认地址（示例）：`http://localhost:5121`（按启动日志为准）。

## 运行模式

### Local 存储模式（默认）

- 存储：本地文件系统
- 数据库：SQLite（`storage/nekohub.db`）

示例环境变量：

```bash
export Storage__Provider=local
export Storage__Local__RootPath=storage/assets
export Storage__PublicBaseUrl=http://localhost:5121/content
export Persistence__Database__ConnectionString="Data Source=storage/nekohub.db"
```

### S3-compatible 存储模式

- 存储：S3-compatible（可对接 MinIO / AWS S3 / 兼容实现）
- 数据库：当前仍为 SQLite

示例环境变量：

```bash
export Storage__Provider=s3
export Storage__S3__ProviderName=s3
export Storage__S3__Endpoint=http://127.0.0.1:9000
export Storage__S3__Bucket=nekohub
export Storage__S3__Region=us-east-1
export Storage__S3__AccessKey=minioadmin
export Storage__S3__SecretKey=minioadmin
export Storage__S3__ForcePathStyle=true
export Storage__S3__PublicBaseUrl=http://127.0.0.1:9000/nekohub
export Persistence__Database__ConnectionString="Data Source=storage/nekohub.db"
```

## Docker 运行方式

```bash
docker build -t nekohub:latest .
docker run --rm -p 5121:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e Storage__Provider=local \
  -e Storage__Local__RootPath=/app/storage/assets \
  -e Storage__PublicBaseUrl=http://localhost:5121/content \
  -e Persistence__Database__ConnectionString="Data Source=/app/storage/nekohub.db" \
  -v $(pwd)/data:/app/storage \
  nekohub:latest
```

## Docker Compose 运行方式

### Local 模式

```bash
docker compose up --build nekohub
```

### S3-compatible 模式（示例：MinIO）

```bash
docker compose --profile s3 up --build minio minio-init nekohub-s3
```

## 环境变量配置方式

- ASP.NET Core 支持 `Section__SubSection__Key` 形式覆盖 `appsettings.json`。
- 推荐通过环境变量或密钥管理系统注入敏感配置，不要把真实密钥提交到仓库。

## 数据库说明（当前 SQLite）

- 当前仅使用 SQLite。
- 启动时自动执行 EF Core 迁移。
- 默认连接串位于 `src/NekoHub.Api/appsettings.json`，建议生产环境通过环境变量覆盖。

## 已知限制

- 当前无鉴权与多租户隔离能力。
- 结构化结果为最小样板，当前仅内置 `basic_caption` 的 deterministic 产出。
- 当前不包含复杂任务调度能力（如队列重试、分布式处理编排）。

## 后续路线图（简版）

- 强化处理任务生命周期管理（重试、失败状态、可观测性）。
- 扩展更多可插拔处理能力（OCR、tagging、审核评分等）。
- 增强部署形态与运行时治理能力。

## 许可证

本项目采用 `Apache License 2.0`，详见 [LICENSE](./LICENSE)。

## 部署文档

详细部署说明见 [docs/DEPLOYMENT.md](./docs/DEPLOYMENT.md)。
