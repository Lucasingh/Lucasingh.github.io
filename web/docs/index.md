---
layout: home
title: 文档首页
description: AovoCL 跨平台 Minecraft 启动器使用文档

hero:
  name: AovoCL
  text: 跨平台 Minecraft 启动器文档
  tagline: 参考 PCL2 架构打造，Windows / Linux / macOS 通用
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/getting-started
    - theme: alt
      text: 版本管理
      link: /guide/version-manager

features:
  - title: 原版一键启动
    details: 自动补全客户端、依赖库与资源文件，SHA1 校验通过后直接拉起游戏。
  - title: 版本管理
    details: 内置版本清单，支持下载新版本、重命名、删除与打开版本目录。
  - title: 自动 Java
    details: 扫描本机 Java；找不到时自动下载匹配的 JRE，国内优先清华镜像。
  - title: 下载加速
    details: BMCLAPI 镜像 + 多文件并发 + 大文件分片，连接数可调，镜像异常自动回退官方源。
---

## 站点说明

本站是 AovoCL 的使用文档，内容全部来自仓库内 `web/docs` 下的 Markdown 文件。
构建时会额外生成一份 `tools.json` 索引，供外部工具程序直接拉取文档目录。
