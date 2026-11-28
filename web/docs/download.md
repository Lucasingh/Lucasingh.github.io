---
title: 下载启动器
description: 下载 AovoCL 跨平台 Minecraft 启动器 Windows 版本
tags: [下载, 安装]
---

# 下载启动器

<div class="dl-hero">
  <div class="dl-hero__left">
    <span class="dl-hero__label">最新版本</span>
    <span class="dl-hero__ver">v0.2.5</span>
  </div>
  <div class="dl-hero__right">
    <span>Windows 10 / 11 · x64</span>
    <span>自带 .NET 8 运行时</span>
    <span>安装包 31.4 MB</span>
  </div>
</div>

## 下载方式

国内用户推荐使用蓝奏云网盘下载；GitHub Release 适合海外网络或作为备用源。

<div class="dl-grid">
  <a class="dl-card dl-card--primary" href="https://wwbgb.lanzoul.com/b01i61qfba" target="_blank" rel="noopener noreferrer">
    <span class="dl-card__tag">国内推荐</span>
    <span class="dl-card__title">蓝奏云网盘</span>
    <span class="dl-card__desc">国内访问通常更快，无需代理</span>
    <span class="dl-card__meta">提取密码 i3xf · 含最近两个版本</span>
    <span class="dl-card__cta">前往下载 →</span>
  </a>
  <a class="dl-card" href="https://github.com/Lucasingh/Lucasingh.github.io/releases/download/v0.2.5/AovoCL-Setup-0.2.5-win-x64.exe" target="_blank" rel="noopener noreferrer">
    <span class="dl-card__tag dl-card__tag--ghost">备用</span>
    <span class="dl-card__title">GitHub Release 直链</span>
    <span class="dl-card__desc">官方发布资产，海外网络或已有代理时更稳定</span>
    <span class="dl-card__meta">AovoCL-Setup-0.2.5-win-x64.exe</span>
    <span class="dl-card__cta">直接下载 →</span>
  </a>
  <a class="dl-card" href="https://wwbgb.lanzoul.com/b01i61qfcb" target="_blank" rel="noopener noreferrer">
    <span class="dl-card__tag dl-card__tag--ghost">归档</span>
    <span class="dl-card__title">蓝奏云历史版本</span>
    <span class="dl-card__desc">早期版本安装包归档，需要旧版时使用</span>
    <span class="dl-card__meta">提取密码 i3xf · v0.2.0 – v0.2.2</span>
    <span class="dl-card__cta">查看历史版本 →</span>
  </a>
  <a class="dl-card" href="https://github.com/Lucasingh/Lucasingh.github.io/releases" target="_blank" rel="noopener noreferrer">
    <span class="dl-card__tag dl-card__tag--ghost">全部</span>
    <span class="dl-card__title">GitHub Releases 列表</span>
    <span class="dl-card__desc">查看全部发布记录、资产与逐版本更新说明</span>
    <span class="dl-card__meta">含更新日志与校验信息</span>
    <span class="dl-card__cta">打开列表 →</span>
  </a>
</div>


## 安装步骤

1. 下载 `AovoCL-Setup-0.2.5-win-x64.exe`。
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
Get-FileHash .\AovoCL-Setup-0.2.5-win-x64.exe -Algorithm SHA256
```

```text
4a6696abf468d943b8f0a69037ebde0e2a6c79a1df12d0f15e7a54b7119fac20
```

## 版本说明

### v0.2.5

- 新增版本管理子系统：多游戏文件夹绑定、版本自动分类与排序、版本隔离、继承链识别
- 启动页改为卡片式版本列表，支持搜索、分类筛选、收藏星标与右键菜单
- 新增独立的「日志」页，运行输出集中查看并自动滚动
- 统一视觉：下拉框、按钮、卡片与页面切换动效，移除重复的工具栏入口

### v0.2.4

- 安装包改为自带 .NET 8 运行时，不再要求目标电脑预装 .NET，修复部分电脑提示缺少 .NET 无法启动的问题
- 更新窗口支持取消下载与失败重试
- 更新包下载最多 32 线程分段，自动选择最快下载源
- 修复 BMCLAPI 版本清单重定向导致的「版本列表获取失败」，镜像不可用时自动回退官方源

### v0.2.3

- 更新改为独立窗口，展示总进度、实时下载速度、剩余时间与每条下载线程的进度
- 更新包下载最多 32 线程分段，自动选择最快下载源，失败自动切换
- 下载更新时可随时取消，失败后可一键重试
- 修复 BMCLAPI 版本清单重定向导致的「版本列表获取失败」，镜像不可用时自动回退官方源

### v0.2.2

- 更新包下载提速：下载前自动测速，从多个下载源中挑选最快的一个
- 新增 GitHub 加速镜像作为备用下载源，直连不畅时可自动切换
- 改为多连接分段下载，大安装包速度明显提升
- 遇到下载源限流会自动退避并从断点续传，不再整包重下
- 状态栏在下载更新时显示实时速度

### v0.2.1

- 启动时自动检查更新，发现新版本会展示更新内容，可选择立即更新或稍后
- 设置页新增「检查更新」，可查看当前版本并手动检查
- 下载总览显示阶段总进度、下载速度、本批剩余文件与当前文件
- 下载页新增资源类型与加载器入口，Mod、整合包、Forge 等将陆续支持
- 启动页只显示已安装完成的版本，重命名后的版本也能正常显示与启动
- 新增 AovoCL 风格标题栏与按钮、列表、进度条、导航收起展开动画

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
