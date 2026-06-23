---
name: project-analyzer
description: Analyze any software project (frontend, backend, or full-stack) and generate a comprehensive project-context.json documenting the complete tech stack — language, runtime, framework, build tools, testing, quality tools, styling, persistence, API style, observability, deployment, and development setup. Use when the user asks to analyze a project, document a tech stack, generate project context, understand what technologies a project uses, or create technical documentation for a codebase. Triggers on phrases like "analyze this project", "what tech stack", "generate project-context", "document this codebase", "scan project dependencies". Works for any ecosystem — Node.js, Java, Python, Go, .NET, Ruby, PHP.
---

# Project Analyzer

Analyze a project directory and produce a single `project-context.json` file that captures the full tech stack in a machine-readable format. The output schema is the same regardless of whether the project is frontend, backend, or full-stack — irrelevant fields are set to `null` or empty arrays.

## Workflow

### Step 1: Identify Project Root

Determine the project root directory:

- Use the path the user provides explicitly
- Fall back to the current working directory if it looks like a repo root (`.git/`, `README.md`, build manifests present)
- Ask the user if ambiguous

Detect the project type by which manifests and source patterns exist:

| Signal                                                                                            | Suggests      |
| ------------------------------------------------------------------------------------------------- | ------------- |
| `package.json` with `react`, `vue`, `angular`, `svelte`, `next`                                   | **frontend**  |
| `package.json` with `express`, `fastify`, `@nestjs/*`, `koa`                                      | **backend**   |
| `pom.xml`, `build.gradle*`, `*.csproj`, `go.mod`, `pyproject.toml`, `Gemfile`, `composer.json`    | **backend**   |
| Both frontend framework deps AND server deps in one `package.json` (e.g. Next.js with API routes) | **fullstack** |

Set the `projectType` field accordingly.

### Step 2: Read Core Configuration Files

Read the build/dependency manifest first — this is the single most informative file. Then read whatever configuration files exist. Do not fabricate data for files that are missing.

**Read order:**

1. **Manifest** — `package.json`, `pom.xml`, `build.gradle*`, `*.csproj`, `pyproject.toml`, `go.mod`, `Gemfile`, `composer.json`
2. **Lock file** — to confirm package manager (`package-lock.json` → npm, `yarn.lock` → Yarn, `pnpm-lock.yaml` → pnpm, `bun.lockb` → Bun, `poetry.lock` → Poetry, etc.)
3. **Language / build config** — `tsconfig.json`, `vite.config.*`, `webpack.config.*`, `next.config.*`, `angular.json`, `application.yml`, `appsettings.json`
4. **Lint / format / test configs** — use Glob (`*.config.{js,ts,mjs}`, `.eslintrc.*`, `.prettierrc*`) to discover, then read
5. **App config** — `.env*`, `config/*`, `src/config/*`
6. **API schemas** — `openapi.*`, `swagger.*`, `schema.graphql`, `*.proto`
7. **Deploy files** — `Dockerfile*`, `docker-compose.*`, `.github/workflows/*`, `k8s/*`, `helm/*`

Use the reference guide at `references/tech-detection-guide.md` to know what to look for in each file.

### Step 3: Analyze Tech Stack

Walk through each category and extract evidence from the files you read. Prefer explicit evidence (dependency names and versions in manifests, config file contents) over guessing.

**Categories to analyze:**

1. **Language & Runtime** — primary language, version constraints, runtime platform
2. **Framework** — main framework, version, notable plugins/starters
3. **Build & Package** — build tool, package manager
4. **Entry Points** — application entry files
5. **API Surface** (if backend) — style (REST/GraphQL/gRPC), docs tooling, ports, base paths, validation library
6. **Persistence** (if backend) — databases, ORM/data layer, migrations, caching
7. **Messaging** (if backend) — brokers, job schedulers
8. **Security** (if backend) — auth mechanism, security middleware
9. **Observability** (if backend) — logging, metrics, tracing, health checks
10. **Styling** (if frontend) — method, CSS framework, preprocessor
11. **Frontend Libraries** (if frontend) — state management, routing, forms, HTTP client
12. **Testing** — unit framework, E2E framework, integration tools, test utilities, coverage
13. **Code Quality** — linter, formatter, static analysis, type checking
14. **Deployment** — containerization, K8s, CI/CD
15. **Dev Experience** — scripts/commands, dev tools, git hooks, env variables

For categories that don't apply to the detected project type, set values to `null` or `[]`.

### Step 4: Analyze Project Structure

Examine the directory layout to identify:

- Entry points
- Layer organization (routes/controllers/services, components/pages/hooks, etc.)
- Test directory layout
- Deploy manifests location

Generate a tree representation limited to 2-3 levels of depth. Focus on `src/` and other meaningful directories — skip `node_modules`, `dist`, `.git`, `__pycache__`, `target`, `bin/Debug`.

### Step 5: Generate project-context.json

Write file to `.sdlc-cgen/project-context.json`. The file overwrites any existing `project-context.json`.

The output must conform to the schema in `assets/project-context-schema.json`. Key rules:

- Every field listed in the schema should be present in the output
- Use `null` for single-value fields where nothing was detected
- Use `[]` for array fields where nothing was detected
- Use `{}` for object fields (like `devCommands`) where nothing was detected
- Include version numbers wherever the manifest provides them
- For `keyProdDependencies` and `keyDevDependencies`, list the 5-15 most architecturally significant dependencies with their versions (format: `"library@version"`)
- `patterns` should contain short observations directly verifiable from a specific file or config you read. Include as many as are evidenced; zero is valid. Do not invent observations to fill a quota.

### Step 6: Generate code-convention.md

Generate `.sdlc-cgen/code-convention.md` from two sources:

**1. Tech stack-specific pitfalls**

Using the tech stack detected in Steps 1–5, include conventions for known gotchas that are:

- Specific to the technologies, versions, or combinations in use in this project
- Not automatically fixed by the project's tooling (linters, formatters, type checkers)
- Not common knowledge that any competent developer or AI agent would already apply

If a pitfall is generic or auto-enforced by the project's tooling, omit it.

**2. Recurring review issues**

Scan `.sdlc-cgen/sessions/*/code-review.json` for any existing review history. An issue pattern appearing in three or more independent reviews is a convention candidate — codify the prohibition.

**Format**

Each rule is a single bullet where **ALWAYS** or **NEVER** is an inline prefix — not a section heading. Rules may optionally be grouped under `##` topic headings; within each group, ALWAYS and NEVER bullets are mixed in logical order.

```markdown
## Topic

- **ALWAYS** do A.
- **ALWAYS** do B.
- **NEVER** do C.
```

Do not create a single monolithic **ALWAYS** block followed by a **NEVER** block. Do not include reviewer instructions, severity labels, or skip conditions. Omit a bullet entirely if there is no evidence for it.

Write to `.sdlc-cgen/code-convention.md`.

### Step 7: Confirm Completion

After writing both files, tell the user:

- File location (absolute path)
- Detected project type
- A brief summary: framework, language, key tools
- Any notable findings or patterns worth calling out
- **Next step:** **`/common-planning Implement {TASK DETAILS}`** — replace `{TASK DETAILS}` with the user story or task text; writes `plan.md` under `.sdlc-cgen/sessions/{session_id}/` using `project-context.json`.

## Resources

### references/tech-detection-guide.md

Comprehensive reference for identifying technologies from manifests and config files across all ecosystems (frontend and backend).

### assets/project-context-schema.json

JSON Schema defining the exact structure of the `project-context.json` output. Read this to understand all available fields and their types.
