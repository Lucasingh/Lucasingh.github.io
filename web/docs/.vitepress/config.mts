import { defineConfig } from 'vitepress'

// GitHub Pages 项目站点需为 /<仓库名>/，由部署工作流通过 DOCS_BASE 注入；本地默认根路径
const base = process.env.DOCS_BASE || '/'

export default defineConfig({
  lang: 'zh-CN',
  title: 'AovoCL',
  description: 'AovoCL 跨平台 Minecraft 启动器文档',
  base,
  lastUpdated: true,
  metaChunk: true,
  themeConfig: {
    nav: [
      { text: '指南', link: '/guide/getting-started' },
      { text: '参考', link: '/reference/tools-json' }
    ],
    sidebar: [
      {
        text: '指南',
        items: [
          { text: '快速开始', link: '/guide/getting-started' },
          { text: '版本管理', link: '/guide/version-manager' },
          { text: '常见问题', link: '/guide/faq' }
        ]
      },
      {
        text: '参考',
        items: [{ text: 'tools.json 索引', link: '/reference/tools-json' }]
      }
    ],
    search: {
      provider: 'local'
    },
    outline: {
      level: [2, 3],
      label: '本页目录'
    },
    docFooter: {
      prev: '上一页',
      next: '下一页'
    },
    lastUpdated: {
      text: '最后更新',
      formatOptions: { dateStyle: 'short', timeStyle: 'short' }
    },
    darkModeSwitchLabel: '外观',
    returnToTopLabel: '回到顶部',
    sidebarMenuLabel: '目录',
    footer: {
      message: '基于 Markdown 构建，构建产物为纯静态文件',
      copyright: 'AovoCL'
    }
  }
})
