# NekoHub 仓库状态快照

> 快照日期：2026-04-17
>
> 这份文档只记录当前仓库已经存在的实现、测试和文档事实，供后续 LLM / agent 开发前快速对齐上下文。不要把它当成 roadmap；新增结论必须以代码、测试或现有文档为依据更新。

## 1. 当前项目范围

当前仓库已经是“公开画廊 + JWT 管理后台 + API key / MCP 机器访问”的完整形态，而不是单一图片上传 API：

- 后端主入口是 `src/NekoHub.Api/Program.cs`
- 主解决方案 `NekoHub.slnx` 只包含 `src/` 下的后端项目
- 前端管理台与公开画廊位于 `web/nekohub-web`
- Mintlify 文档站位于 `docs/mintlify`
- 测试项目单独位于 `tests/NekoHub.Api.IntegrationTests`

## 2. 已实现的后端能力

### 2.1 认证与访问边界

- JWT 登录、刷新、登出、当前用户接口已经存在：`src/NekoHub.Api/Controllers/AuthController.cs`
- 公开系统元数据与健康检查接口已经存在：`src/NekoHub.Api/Controllers/SystemController.cs`
- 管理接口统一走 `ManagementAccess`，MCP 走 `ApiKeyOnly`，JWT-only 用户接口走 `JwtUserRequired`：`src/NekoHub.Api/Auth/AuthorizationPolicies.cs`
- `HybridBearer` 会在 API key 启用时按 Bearer 形态分流 JWT / API key：`src/NekoHub.Api/Extensions/ServiceCollectionExtensions.cs`

### 2.2 资产生命周期与公开访问

- 资产上传、分页、详情、PATCH、删除、批量删除、使用统计、内容访问、技能运行等管理接口已经存在：`src/NekoHub.Api/Controllers/AssetsController.cs`
- 公开资产列表与详情接口已经存在：`src/NekoHub.Api/Controllers/PublicAssetsController.cs`
- `/content/{storageKey}` 的匿名公开内容映射已经存在，并且真正的公开性校验收敛在 `AssetContentService`：
  - `src/NekoHub.Api/Extensions/WebApplicationExtensions.cs`
  - `src/NekoHub.Application/Assets/Services/AssetContentService.cs`
- 资产领域模型已经包含公开性、描述、替代文本、存储 provider/profile、删除态等信息：`src/NekoHub.Domain/Assets/Asset.cs`

### 2.3 用户与权限管理

- 用户列表、详情、创建、更新、启停、重置密码、权限变更接口已经存在：`src/NekoHub.Api/Controllers/UsersController.cs`
- 权限键已经成体系落地：
  - `src/NekoHub.Application/Auth/Permissions/PermissionKeys.cs`
  - `src/NekoHub.Application/Auth/Permissions/PermissionCatalog.cs`
- 用户领域模型已经包含 refresh token 与权限授予：
  - `src/NekoHub.Domain/Users/User.cs`
  - `src/NekoHub.Domain/Users/RefreshToken.cs`
  - `src/NekoHub.Domain/Users/UserPermissionGrant.cs`

### 2.4 存储 Provider、AI Provider、工作流与 MCP

- 存储 provider profile 管理与 GitHub Repo browse/upsert 接口已经存在：`src/NekoHub.Api/Controllers/SystemStorageController.cs`
- AI provider profile 管理与测试接口已经存在：`src/NekoHub.Api/Controllers/AiProviderProfilesController.cs`
- 工作流 profile CRUD 与 autorun 接口已经存在：`src/NekoHub.Api/Controllers/WorkflowsController.cs`
- 资产工作流执行排队逻辑已经存在：`src/NekoHub.Application/Workflows/Services/AssetWorkflowExecutionService.cs`
- MCP 控制器、JSON-RPC server、prompt/resource/tool registry 已存在：
  - `src/NekoHub.Api/Controllers/McpController.cs`
  - `src/NekoHub.Api/Mcp/McpServer.cs`
  - `src/NekoHub.Api/Extensions/ServiceCollectionExtensions.cs`

### 2.5 基础设施与处理流水线

- 应用启动时会先做持久化初始化，再开放路由：`src/NekoHub.Api/Extensions/WebApplicationExtensions.cs`
- 后端已经有队列式资产处理和多个 post-processors：`src/NekoHub.Infrastructure/Processing/*.cs`
- 当前处理链至少包含缩略图、格式转换、水印、EXIF strip、基础 caption 结构化结果等：
  - `src/NekoHub.Infrastructure/Processing/ThumbnailAssetPostProcessor.cs`
  - `src/NekoHub.Infrastructure/Processing/FormatConvertAssetPostProcessor.cs`
  - `src/NekoHub.Infrastructure/Processing/WatermarkAssetPostProcessor.cs`
  - `src/NekoHub.Infrastructure/Processing/ExifStripAssetPostProcessor.cs`
  - `src/NekoHub.Infrastructure/Processing/BasicCaptionStructuredResultPostProcessor.cs`

## 3. 已实现的前端能力

- 路由已经同时覆盖公开画廊、登录页、资产、providers、AI providers、settings、users、workflows：`web/nekohub-web/src/app/router/index.ts`
- 前端认证模型已经是 JWT 会话，不再是浏览器共享 API key：`web/nekohub-web/src/stores/auth.store.ts`
- 公开画廊页面已实现：
  - `web/nekohub-web/src/pages/gallery/GalleryListPage.vue`
  - `web/nekohub-web/src/pages/gallery/GalleryDetailPage.vue`
- 管理台页面已实现：
  - `web/nekohub-web/src/pages/auth/LoginPage.vue`
  - `web/nekohub-web/src/pages/assets/*.vue`
  - `web/nekohub-web/src/pages/providers/ProvidersPage.vue`
  - `web/nekohub-web/src/pages/ai/AiProvidersPage.vue`
  - `web/nekohub-web/src/pages/settings/SettingsPage.vue`
  - `web/nekohub-web/src/pages/users/UsersPage.vue`
  - `web/nekohub-web/src/pages/workflows/WorkflowEditorPage.vue`
- 前端已经具备基于权限的默认落点和路由守卫：`web/nekohub-web/src/app/router/index.ts`

## 4. 测试现状

### 4.1 后端测试

`tests/NekoHub.Api.IntegrationTests` 已经不是空壳，覆盖面较广：

- 端点测试目录：`tests/NekoHub.Api.IntegrationTests/Endpoints`
  - 资产生命周期、查询、PATCH、可见性、批量删除、工作流执行、认证、公开资产、存储 provider、AI provider、MCP 等
- 基础设施/处理测试目录：`tests/NekoHub.Api.IntegrationTests/Infrastructure`
  - 存储契约、GitHub Repo 存储、OpenAI vision client、skill pipeline、具体图片技能等
- 测试宿主与容器环境：`tests/NekoHub.Api.IntegrationTests/Setup/NekoHubApplicationFactory.cs`
- 额外单元测试：`tests/NekoHub.Api.IntegrationTests/Unit/*.cs`

### 4.2 前端测试

- 前端已经使用 Vitest：`web/nekohub-web/package.json`, `web/nekohub-web/vitest.config.ts`
- 已存在的测试覆盖 router、auth store、http client、资产页面、登录页、配置弹窗、AI provider API 等：`web/nekohub-web/src/**/*.test.ts`

## 5. 文档现状

- 根 README 已经说明当前产品模型、部署、前后端对齐关系：`README.md`
- 部署说明已较完整：`docs/DEPLOYMENT.md`
- Mintlify 已覆盖 introduction、quickstart、deployment、assets、auth、users、storage providers、AI providers、MCP 等：
  - `docs/mintlify/docs.json`
  - `docs/mintlify/**/*.mdx`
- 但目前 Mintlify 导航中没有 workflow 相关页面，且仓库内未看到对应 workflow 文档页：`docs/mintlify/docs.json`

## 6. 当前已确认的问题与风险

### 6.1 Workflow 能力已实现，但公开文档明显滞后

证据：

- 后端 controller 已存在：`src/NekoHub.Api/Controllers/WorkflowsController.cs`
- 前端页面已存在：`web/nekohub-web/src/pages/workflows/WorkflowEditorPage.vue`
- Mintlify 导航没有 workflow 页面：`docs/mintlify/docs.json`

影响：后续 agent 很容易误判“工作流还没做”或重复设计已有能力。

### 6.2 `.gitignore` 忽略了整个 `/tests/`

证据：`.gitignore`

影响：当前已被跟踪的测试文件仍会显示变更，但新增测试文件可能被忽略，导致补测后忘记纳入版本控制。

### 6.3 测试宿主包版本与主项目目标框架存在显式错位

证据：

- API 项目目标 `net10.0`：`src/NekoHub.Api/NekoHub.Api.csproj`
- 测试项目引用 `Microsoft.AspNetCore.Mvc.Testing` `9.0.0`：`tests/NekoHub.Api.IntegrationTests/NekoHub.Api.IntegrationTests.csproj`

影响：短期内未必直接坏，但后续升级测试宿主或处理框架问题时要优先检查版本兼容性。

### 6.4 Workflow 当前有执行/解析覆盖，但缺少显式的 controller 级端点测试文件

证据：

- 已存在 `tests/NekoHub.Api.IntegrationTests/Endpoints/AssetWorkflowExecutionTests.cs`
- 已存在 `tests/NekoHub.Api.IntegrationTests/Unit/WorkflowGraphParserTests.cs`
- 当前 `Endpoints/` 目录中没有单独的 `WorkflowsControllerTests.cs`

影响：workflow profile 管理接口的 HTTP 合约回归风险相对更高。

### 6.5 存储路径在“源码运行”和“Compose 运行”两种模式下不同，容易误判

证据：

- 源码默认相对路径：`src/NekoHub.Api/appsettings.json`
- Compose 挂载 `./data` 到 `/app/storage`：`compose.yaml`

影响：修复文件存储、公开内容或 Data Protection 问题时，如果不先区分运行方式，容易定位错目录。

## 7. 未来 agent 工作前的最小检查清单

1. 先读 `AGENTS.md` 和本文件，再开始搜代码。
2. 先看 `git status`，确认仓库里是否已有用户进行中的改动。
3. 不要再把项目理解成“只有 API key 的旧后台”；当前真实模型是公开站 + JWT 管理台 + API key / MCP 兼容。
4. 改测试时使用显式测试项目路径：`tests/NekoHub.Api.IntegrationTests/NekoHub.Api.IntegrationTests.csproj`。
5. 改文档前先核对 controller、router、store 与现有测试，不要只根据 README 或旧记忆写文档。