---
title: tools.json 索引
description: 文档站自动生成的机器可读索引，字段说明与拉取方式
tags: [索引, 自动化, API]
---

# tools.json 索引

构建和本地开发时，`web/scripts/gen-tools.mjs` 会扫描 `docs` 下的所有 Markdown，
读取 frontmatter 后生成 `docs/public/tools.json`，随站点一起发布到根路径 `/tools.json`。
工具程序可以直接读取它来获取文档目录，而不必解析 HTML。

## frontmatter 字段

在 Markdown 文件开头使用 YAML frontmatter：

```markdown
---
title: 版本管理
description: 下载新版本、重命名、删除本地版本
tags: [版本, 下载, 重命名]
updated: 2026-11-14
---
```

| 字段 | 是否必填 | 说明 |
| --- | --- | --- |
| `title` | 否 | 缺省时取正文第一个一级标题，再缺省则用文件名 |
| `description` | 否 | 一句话摘要，写入索引 |
| `tags` | 否 | 字符串数组或逗号分隔字符串 |
| `updated` | 否 | 更新日期，缺省使用文件修改时间 |
| `draft` | 否 | 为 `true` 时不进入索引 |

首页这类 `layout: home` 的页面不需要额外配置，同样会进入索引。

## 索引结构

```json
{
  "name": "AovoCL 文档",
  "description": "AovoCL 跨平台 Minecraft 启动器文档",
  "base": "/",
  "generatedAt": "2026-11-14T02:10:00.000Z",
  "count": 5,
  "items": [
    {
      "id": "guide/version-manager",
      "title": "版本管理",
      "description": "下载新版本、重命名、删除本地版本",
      "tags": ["版本", "下载", "重命名"],
      "url": "/guide/version-manager.html",
      "file": "docs/guide/version-manager.md",
      "updated": "2026-11-14"
    }
  ]
}
```

- `url` 已包含部署 `base` 前缀，可直接拼域名使用。
- `file` 是仓库内的源文件路径，适合在本地直接读取 Markdown 原文。
- `count` 与 `items` 长度一致，方便做完整性校验。

## 拉取方式

部署后（GitHub Pages 或 Cloudflare Pages）：

```powershell
Invoke-RestMethod "https://<你的站点>/tools.json"
```

本地开发时可以直接读取生成文件，或通过 dev server 获取：

```powershell
# 先生成一次
npm run gen

# 或启动本地服务后访问
npm run dev
Invoke-RestMethod "http://localhost:5173/tools.json"
```

## 与站点构建的关系

`npm run dev` 与 `npm run build` 都会先执行生成脚本，因此不需要手动维护 `tools.json`；
该文件已加入 `.gitignore`，避免构建产物进入版本库。
