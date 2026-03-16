# 文档贡献说明

本目录用于维护 NekoHub 的 Mintlify 文档站。

## 提交流程

1. 在仓库里修改 `docs/mintlify`
2. 本地运行 `mint dev`
3. 检查中文根路径和 `/en/...` 英文路径
4. 提交变更

## 写作规则

- 中文页是唯一原稿，英文页跟随同步
- URL slug 保持英文，不翻译路由
- 新增页面时，中英文目录结构必须一一对应
- 接口示例必须基于当前代码和真实接口行为
- 统一使用项目真实术语：
  - 角色：`superAdmin`、`admin`、`user`
  - 权限：点号格式，例如 `assets.read`
  - API key：`Authorization: Bearer <API_KEY>`

## 提交前自检

- 文案是否与当前后端实现一致
- 中英文导航、卡片链接、交叉引用是否一致
- 是否残留过时文案，例如旧 Header、旧角色名或旧权限格式
- 是否保持了 Mintlify 页面 frontmatter 和组件语法有效
