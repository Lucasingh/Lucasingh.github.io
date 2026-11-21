---
title: 下载启动器
description: 下载 AovoCL 跨平台 Minecraft 启动器 Windows 版本
tags: [下载, 安装]
---

# 下载启动器

当前最新版本：**v0.2.0**，适用于 Windows 10 / 11（x64），已内置 .NET 8 运行时，通过安装程序部署，无需手动解压。

## 下载方式

国内用户推荐使用蓝奏云网盘下载；GitHub Release 适合海外网络或作为备用源。

<a class="download-button" href="https://wwbgb.lanzoul.com/iTSFX4gvk4ne" target="_blank" rel="noopener noreferrer">蓝奏云网盘下载（国内推荐 · Windows x64 · 29.7 MB）</a>

| 下载方式 | 链接 | 说明 |
| --- | --- | --- |
| 蓝奏云网盘（推荐） | [打开下载页](https://wwbgb.lanzoul.com/iTSFX4gvk4ne) | 国内访问通常更快；提取密码：`7obm` |
| GitHub Release 直链 | [下载安装包](https://github.com/Lucasingh/Lucasingh.github.io/releases/download/v0.2.0/AovoCL-Setup-0.2.0-win-x64.exe) | 官方发布资产；海外网络或已有代理时更稳定 |
| GitHub Releases 列表 | [查看全部版本](https://github.com/Lucasingh/Lucasingh.github.io/releases) | 可查看历史版本与更新记录 |

## 安装步骤

1. 下载 `AovoCL-Setup-0.2.0-win-x64.exe`。
2. 双击安装包并按向导操作；默认仅安装到当前用户目录，无需管理员权限。
3. 可按需勾选「创建桌面快捷方式」，安装完成后从开始菜单或桌面启动 AovoCL。
4. 首次启动后在「设置」里确认游戏目录，再到「下载」页选择版本，等待文件补齐后点击启动。

需要卸载时，可在 Windows「设置 → 应用 → 安装的应用」中找到 **AovoCL 启动器**。

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
Get-FileHash .\AovoCL-Setup-0.2.0-win-x64.exe -Algorithm SHA256
```

```text
b9a516ca60ddc33ab5ca63a8e78ad6e340877cf24855c93f0e923e1bc2c082c7
```

## 版本说明

### v0.2.0

- 启动页与下载页新增版本分类菜单：正式版、快照、Beta、Alpha，并显示各分类数量
- 分类筛选可与版本搜索叠加使用
- 提供 Windows 安装包，支持开始菜单、可选桌面快捷方式与标准卸载

### v0.1.0

- 原版版本一键安装与启动，文件逐个 SHA1 校验，校验通过后原子替换
- 多文件并发 + 大文件分片下载，连接预算可调，镜像异常自动回退官方源
- 自动探测本机 Java，缺失时下载 JRE 17
- 版本管理：在线下载、修复缺失文件、重命名、删除、打开版本目录
- 离线账户：输入用户名即可进入单人游戏与局域网联机

> 本仓库只托管文档站与构建产物，启动器源码不对外公开。
