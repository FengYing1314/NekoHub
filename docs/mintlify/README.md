# NekoHub Mintlify Docs

这个目录是 NekoHub 的 Mintlify 文档站点源码。

## 目录说明

- `docs.json`：站点配置、导航和多语言入口
- 根目录 `.mdx`：默认语言为简体中文的页面
- `en/`：英文镜像页面
- `api/`、`concepts/`、`configuration/`、`guides/`：中文主站页面分组
- `en/api/`、`en/concepts/`、`en/configuration/`、`en/guides/`：英文镜像页面分组

## 本地预览

在 `docs/mintlify` 目录运行：

```bash
mint dev
```

默认预览地址：

```text
http://localhost:3000
```

## 维护约定

- 中文页是原稿，英文页从中文同步
- 新增或修改页面时，保持中英文路径结构一致
- 所有接口示例必须以当前后端真实行为为准
- API key 示例统一使用 `Authorization: Bearer <API_KEY>`
- 用户角色统一使用 `superAdmin`、`admin`、`user`

## 发布前检查

- 本地运行 `mint dev`，确认中英文导航和内部链接都可用
- 全局搜索确认没有残留旧 Header、旧角色名或旧权限格式等过时文案
- 对照当前后端接口确认响应字段、错误码和 Header 写法没有漂移
