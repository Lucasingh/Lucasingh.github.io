---
title: 下载启动器
description: 下载 AovoCL 跨平台 Minecraft 启动器 Windows 版本
tags: [下载, 安装]
---

# 下载启动器

当前最新版本：**v0.1.0**，适用于 Windows 10 / 11（x64），已内置 .NET 8 运行时，解压即用。

<a class="download-button" href="https://github.com/Lucasingh/Lucasingh.github.io/releases/download/v0.1.0/AovoCL-0.1.0-win-x64.zip">下载 AovoCL v0.1.0（Windows x64 · 41 MB）</a>

所有版本与更新记录：[GitHub Releases](https://github.com/Lucasingh/Lucasingh.github.io/releases)

## 安装步骤

1. 下载 `AovoCL-0.1.0-win-x64.zip`。
2. 解压到任意目录，例如 `D:\Apps\AovoCL`（不要直接放在压缩包里运行）。
3. 双击 `AovoCL.Desktop.exe` 启动。
4. 首次启动后在「设置」里确认游戏目录，再到「下载」页选择版本，等待文件补齐后点击启动。

::: tip 首次运行提示
Windows SmartScreen 可能提示「已保护你的电脑」，这是因为可执行文件没有代码签名。
点击「更多信息」→「仍要运行」即可。
:::

## 系统要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 及以上（x64） |
| 磁盘空间 | 启动器本体约 95 MB，每个游戏版本约 700 MB |
| 运行时 | 无需安装，已自包含 .NET 8 |
| Java | 首次启动自动探测；缺失时自动下载 JRE 17 |
| 网络 | 需要访问 BMCLAPI 镜像或 Mojang 官方源 |

## 文件校验

下载完成后可以核对 SHA256，确认文件完整：

```powershell
Get-FileHash .\AovoCL-0.1.0-win-x64.zip -Algorithm SHA256
```

```text
0134c4ab47b7d9226f276fe6b9812f3fae0e1e81008b36e5f9c0aad321030df7
```

## 版本说明

### v0.1.0

- 原版版本一键安装与启动，文件逐个 SHA1 校验，校验通过后原子替换
- 多文件并发 + 大文件分片下载，连接预算可调，镜像异常自动回退官方源
- 自动探测本机 Java，缺失时下载 JRE 17
- 版本管理：在线下载、修复缺失文件、重命名、删除、打开版本目录
- 离线账户：输入用户名即可进入单人游戏与局域网联机

> 本仓库只托管文档站与构建产物，启动器源码不对外公开。
