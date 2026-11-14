// 扫描 docs 下的 Markdown frontmatter，生成 docs/public/tools.json 供工具程序拉取。
// 用法：node scripts/gen-tools.mjs（npm run dev / build 会自动执行）
import fs from 'node:fs'
import path from 'node:path'
import matter from 'gray-matter'

const webRoot = path.resolve(import.meta.dirname, '..')
const docsDir = path.join(webRoot, 'docs')
const outDir = path.join(docsDir, 'public')
const outFile = path.join(outDir, 'tools.json')

// GitHub Pages 项目站点部署在 /<仓库名>/ 下，构建时通过 DOCS_BASE 传入
const base = normalizeBase(process.env.DOCS_BASE || '/')
const siteName = 'AovoCL 文档'
const siteDescription = 'AovoCL 跨平台 Minecraft 启动器文档'

function normalizeBase(value) {
  let result = value.trim()
  if (!result.startsWith('/')) result = '/' + result
  if (!result.endsWith('/')) result += '/'
  return result
}

function walk(dir, files = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === '.vitepress' || entry.name === 'public') continue
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) walk(full, files)
    else if (entry.name.endsWith('.md')) files.push(full)
  }
  return files
}

function firstHeading(content) {
  const match = content.match(/^\s*#\s+(.+?)\s*$/m)
  return match ? match[1].trim() : ''
}

function toUrl(relPath) {
  const route = relPath.replace(/\.md$/, '')
  if (route === 'index') return base
  if (route.endsWith('/index')) return base + route.slice(0, -'index'.length)
  return base + route + '.html'
}

function toTags(value) {
  if (Array.isArray(value)) return value.map(item => String(item).trim()).filter(Boolean)
  if (typeof value === 'string' && value.trim()) return value.split(',').map(item => item.trim()).filter(Boolean)
  return []
}

function toDate(value, fallback) {
  if (value instanceof Date) return value.toISOString().slice(0, 10)
  if (typeof value === 'string' && value.trim()) return value.trim()
  return fallback
}

const items = walk(docsDir)
  .filter(file => !file.includes(`${path.sep}public${path.sep}`))
  .map(file => {
    const raw = fs.readFileSync(file, 'utf8')
    const { data, content } = matter(raw)
    if (data.draft === true) return null

    const rel = path.relative(docsDir, file).split(path.sep).join('/')
    const stat = fs.statSync(file)
    const date = stat.mtime.toISOString().slice(0, 10)

    return {
      id: rel.replace(/\.md$/, ''),
      title: data.title || firstHeading(content) || rel.replace(/\.md$/, ''),
      description: data.description || '',
      tags: toTags(data.tags),
      url: toUrl(rel),
      file: 'docs/' + rel,
      updated: toDate(data.updated, date)
    }
  })
  .filter(Boolean)
  .sort((a, b) => a.id.localeCompare(b.id))

const index = {
  name: siteName,
  description: siteDescription,
  base,
  generatedAt: new Date().toISOString(),
  count: items.length,
  items
}

fs.mkdirSync(outDir, { recursive: true })
fs.writeFileSync(outFile, JSON.stringify(index, null, 2) + '\n', 'utf8')
console.log(`[tools.json] 已生成 ${items.length} 条记录 -> ${path.relative(webRoot, outFile)}`)
