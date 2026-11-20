---
layout: home
title: 文档首页
description: AovoCL 跨平台 Minecraft 启动器使用文档

hero:
  name: AovoCL
  text: 跨平台 Minecraft 启动器
  tagline: 参考 PCL2 架构打造 · Windows / Linux / macOS 通用 · 原版一键启动
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/getting-started
    - theme: alt
      text: 常见问题
      link: /guide/faq
    - theme: alt
      text: 下载 tools.json
      link: /tools.json

features:
  - title: 原版一键启动
    details: 自动补全客户端、依赖库与资源文件，逐个 SHA1 校验通过后原子落盘，再拉起游戏。
  - title: 版本管理
    details: 内置官方版本清单，支持下载、修复、重命名、删除与打开版本目录。
  - title: 自动 Java
    details: 扫描本机 Java 并按版本匹配；找不到时自动下载对应 JRE，国内优先清华镜像。
  - title: 下载加速
    details: BMCLAPI 镜像 + 多文件并发 + 大文件分片，连接预算可调，镜像异常自动回退官方源。
  - title: 离线账户
    details: 输入用户名即可进入单人游戏与局域网联机，无需登录。
  - title: 跨平台
    details: Core 为纯 .NET 8 类库，桌面端基于 Avalonia 11，同一套逻辑跑三个系统。
---

## 三分钟了解

1. **选择目录**：默认 `%APPDATA%\AovoCL\minecraft`，也可以直接指向官方启动器或 PCL2 的游戏目录。
2. **选择版本**：在「下载」页搜索并下载目标版本，或直接从已有本地版本中挑选。
3. **点击启动**：启动器补齐缺失文件、准备 Java、生成启动参数并拉起游戏，日志实时输出到「运行日志」面板。

首次启动 1.20.1 大约需要下载 700 MB 文件，速度取决于网络与镜像节点。

## 文档地图

| 文档 | 内容 |
| --- | --- |
| [快速开始](/guide/getting-started) | 系统要求、首次启动流程、下载调优、目录布局 |
| [版本管理](/guide/version-manager) | 在线清单、下载与修复、重命名、删除、打开目录 |
| [常见问题](/guide/faq) | 启动无反应、Java 不匹配、下载慢、离线验证提示 |
| [项目架构](/reference/architecture) | 仓库结构、Core 模块职责、下载流水线与镜像规则 |
| [tools.json 索引](/reference/tools-json) | 机器可读的文档索引，字段说明与拉取方式 |

## 关于本站

本站内容全部来自仓库内 `web/docs` 下的 Markdown，由 VitePress 构建为纯静态站点并部署在 GitHub Pages。
构建时额外生成 [`tools.json`](/tools.json) 索引，外部工具可以直接拉取文档目录，无需解析 HTML。
