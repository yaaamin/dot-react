// Scaffolds endpoint files in server/api/. The route itself comes from the file name
// (see generators/ApiRouteGenerator.cs); this only writes the starter code.
//
//   bun run api:new users/[id].get    → creates server/api/users/[id].get.cs
//
// scripts/dev-api.ts also uses scaffoldIfEmpty() to fill in empty files created during `bun run dev`.

import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, sep } from 'node:path'

export const apiDir = join(import.meta.dir, '..', 'server', 'api')
const httpMethods = ['get', 'post', 'put', 'patch', 'delete', 'head', 'options']

const pascal = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

// Mirrors ParseRoute in the generator: "users/[id].get.cs" → GET /api/users/{id}, class GetUsersById
function describe(relativePath: string) {
  const segments = relativePath.replace(/\.cs$/, '').split('/')
  let method = 'get'
  const last = segments.at(-1)!
  const dot = last.lastIndexOf('.')
  if (dot > 0 && httpMethods.includes(last.slice(dot + 1).toLowerCase())) {
    method = last.slice(dot + 1).toLowerCase()
    segments[segments.length - 1] = last.slice(0, dot)
  }
  if (segments.at(-1)?.toLowerCase() === 'index') segments.pop()

  const route = '/api/' + segments.map(s => s.replace(/\[\.\.\.(\w+)\]/g, '{*$1}').replace(/\[(\w+)\]/g, '{$1}')).join('/')
  const className = pascal(method) + segments
    .map(s => s.replace(/\[(?:\.\.\.)?(\w+)\]/g, (_, p) => 'By' + pascal(p)))
    .map(s => s.split(/[^A-Za-z0-9]+/).map(pascal).join(''))
    .join('')
  const params = [...relativePath.matchAll(/\[(?:\.\.\.)?(\w+)\]/g)].map(m => m[1])

  return { method: method.toUpperCase(), route: route.replace(/\/$/, '') || '/api', className, params }
}

function template(relativePath: string) {
  const { method, route, className, params } = describe(relativePath)
  const hasBody = ['POST', 'PUT', 'PATCH'].includes(method)
  const args = [...params.map(p => `string ${p}`), ...(hasBody ? ['Request body'] : [])].join(', ')
  const result = [...params, ...(hasBody ? ['body'] : [])].join(', ')

  return `namespace Server.Api;

// ${method} ${route}
public static class ${className}
{
${hasBody ? '    public record Request(string Name);\n\n' : ''}    public static IResult Handle(${args}) =>
        TypedResults.Ok(new { ${result || 'message = "Hello from ' + method + ' ' + route + '"'} });

    // Optional: extra endpoint setup (summary, auth, caching, ...)
    public static void Configure(RouteHandlerBuilder endpoint) =>
        endpoint.WithSummary("${method} ${route}");
}
`
}

function scaffold(file: string) {
  const rel = relative(apiDir, file).split(sep).join('/')
  writeFileSync(file, template(rel))
  const { method, route } = describe(rel)
  console.log(`scaffolded server/api/${rel}  →  ${method} ${route}`)
}

/** Writes the starter code into `file` if it's an empty .cs file in server/api/. Returns true if it did. */
export function scaffoldIfEmpty(file: string) {
  if (!file.startsWith(apiDir + sep) || !file.endsWith('.cs')) return false
  try {
    if (statSync(file).size > 0) return false
  } catch {
    return false // Deleted or renamed away
  }
  scaffold(file)
  return true
}

if (import.meta.main) {
  const [command, arg] = process.argv.slice(2)

  if (command === 'new' && arg) {
    const file = join(apiDir, arg.endsWith('.cs') ? arg : `${arg}.cs`)
    if (existsSync(file) && readFileSync(file, 'utf8').trim()) {
      console.error(`${relative(process.cwd(), file)} already exists`)
      process.exit(1)
    }
    mkdirSync(dirname(file), { recursive: true })
    scaffold(file)
  } else {
    console.log('usage: bun scripts/api.ts new <path, e.g. users/[id].get>')
    process.exit(command ? 1 : 0)
  }
}
