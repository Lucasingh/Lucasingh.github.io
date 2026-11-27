import DefaultTheme from 'vitepress/theme'
import { h } from 'vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      // 首页 Hero 上方的版本徽标
      'home-hero-info-before': () =>
        h('a', { class: 'hero-badge', href: '/download' }, [
          h('span', { class: 'hero-badge__dot' }),
          h('span', { class: 'hero-badge__text' }, 'v0.2.4 已发布'),
          h('span', { class: 'hero-badge__sep' }, '·'),
          h('span', { class: 'hero-badge__note' }, '自带 .NET 运行时')
        ])
    })
  }
}
