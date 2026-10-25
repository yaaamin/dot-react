// Runs the .NET API in development and restarts it whenever a server file changes.
//
// Used instead of `dotnet watch` because routes come from file names: adding, renaming or
// deleting a file in server/api/ needs a rebuild and a restart, and `dotnet watch` either
// hot-patches the change without re-registering routes or leaves the old server holding the port.
//
//   bun scripts/dev-api.ts            (part of `bun run dev`, or `bun run dev:api` on its own)

import { readFileSync, watch } from 'node:fs'
import { basename, join, sep } from 'node:path'
import { type Subprocess, spawn } from 'bun'
import { scaffoldIfEmpty } from './api'

const root = join(import.meta.dir, '..')
const serverDir = join(root, 'server')
const watched = [serverDir, join(root, 'generators')]
const ignored = [`${sep}bin${sep}`, `${sep}obj${sep}`, `${sep}wwwroot${sep}`]

// Same URL as `dotnet run` would use, so the Vite proxy keeps working.
const launchSettings = JSON.parse(readFileSync(join(serverDir, 'Properties', 'launchSettings.json'), 'utf8').replace(/^﻿/, ''))
const url: string = launchSettings.profiles.http.applicationUrl

let server: Subprocess | undefined
let building = false
let pending = false
let timer: Timer | undefined

async function stop() {
  if (!server) return
  const proc = server
  server = undefined
  proc.kill('SIGTERM')
  const exited = await Promise.race([proc.exited.then(() => true), Bun.sleep(5000).then(() => false)])
  if (!exited) proc.kill('SIGKILL')
}

async function restart() {
  if (building) {
    pending = true
    return
  }
  building = true
  await stop()

  const build = spawn(['dotnet', 'build', 'server', '--nologo', '-v', 'q'], { cwd: root, stdout: 'pipe', stderr: 'inherit' })
  const output = await new Response(build.stdout).text()
  const ok = (await build.exited) === 0
  building = false

  // Only show errors and warnings (msbuild prints each twice: inline and in its summary)
  const problems = new Set(output.split('\n').filter(line => / (error|warning) [A-Z]+\d+:/.test(line)).map(line => line.trim()))
  for (const line of problems) console.log(line)

  if (pending) {
    pending = false
    return restart()
  }
  if (!ok) {
    console.log('build failed, waiting for a file to change...')
    return
  }

  // Run the built dll directly: no `dotnet run` wrapper, so stop() always reaches the real process.
  server = spawn(['dotnet', join('bin', 'Debug', 'net10.0', 'Server.dll')], {
    cwd: serverDir,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: url },
    stdout: 'inherit',
    stderr: 'inherit',
  })
}

function onChange(dir: string, name: string | null) {
  if (!name) return
  const file = join(dir, name)
  if (ignored.some(part => file.includes(part))) return
  if (!/\.(cs|csproj|json)$/.test(file) || basename(file).startsWith('.')) return // skip editor temp files

  // An empty file in server/api/ gets starter code; that write triggers this again.
  if (scaffoldIfEmpty(file)) return

  clearTimeout(timer)
  timer = setTimeout(() => {
    console.log(`changed: ${file.slice(root.length + 1)}, restarting API...`)
    restart()
  }, 200)
}

for (const dir of watched) watch(dir, { recursive: true }, (_, name) => onChange(dir, name))

for (const signal of ['SIGINT', 'SIGTERM'] as const) {
  process.on(signal, async () => {
    await stop()
    process.exit(0)
  })
}

restart()
