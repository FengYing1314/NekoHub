# 部署说明

本文档覆盖 NekoHub 首发版本的最小可运行部署方式：

- 本地源码部署
- Docker 部署
- Docker Compose 部署
- Local / S3-compatible 两种存储模式

## 1. 本地源码部署

### 1.1 前置要求

- .NET 10 SDK

### 1.2 构建与启动

```bash
dotnet restore NekoHub.slnx
dotnet build NekoHub.slnx
dotnet run --project src/NekoHub.Api/NekoHub.Api.csproj
```

## 2. Docker 部署

### 2.1 构建镜像

```bash
docker build -t nekohub:latest .
```

### 2.2 Local 模式运行

```bash
docker run --rm -p 5121:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e Storage__Provider=local \
  -e Storage__Local__RootPath=/app/storage/assets \
  -e Storage__PublicBaseUrl=http://localhost:5121/content \
  -e Persistence__Database__ConnectionString="Data Source=/app/storage/nekohub.db" \
  -v $(pwd)/data:/app/storage \
  nekohub:latest
```

## 3. Docker Compose 部署

### 3.1 Local 模式

```bash
docker compose up --build nekohub
```

### 3.2 S3-compatible 模式（示例：MinIO）

```bash
docker compose --profile s3 up --build minio minio-init nekohub-s3
```

## 4. 必要环境变量

### 4.1 通用

- `Persistence__Database__Provider`（默认 `sqlite`）
- `Persistence__Database__ConnectionString`

### 4.2 Local 存储

- `Storage__Provider=local`
- `Storage__Local__RootPath`
- `Storage__PublicBaseUrl`

### 4.3 S3-compatible 存储

- `Storage__Provider=s3`
- `Storage__S3__ProviderName`
- `Storage__S3__Endpoint`
- `Storage__S3__Bucket`
- `Storage__S3__Region`（可选）
- `Storage__S3__AccessKey`
- `Storage__S3__SecretKey`
- `Storage__S3__ForcePathStyle`
- `Storage__S3__PublicBaseUrl`

## 5. 最小可运行配置示例

请参考：

- `src/NekoHub.Api/appsettings.json`
- `.env.example`

建议生产环境通过环境变量覆盖敏感配置，不要将真实密钥写入仓库。
