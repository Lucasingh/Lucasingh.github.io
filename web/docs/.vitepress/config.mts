import { defineConfig } from 'vitepress'

// GitHub Pages 项目站点需为 /<仓库名>/，由部署工作流通过 DOCS_BASE 注入；本地与用户站点默认根路径
const base = process.env.DOCS_BASE || '/'

export default defineConfig({
  lang: 'zh-CN',
  title: 'AovoCL',
  description: 'AovoCL 跨平台 Minecraft 启动器文档',
  base,
  lastUpdated: true,
  metaChunk: true,
  head: [
    ['meta', { name: 'theme-color', content: '#2f6feb' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'AovoCL 文档' }],
    ['meta', { property: 'og:description', content: 'AovoCL 跨平台 Minecraft 启动器使用文档' }]
  ],
  themeConfig: {
    nav: [
      { text: '指南', link: '/guide/getting-started', activeMatch: '/guide/' },
      { text: '参考', link: '/reference/architecture', activeMatch: '/reference/' },
      { text: 'tools.json', link: '/tools.json' }
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
        items: [
          { text: '项目架构', link: '/reference/architecture' },
          { text: 'tools.json 索引', link: '/reference/tools-json' }
        ]
      }
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/Lucasingh/Lucasingh.github.io' }
    ],
    search: {
      provider: 'local',
      options: {
        translations: {
          button: {
            buttonText: '搜索文档',
            buttonAriaLabel: '搜索文档'
          },
          modal: {
            noResultsText: '没有找到相关结果',
            resetButtonTitle: '清除搜索条件',
            footer: {
              selectText: '选择',
              navigateText: '切换',
              closeText: '关闭'
            }
          }
        }
      }
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
    notFound: {
      title: '页面不存在',
      quote: '这个地址没有对应的文档，可能是链接过期或输入有误。',
      linkText: '返回首页',
      linkLabel: '回到首页'
    },
    footer: {
      message: '基于 Markdown 构建，构建产物为纯静态文件',
      copyright: 'AovoCL'
    }
  }
})
