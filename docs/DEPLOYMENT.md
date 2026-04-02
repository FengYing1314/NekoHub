# NekoHub 部署说明

## 1. 活跃入口

- 后端入口：`src/NekoHub.Api`
- 前端入口：`web/nekohub-web`
- 解决方案入口：`NekoHub.slnx`

所有运行、构建、测试命令都应基于上述入口执行。

## 2. 部署模式

- Local 模式
  - 存储：本地文件系统
  - 数据库：推荐 PostgreSQL，SQLite 仅用于轻量开发或单机验证
- S3-compatible 模式
  - 存储：S3-compatible，例如 MinIO
  - 数据库：推荐 PostgreSQL

## 3. 前置要求

- .NET 10 SDK
- Node.js 22+
- Docker / Docker Compose

## 4. 启动时初始化逻辑

应用启动后会自动执行：

- EF Core migrations
- SQLite 数据目录创建
- default write profile bootstrap

其中 bootstrap 规则如下：

- 仅当数据库里不存在 default write profile 时触发
- 只会基于当前 `Storage:Provider` 初始化一个最小 profile
- 不会将全局 runtime provider 变成数据库热切换模式

## 5. 关键环境变量

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
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_PORT`

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

GitHub Repo experimental provider：

- `Storage__Provider=github-repo`
- `Storage__GitHubRepo__ProviderName`
- `Storage__GitHubRepo__Owner`
- `Storage__GitHubRepo__Repo`
- `Storage__GitHubRepo__Ref`
- `Storage__GitHubRepo__BasePath`
- `Storage__GitHubRepo__ApiBaseUrl`
- `Storage__GitHubRepo__RawBaseUrl`
- `Storage__GitHubRepo__VisibilityPolicy`
- `Storage__GitHubRepo__Token`

## 6. 本地源码部署

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

开发环境默认行为：

- 监听 `http://localhost:5121`
- API Key 默认关闭
- SQLite 默认位于 `src/NekoHub.Api/storage/nekohub.dev.db`

验证：

```bash
curl http://localhost:5121/api/v1/system/ping
```

## 7. Docker（单容器）

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
  -e Persistence__Database__Provider=sqlite \
  -e Persistence__Database__ConnectionString="Data Source=/app/storage/nekohub.db" \
  -v ${PWD}/data:/app/storage \
  nekohub:latest
```

说明：

- Dockerfile 已安装 PostgreSQL 运行时依赖 `libgssapi-krb5-2`
- 容器内持久化目录默认是 `/app/storage`

## 8. Docker Compose

默认 compose 为完整推荐部署：

- `nekohub`：后端
- `postgres`：PostgreSQL
- `nekohub-web`：管理面板

启动：

```bash
cp .env.example .env
# 修改 Auth__ApiKey__Keys__0
docker compose up -d
curl http://localhost:5121/api/v1/system/ping
```

可选 SQLite 模式：

- 将 `.env` 中 `Persistence__Database__Provider` 改为 `sqlite`
- 将 `Persistence__Database__ConnectionString` 改为 `Data Source=/app/storage/nekohub.db`

S3 / MinIO 示例：

```bash
docker compose --profile s3 up --build minio minio-init nekohub-s3
```

默认端口：

- 前端：`http://localhost:5173`
- 后端 Local：`http://localhost:5121`
- 后端 S3 示例：`http://localhost:5122`
- PostgreSQL：`localhost:5432`
- MinIO API：`http://localhost:9000`
- MinIO Console：`http://localhost:9001`

## 9. API Key 与受保护入口

- Header：`Authorization: Bearer <API_KEY>`
- 开发环境本地源码默认关闭 API Key
- Compose / 生产环境建议始终开启

受保护入口：

- `/api/v1/assets`
- `/api/v1/system/storage`
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

## 10. 存储 profile 与 runtime 说明

- 数据库里的 default write profile 是默认写入目标
- 全局 runtime provider 当前仍由配置驱动
- 上传可选 `storageProviderProfileId`
- 未指定时优先使用数据库 default write profile，再回退配置默认 provider
- 读取优先按资产绑定的 profile 解析，旧资产按历史字段回退
- `github-repo` 的 `browse` / `upsert` 只是显式管理 API，不接管全局 runtime provider
- Provider 管理入口在前端 `/providers`

## 11. 生产环境最小建议

- 启用 API Key，并使用高强度密钥
- 敏感配置通过环境变量或密钥系统注入
- 为 PostgreSQL 做备份与连接池配置
- 在网关层控制 `/content` 暴露策略
- 统一 TLS、访问日志和错误日志
- 修改 EF Core 模型后及时生成并发布 migration
