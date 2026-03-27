# NekoHub 部署说明

## 1. 部署模式

- Local 模式
  - 存储：本地文件系统
  - 数据库：SQLite
- S3-compatible 模式
  - 存储：S3-compatible（例如 MinIO）
  - 数据库：SQLite 或 PostgreSQL

## 2. 前置要求

- .NET 10 SDK（源码部署）
- Node.js 22+（前端源码部署）
- Docker / Docker Compose（容器部署）

## 3. 关键环境变量

通用：

- `ASPNETCORE_URLS`
- `Persistence__Database__Provider`
- `Persistence__Database__ConnectionString`
- `Auth__ApiKey__Enabled`
- `Auth__ApiKey__Keys__0`
- `FRONTEND_PORT`
- `FRONTEND_VITE_API_BASE_URL`
- `FRONTEND_VITE_MAX_UPLOAD_SIZE_BYTES`

Local：

- `Storage__Provider=local`
- `Storage__Local__RootPath`
- `Storage__PublicBaseUrl`

PostgreSQL：

- `Persistence__Database__Provider=postgresql`
- `Persistence__Database__ConnectionString=Host=...;Port=5432;Database=...;Username=...;Password=...`

S3-compatible：

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

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

可选启动前端：

```bash
cd web/nekohub-web
npm install
npm run dev
```

验证：

```bash
curl http://localhost:5121/api/v1/system/ping
```

## 5. Docker（单容器）

构建：

```bash
docker build -t nekohub:latest .
```

Local 模式运行：

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

## 6. Docker Compose

Local 模式：

```bash
cp .env.example .env
# 修改 Auth__ApiKey__Keys__0
docker compose up -d
curl http://localhost:5121/api/v1/system/ping
```

S3 模式（MinIO 示例）：

```bash
docker compose --profile s3 up --build minio minio-init nekohub-s3
```

默认端口：

- 前端：`http://localhost:5173`
- NekoHub Local：`http://localhost:5121`
- NekoHub S3 示例：`http://localhost:5122`
- MinIO API：`http://localhost:9000`
- MinIO Console：`http://localhost:9001`

## 7. API Key 调用

- Header：`Authorization: Bearer <API_KEY>`
- 受保护入口：
  - `/api/v1/assets`
  - `/mcp`

HTTP 最小调用：

```bash
curl -H "Authorization: Bearer replace-with-strong-key" \
  "http://localhost:5121/api/v1/assets?page=1&pageSize=20"
```

MCP 最小调用：

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

## 8. 存储 profile 与 runtime 说明

- 上传支持可选 `storageProviderProfileId`
- 未指定时优先使用 DB 默认写入 profile，再回退配置默认 provider
- 读取优先按资产绑定 profile 解析，旧资产按历史字段回退
- `github-repo` 的 `browse/upsert` 属于显式管理 API，不切换全局 runtime provider

## 9. 生产环境最小建议

- 启用 API Key，并使用高强度密钥
- 敏感配置通过环境变量或密钥系统注入
- 在网关层控制 `/content` 暴露策略
- 统一 TLS 与访问日志
