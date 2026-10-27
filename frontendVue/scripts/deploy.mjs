// 把 dist 複製到 backendTray\frontend\dist，之後執行 backendTray 的 build\build.ps1 就會打包進安裝程式。
// 目標資料夾預設是同層的 ..\backendTray\frontend\dist，可用環境變數 TRAY_FRONTEND_DIST 改成其他位置。
import { cpSync, existsSync, readdirSync, rmSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const projectRoot = fileURLToPath(new URL('..', import.meta.url))
const source = join(projectRoot, 'dist')
const target = resolve(process.env.TRAY_FRONTEND_DIST ?? join(projectRoot, '..', 'backendTray', 'frontend', 'dist'))

function fail(message) {
  console.error(`deploy: ${message}`)
  process.exit(1)
}

if (!existsSync(join(source, 'index.html'))) fail(`找不到 ${join(source, 'index.html')}，請先執行 npm run build。`)
if (!existsSync(target)) fail(`找不到目標資料夾 ${target}。可用環境變數 TRAY_FRONTEND_DIST 指定位置。`)

// 先清空舊版檔案，避免已刪除的檔案殘留；.gitkeep 是 backendTray 版控用的，要保留。
for (const name of readdirSync(target)) {
  if (name !== '.gitkeep') rmSync(join(target, name), { recursive: true, force: true })
}
cpSync(source, target, { recursive: true })

console.log(`deploy: 已複製到 ${target}`)
console.log('deploy: 接著在 backendTray 執行 .\\build\\build.ps1 重新打包。')
