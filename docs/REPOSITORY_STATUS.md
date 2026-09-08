# NekoHub 仓库状态

> 更新日期：2026-09-08。此文记录代码范围与验证入口；实际测试结果以当次执行和 CI 为准。

## 产品与架构

- 产品入口为匿名公开画廊、JWT 管理台、API key / MCP 机器访问。
- 后端为 .NET 10，分为 Api、Application、Domain、Infrastructure 四个项目；前端为 Vue 3 / TypeScript / Pinia / Vite。
- 数据为部署内共享资产，当前不按用户分配资源归属。权限控制功能操作和公开/私有访问。
- 主解决方案 `NekoHub.slnx` 仅含 `src/` 项目；后端测试须显式指定 `tests/NekoHub.Api.IntegrationTests/NekoHub.Api.IntegrationTests.csproj`。

## 已有能力

- 资产上传、查询、元数据和可见性更新、批量删除、受保护文件及衍生物下载。
- Local、S3-compatible、GitHub Repo 运行时存储，数据库 profile 选择和绑定；GitHub Releases 仍无资产写入运行时实现。
- JWT 登录/轮换/登出、角色与权限管理、密码重置、首次用户种子。
- 缩略图、格式转换、水印、Exif 清理、AI caption 与执行记录。
- 工作流 CRUD、autorun、手动排队；仍为线性执行，连线不参与 DAG 调度。
- 公开内容路由、MCP tools/resources/prompts 与 Streamable HTTP/SSE 入口。

## 当前可靠性机制

- 用户 PATCH 与状态端点共享禁用边界；角色转换限制与创建角色限制一致。
- 密码重置撤销用户会话，刷新令牌使用 PostgreSQL 事务原子轮换；JWT 检查当前会话和角色。
- 浏览器会话绑定后端地址，切换后端或登出取消旧请求；公开站与管理台共享地址解析，公开请求不带凭据。
- 上传与处理任务统一持久化，后台有租约领取、状态查询和失败重试。中断的图片任务由用户核对后重试。
- 删除记录与待删文件清单统一提交，物理清理失败可在后台恢复。
- 已引用的存储 profile 不能原位修改存储位置；未完成清理任务也保留引用保护。
- 保留原图登记为衍生物，原图改变时失效旧缩略图/caption；同一资产的技能、元数据修改和删除使用操作锁。
- MinIO 示例保持私有桶，公开访问经应用代理。

上述接口和恢复边界见 [PROCESSING.md](./PROCESSING.md)。同 key 覆盖及上传在进程骤停窗口仍可能需要对象与数据库核对，当前没有跨存储的恰好一次事务。

## 测试与交付

- 后端测试已经纳入 Git，不再被整个 `/tests/` 忽略。
- 现有后端覆盖资产、存储、公开访问、用户权限、会话、MCP、工作流和图像技能；新增持久化作业、并发令牌、处理失败补偿等回归。
- 前端使用 Vitest，构建先运行 vue-tsc。会话切换、匿名公开请求、资产操作权限、后台任务与原图下载有回归测试。
- `.github/workflows/verify.yml` 执行后端/私有 S3 测试、前端测试和构建；镜像与 Pages 发布依赖该验证。
- 后端集成测试通过 Testcontainers 创建隔离 PostgreSQL；显式启用 S3 时使用隔离 MinIO。若配置 `NEKOHUB_TEST_DATABASE_CONNECTIONSTRING`，会在指定服务创建/删除测试数据库，运行前需核对目标。
- 依赖告警修复限定于 Microsoft.OpenApi 2.7.5 与测试传递依赖 SSH.NET 2026.0.0；测试宿主与 .NET 10 对齐。

## 文档与部署事实

- Mintlify 已有中英文 workflow 指南/API 页面、导航及 WorkflowsControllerTests；旧快照中的“缺失”描述已过期。
- 前端容器使用 Nginx，不是 vite preview；Host 允许列表设置不控制该容器。
- 初始管理员只在用户表为空时创建，不是每次发现缺少 SuperAdmin 都创建。
- Compose 本地数据与 Data Protection keys 挂载在 `./data`；源码运行的相对路径需按 API content root 判断。
- 根 `.gitignore` 保持本地 `.pensieve` 忽略规则；记忆初始化本身不启用自动捕获。

## 验证命令

```bash
dotnet test tests/NekoHub.Api.IntegrationTests/NekoHub.Api.IntegrationTests.csproj
NEKOHUB_RUN_S3_IT=true dotnet test tests/NekoHub.Api.IntegrationTests/NekoHub.Api.IntegrationTests.csproj
cd web/nekohub-web
npm ci
npm run test
npm run build
```
