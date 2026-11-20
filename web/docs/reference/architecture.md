---
title: 项目架构
description: AovoCL 仓库结构、Core 模块职责、下载流水线与镜像映射规则
tags: [架构, 开发, Core, 下载]
---

# 项目架构

::: info 说明
本页用于说明启动器的内部结构，方便理解行为与排查问题。启动器源码不对外公开，本仓库只托管文档站与构建产物。
:::

## 仓库结构

```text
AovoCL/
├─ AovoCL.sln
├─ src/
│  ├─ AovoCL.Core/        # 纯 .NET 8 类库：下载、安装、Java、启动
│  ├─ AovoCL.Desktop/     # Avalonia 11 桌面端
│  └─ AovoCL.Cli/         # 命令行验证工具
├─ web/docs/              # 本文档站（VitePress）
└─ testgame/              # 验证用游戏实例
```

三层职责：Core 不依赖 UI，桌面端与 CLI 都只调用 Core 的公开 API。

## Core 模块

| 模块 | 职责 |
| --- | --- |
| `GamePaths` | 游戏目录布局（versions / libraries / assets / runtime / cache） |
| `MirrorOptions` | 官方地址到 BMCLAPI 镜像的映射规则，失败回退官方 |
| `FileDownloader` | 并发下载、Range 分片、SHA1 校验、原子替换、进度与测速 |
| `VersionService` | 拉取版本清单与版本 JSON，展开依赖库与资源文件列表 |
| `GameInstaller` | 组装下载任务并安装一个完整版本 |
| `JavaRuntimeManager` | 探测本机 Java，必要时下载 JRE |
| `LauncherService` | 对外门面：安装、启动、检测 Java、本地版本管理 |

## 下载流水线

```text
版本清单 → 版本 JSON → 文件列表
        → 已存在且 SHA1 匹配则跳过
        → 全局连接预算内并发下载（镜像优先）
            · ≥ 4 MB 文件先探测 Range，支持则分块多连接
            · 单片失败退避重试，最终回退官方源
        → 整文件 SHA1 校验
        → File.Move 原子替换，失败清理临时文件
```

关键约束：

- **全局连接预算**：单文件占 1 个槽，分片每条连接占 1 个槽，总连接数不超过「下载并发」设置。
- **不牺牲正确性**：任何提速都以整文件 SHA1 校验通过、原子替换为前提。
- **可取消**：全链路传入 `CancellationToken`，取消时停止分片并清理临时文件。

## 镜像映射

| 官方地址 | 镜像地址 |
| --- | --- |
| `piston-meta.mojang.com` | `bmclapi2.bangbang93.com` |
| `launchermeta.mojang.com` | `bmclapi2.bangbang93.com` |
| `launcher.mojang.com` | `bmclapi2.bangbang93.com` |
| `resources.download.minecraft.net` | `bmclapi2.bangbang93.com/assets` |
| `libraries.minecraft.net` | `bmclapi2.bangbang93.com/maven` |

关闭「镜像加速」后全部走官方地址；镜像请求失败时按文件回退官方源。

## 下载参数默认值

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| 全局并发 | 16 | 单文件与分片共享 |
| 分片并发 | 4 | 单个大文件内部的连接数 |
| 分片阈值 | 4 MB | 小于该值不分片 |
| 最小分片 | 1 MB | 避免切出大量碎片 |
| 启用分片 | 开启 | 服务器不支持 Range 时自动回退 |

## 构建与运行

```powershell
# 构建整套解决方案
C:\AovoCL\.dotnet\dotnet.exe build AovoCL.sln

# 运行桌面端
C:\AovoCL\.dotnet\dotnet.exe run --project src\AovoCL.Desktop
```

要求 .NET 8 SDK；桌面端依赖 Avalonia 11。
