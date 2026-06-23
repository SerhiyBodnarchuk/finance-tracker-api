# Tech Stack Detection Guide

Identifies technologies by analyzing build manifests, dependency locks, and configuration files. Covers both frontend and backend ecosystems.

---

## 1. Language & Runtime Detection

### JavaScript / TypeScript (Node.js)

**Manifests:** `package.json`

- **Node version:** `engines.node` in `package.json`, `.nvmrc`, `.node-version`
- **TypeScript:** `typescript` in devDependencies, `tsconfig.json`
- **Lock files:** `package-lock.json` (npm), `yarn.lock`, `pnpm-lock.yaml`, `bun.lockb`

### Java / Kotlin

**Manifests:** `pom.xml`, `build.gradle`, `build.gradle.kts`

- **JDK target:** `maven-compiler-plugin`, `java { toolchain { languageVersion = ... } }`, `sourceCompatibility`
- **Config:** `src/main/resources/application.yml|properties`

### .NET (C# / F#)

**Manifests:** `*.csproj`, `*.sln`, `global.json`

- **TFM:** `<TargetFramework>netX.Y</TargetFramework>`
- **SDK version:** `global.json`
- **Config:** `appsettings.json`, `appsettings.*.json`

### Python

**Manifests:** `pyproject.toml`, `requirements.txt`, `Pipfile`

- **Version:** `requires-python` in `pyproject.toml`, `python_version` in `Pipfile`
- **Managers:** Poetry (`poetry.lock`), Pipenv (`Pipfile.lock`), uv (`uv.lock`), PDM (`pdm.lock`)

### Go

**Manifests:** `go.mod`, `go.sum`

- **Version:** `go X.Y` in `go.mod`

### Ruby

**Manifests:** `Gemfile`, `Gemfile.lock`

- **Version:** `.ruby-version` or `ruby "X.Y.Z"` in Gemfile

### PHP

**Manifests:** `composer.json`, `composer.lock`

- **Version:** `require.php` in `composer.json`

---

## 2. Framework Detection

### Frontend Frameworks

**React**

- **Deps:** `react`, `react-dom`
- **Indicators:** `.jsx`/`.tsx` files, `@vitejs/plugin-react`

**Vue.js**

- **Deps:** `vue`
- **Configs:** `vue.config.js`, `@vitejs/plugin-vue`
- **Indicators:** `.vue` files

**Angular**

- **Deps:** `@angular/core`, `@angular/common`
- **Configs:** `angular.json`

**Next.js**

- **Deps:** `next`
- **Configs:** `next.config.js`, `next.config.mjs`
- **Indicators:** `pages/` or `app/` directory

**Svelte**

- **Deps:** `svelte`
- **Configs:** `svelte.config.js`, `@sveltejs/vite-plugin-svelte`

### Backend Frameworks

**Express** — deps: `express`; indicators: `app.get(...)`, `app.use(...)`
**NestJS** — deps: `@nestjs/core`, `@nestjs/common`; indicators: `@Module`, `NestFactory.create`
**Fastify** — deps: `fastify`; indicators: `fastify.register(...)`
**Spring Boot** — deps: `spring-boot-starter-*`; indicators: `@SpringBootApplication`
**Quarkus** — deps: `io.quarkus:*`; config: `quarkus.*`
**Micronaut** — deps: `io.micronaut:*`; indicators: `@Controller`
**ASP.NET Core** — deps: `Microsoft.AspNetCore.*`; indicators: `WebApplication.CreateBuilder`
**Django** — deps: `Django`; indicators: `manage.py`, `settings.py`, `urls.py`
**FastAPI** — deps: `fastapi`, `uvicorn`; indicators: `FastAPI()`, `@app.get(...)`
**Flask** — deps: `flask`; indicators: `Flask(__name__)`
**Gin** — deps: `github.com/gin-gonic/gin`
**Echo** — deps: `github.com/labstack/echo/v4`
**Fiber** — deps: `github.com/gofiber/fiber/v2`
**Rails** — deps: `rails`; indicators: `config/routes.rb`, `app/controllers`
**Laravel** — deps: `laravel/framework`; indicators: `artisan`, `routes/api.php`
**Symfony** — deps: `symfony/framework-bundle`; indicators: `bin/console`

---

## 3. Build Tools

### Frontend

- **Vite** — deps: `vite`; config: `vite.config.js|ts`
- **Webpack** — deps: `webpack`, `webpack-cli`; config: `webpack.config.js`
- **Parcel** — deps: `parcel`; config: `.parcelrc`
- **Rollup** — deps: `rollup`; config: `rollup.config.js`

### Backend

- **Maven** — file: `pom.xml`
- **Gradle** — files: `build.gradle`, `build.gradle.kts`, `settings.gradle*`
- **dotnet CLI / MSBuild** — files: `*.csproj`, `*.sln`
- **go build** — file: `go.mod`
- **pip / poetry / uv** — files: `pyproject.toml`, `requirements.txt`

---

## 4. API Style & Schemas

### REST (OpenAPI/Swagger)

- **Files:** `openapi.yaml|yml|json`, `swagger.yaml|json`
- Java: `springdoc-openapi*`, `io.swagger*`
- .NET: `Swashbuckle.AspNetCore`, `NSwag.AspNetCore`
- Node: `swagger-ui-express`, `@nestjs/swagger`
- Python: FastAPI auto docs, `drf-yasg`

### GraphQL

- **Files:** `schema.graphql`, `*.graphql`
- Node: `apollo-server`, `@nestjs/graphql`, `graphql`
- Java: `graphql-java`, `spring-graphql`
- Python: `graphene`, `strawberry-graphql`
- .NET: `HotChocolate`, `GraphQL.NET`

### gRPC

- **Files:** `*.proto`
- Java: `io.grpc:*`
- Go: `google.golang.org/grpc`
- Node: `@grpc/grpc-js`
- Python: `grpcio`

---

## 5. Persistence & Migrations

### Database Drivers

- **PostgreSQL:** `org.postgresql:postgresql`, `Npgsql`, `pg`, `psycopg`, `pgx`
- **MySQL:** `mysql:mysql-connector-java`, `MySqlConnector`, `mysql2`, `PyMySQL`
- **MS SQL:** `mssql-jdbc`, `Microsoft.Data.SqlClient`, `mssql`, `pyodbc`
- **SQLite:** `sqlite-jdbc`, `Microsoft.Data.Sqlite`, `sqlite3`, `better-sqlite3`
- **MongoDB:** `mongoose`, `mongodb`, `MongoClient`

### ORMs / Data Layers

- **Java:** JPA/Hibernate (`hibernate-core`, `spring-boot-starter-data-jpa`)
- **.NET:** EF Core (`Microsoft.EntityFrameworkCore.*`)
- **Node:** Prisma, TypeORM, Sequelize, Drizzle (`drizzle-orm`), Mongoose
- **Python:** SQLAlchemy, Django ORM, Tortoise ORM
- **Go:** GORM (`gorm.io/gorm`)

### Migrations

- Java: Flyway, Liquibase
- .NET: EF migrations
- Node: Prisma Migrate, Knex, Drizzle Kit (`drizzle-kit`)
- Python: Alembic, Django migrations
- Go: golang-migrate

---

## 6. Messaging & Async

### Brokers

- **Kafka:** `kafkajs`, `confluent-kafka`, `org.apache.kafka`
- **RabbitMQ:** `amqplib`, `com.rabbitmq`, `RabbitMQ.Client`
- **SQS:** `@aws-sdk/client-sqs`, AWS SDKs
- **NATS:** `nats`, `nats.go`

### Job Processing

- Node: BullMQ, Agenda
- Java: Quartz, Spring `@Scheduled`
- .NET: Hangfire, Quartz.NET
- Python: Celery, RQ, APScheduler

---

## 7. Security

### Auth

- **JWT:** `jsonwebtoken`, `python-jose`, `PyJWT`, `golang-jwt/jwt`
- **Passport.js:** `passport`, `passport-jwt`, `passport-local`
- **Spring Security:** `spring-boot-starter-security`, `spring-security-oauth2-*`
- **.NET Identity:** `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Identity.Web`
- **Auth0:** `@auth0/*`

### Security middleware

- Node: `helmet`, `cors`, `csurf`, `express-rate-limit`
- Secrets: `.env*`, Vault/KMS client libs

---

## 8. Observability

### Logging

- Node: Winston, Pino
- Java: Logback, Log4j2, SLF4J
- .NET: Serilog, NLog
- Python: structlog, loguru
- Go: zap, logrus

### Metrics / Tracing

- **OpenTelemetry:** `opentelemetry-*` (all ecosystems)
- **Prometheus:** `prom-client`, Micrometer, `prometheus_client`
- **Tracing:** Jaeger/Zipkin exporters

### Health Checks

- Spring Boot Actuator, ASP.NET HealthChecks, custom `/health` endpoints

---

## 9. Styling (Frontend)

### CSS Frameworks

- **Tailwind CSS:** dep `tailwindcss`; config `tailwind.config.js|ts`; indicators: `@tailwind` directives
- **Bootstrap:** deps `bootstrap`, `react-bootstrap`
- **MUI:** deps `@mui/material`, `@mui/icons-material`
- **Ant Design:** dep `antd`
- **Chakra UI:** dep `@chakra-ui/react`

### CSS Preprocessors

- **Sass/SCSS:** dep `sass`; indicators: `.scss` files
- **Less:** dep `less`
- **PostCSS:** dep `postcss`; config `postcss.config.js`

### CSS-in-JS

- **styled-components:** dep `styled-components`
- **Emotion:** deps `@emotion/react`, `@emotion/styled`

---

## 10. Frontend Libraries

### State Management

- Redux / RTK: `@reduxjs/toolkit`, `react-redux`
- Zustand: `zustand`
- MobX: `mobx`, `mobx-react-lite`
- Jotai: `jotai`
- Pinia (Vue): `pinia`
- Vuex (Vue): `vuex`

### Routing

- React Router: `react-router-dom`
- Vue Router: `vue-router`
- TanStack Router: `@tanstack/react-router`

### Form Handling

- React Hook Form: `react-hook-form`
- Formik: `formik`
- Validation: `zod`, `yup`

### HTTP / Data Fetching

- Axios: `axios`
- TanStack Query: `@tanstack/react-query`
- SWR: `swr`
- Apollo Client: `@apollo/client`

---

## 11. Testing

### Unit

- **Vitest:** dep `vitest`; config `vitest.config.*`
- **Jest:** dep `jest`; config `jest.config.*`
- **JUnit 5:** `org.junit.jupiter`
- **pytest:** dep `pytest`
- **Go:** `go test`, Testify

### E2E

- **Playwright:** dep `@playwright/test`; config `playwright.config.*`
- **Cypress:** dep `cypress`; config `cypress.config.*`

### Integration

- Supertest, Testcontainers, WireMock, WebApplicationFactory

### Test Utilities

- Testing Library (`@testing-library/*`), jsdom, happy-dom, Faker

### Coverage

- v8, istanbul/nyc, JaCoCo, coverage.py

---

## 12. Code Quality

### Linters

- **ESLint:** dep `eslint`; config `.eslintrc.*`, `eslint.config.*`
- **Stylelint:** dep `stylelint`
- **Ruff:** dep `ruff`
- **golangci-lint:** config `.golangci.yml`
- **Checkstyle / SpotBugs / PMD** (Java)

### Formatters

- **Prettier:** dep `prettier`; config `.prettierrc*`
- **Black:** dep `black`
- **gofmt** (Go default)
- **dotnet format** (.NET)

### Static Analysis

- SonarQube, mypy, bandit, .NET analyzers

---

## 13. Deployment & Infrastructure

### Containers

- `Dockerfile*`, `docker-compose.yml|yaml`, `.dockerignore`

### Kubernetes

- `k8s/*`, `manifests/*`, `helm/*`, `charts/*`

### CI/CD

- GitHub Actions: `.github/workflows/*`
- GitLab CI: `.gitlab-ci.yml`
- Azure Pipelines: `azure-pipelines.yml`
- Jenkins: `Jenkinsfile`

### Serverless / IaC

- `serverless.yml`, `terraform/*.tf`, `pulumi*`

---

## 14. Configuration Files Quick Reference

**Always read first:**

1. Build manifests (`package.json`, `pom.xml`, `build.gradle*`, `*.csproj`, `pyproject.toml`, `go.mod`, `Gemfile`, `composer.json`)
2. Lock files (to detect package manager)

**Then read available:** 3. App config (`tsconfig.json`, `application.yml`, `appsettings.json`, `.env*`, `config/*`) 4. Build config (`vite.config.*`, `webpack.config.*`, `next.config.*`) 5. Lint/format configs (`.eslintrc*`, `eslint.config.*`, `.prettierrc*`, `tailwind.config.*`) 6. Test configs (`vitest.config.*`, `jest.config.*`, `playwright.config.*`) 7. API schemas (`openapi.*`, `schema.graphql`, `*.proto`) 8. Deploy files (`Dockerfile*`, `docker-compose.*`, `.github/workflows/*`, `k8s/*`)

**Use Glob to discover:** `*.config.js`, `*.config.ts`, `*.config.mjs`, `.eslintrc.*`, `.prettierrc*`
