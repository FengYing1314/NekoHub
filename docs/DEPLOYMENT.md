# NekoHub 部署说明

本文档目标：让开发者在最短路径内把 NekoHub 跑起来，并完成 API/MCP 的最小可用接入。

## 1. 部署模式概览

- **Local 模式（推荐首发体验）**
  - 存储：本地文件系统
  - 数据库：SQLite
  - 适合：本地开发、PoC、Agent 快速接入
- **S3-compatible 模式**
  - 存储：S3-compatible（如 MinIO）
  - 数据库：SQLite
  - 适合：需要对象存储语义的部署环境

## 2. 前置要求

- .NET 10 SDK（源码部署需要）
- Docker / Docker Compose（容器部署需要）

## 3. 环境变量说明

### 3.1 通用配置

- `ASPNETCORE_URLS`：服务监听地址，默认 `http://+:8080`
- `Persistence__Database__Provider`：当前使用 `sqlite`
- `Persistence__Database__ConnectionString`：SQLite 连接串
- `Auth__ApiKey__Enabled`：是否启用 API Key
- `Auth__ApiKey__Keys__0`：第一把 API Key（可继续 `__1`、`__2`）

### 3.2 Local 模式

- `Storage__Provider=local`
- `Storage__Local__RootPath`：本地存储目录
- `Storage__PublicBaseUrl`：对外访问 `/content` 的基础 URL

### 3.3 S3-compatible 模式

- `Storage__Provider=s3`
- `Storage__S3__ProviderName`
- `Storage__S3__Endpoint`
- `Storage__S3__Bucket`
- `Storage__S3__Region`
- `Storage__S3__AccessKey`
- `Storage__S3__SecretKey`
- `Storage__S3__ForcePathStyle`
- `Storage__S3__PublicBaseUrl`

## 4. 本地源码部署

1. 安装 .NET 10 SDK  
2. 还原与构建：

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
```

3. 启动 API：

```bash
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

4. 验证：

```bash
curl http://localhost:5121/api/v1/system/ping
```

> 提示：开发环境可关闭 API Key；上线环境建议始终开启。

## 5. Docker 部署（单容器）

### 5.1 构建镜像

```bash
docker build -t nekohub:latest .
```

### 5.2 Local 模式启动

```bash
docker run --rm -p 5121:8080 \
  -e Auth__ApiKey__Enabled=true \
  -e Auth__ApiKey__Keys__0=replace-with-strong-key \
  -e Storage__Provider=local \
  -e Storage__Local__RootPath=/app/storage/assets \
  -e Storage__PublicBaseUrl=http://localhost:5121/content \
  -e Persistence__Database__ConnectionString="Data Source=/app/storage/nekohub.db" \
  -v $(pwd)/data:/app/storage \
  nekohub:latest
```

## 6. Docker Compose 部署

### 6.1 最小可运行（Local 模式）

1. 复制环境模板：

```bash
cp .env.example .env
```

2. 修改 `.env` 中 `Auth__ApiKey__Keys__0`

3. 启动：

```bash
docker compose up --build nekohub
```

4. 验证：

```bash
curl http://localhost:5121/api/v1/system/ping
```

### 6.2 S3-compatible 模式（Compose + MinIO 示例）

```bash
docker compose --profile s3 up --build minio minio-init nekohub-s3
```

默认端口：

- NekoHub S3 模式示例：`http://localhost:5122`
- MinIO API：`http://localhost:9000`
- MinIO Console：`http://localhost:9001`

## 7. API Key 配置与调用

- Header：`Authorization: Bearer <API_KEY>`
- 受保护入口：
  - `/api/v1/assets`
  - `/mcp`

最小调用示例（HTTP）：

```bash
curl -H "Authorization: Bearer replace-with-strong-key" \
  "http://localhost:5121/api/v1/assets?page=1&pageSize=20"
```

最小调用示例（MCP）：

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

## 8. 首次部署常见踩坑

- **忘记改默认 API Key**：请务必替换示例值。
- **`Storage__PublicBaseUrl` 不匹配外部地址**：会导致返回的内容 URL 无法访问。
- **S3 模式 Bucket 未准备好**：Compose 示例中 `minio-init` 会自动建桶；自建环境需手动创建。
- **SQLite 文件权限问题**：容器挂载目录需可写。

## 9. 生产化最小建议

- 强制启用 API Key，使用高强度密钥并轮换
- 将敏感配置放入环境变量或密钥管理系统
- 对 `/content` 的公开访问策略做网关层控制
- 结合反向代理统一 TLS 与访问日志
