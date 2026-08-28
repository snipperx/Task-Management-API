# Task Management API

A team task-management REST API built with **ASP.NET Core (.NET 10)**, **EF Core**, **PostgreSQL**,
**JWT auth** and **role/permission-based authorization**.

Users register and log in, Managers/Admins create projects and assign work, Developers move their
tasks through a controlled workflow, and everyone can comment. Statistics endpoints summarise
progress per project and across the board.

---

## Contents

- [Architecture](#architecture)
- [Quick start (Docker)](#quick-start-docker)
- [Local development](#local-development)
- [Configuration](#configuration)
- [Authentication & roles](#authentication--roles)
- [API reference](#api-reference)
- [Trying the API (Bruno / .http)](#trying-the-api-bruno--http)
- [Business rules](#business-rules)
- [Testing](#testing)
- [Seed data / test credentials](#seed-data--test-credentials)
- [Deployment notes](#deployment-notes)

---

## Architecture

```
src/TaskManagementAPI/
├── Controllers/        thin HTTP layer, one per resource
├── Services/           business logic + rules (Auth, User, Project, Task, Comment)
│                       + OverdueEscalationService (BackgroundService)
├── Repositories/       generic IRepository<T> / Repository<T> + UnitOfWork
├── Domain/             entities, enums, TaskWorkflow state-machine
├── Data/               AppDbContext, EF migrations, DataSeeder
├── Contracts/          request/response DTOs + mapping extensions
├── Security/           JWT TokenService, BCrypt hasher, permissions, policy provider
├── Middleware/         GlobalExceptionMiddleware  (RFC-style ErrorResponse)
├── Extensions/         DI composition (AddPersistence / AddApplicationServices / AddJwtAuth / AddSwagger)
└── Program.cs          pipeline wiring

tests/TaskManagementAPI.Tests/
├── Unit/               xUnit + Moq — services & workflow rules
└── Integration/        WebApplicationFactory + EF InMemory — controller/auth flows

bruno/                  Bruno API collection (every endpoint, Local + Docker environments)
http/                   .http request files for VS Code REST Client / Rider / VS 2022
```

Layering: `Controller → Service → Repository/DbContext`. Controllers never touch `DbContext`
directly; services own transactions via `IUnitOfWork`.

---

## Quick start (Docker)

```bash
cp .env.example .env
# edit .env and set a real JWT_SECRET (>= 32 chars), e.g.:
#   JWT_SECRET=$(openssl rand -base64 48)

docker compose up --build
```

- API:      http://localhost:5000
- Swagger:  http://localhost:5000/swagger
- Health:   http://localhost:5000/health
- pgAdmin (optional): `docker compose --profile tools up pgadmin` → http://localhost:5050

The database schema is created via EF migrations and seeded automatically on first start.

---

## Local development

Requirements: .NET 10 SDK, a PostgreSQL instance (or `docker run` one).

```bash
# 1. start a database
docker run -d --name tm-pg -e POSTGRES_PASSWORD=postgres123 -e POSTGRES_DB=TaskManagement \
  -p 5432:5432 postgres:16-alpine

# 2. run the API (Development env has a dev JWT secret baked in)
dotnet run --project src/TaskManagementAPI

# Swagger: https://localhost:xxxx/swagger  (port shown in console)
```

### EF migrations

```bash
dotnet tool install --global dotnet-ef      # once

# add a migration
dotnet ef migrations add <Name> \
  --project src/TaskManagementAPI --output-dir Data/Migrations

# apply manually (otherwise applied on startup)
dotnet ef database update --project src/TaskManagementAPI
```

---

## Configuration

| Key | Env var | Default | Notes |
|-----|---------|---------|-------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | localhost pg | required |
| `JwtSettings:Secret` | `JwtSettings__Secret` | – | **required, ≥ 32 bytes**, validated on startup |
| `JwtSettings:AccessTokenExpirationMinutes` | `JwtSettings__AccessTokenExpirationMinutes` | 15 | |
| `JwtSettings:RefreshTokenExpirationDays` | `JwtSettings__RefreshTokenExpirationDays` | 7 | |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0` … | `[]` | empty ⇒ allow-any (dev) |
| `SeedOnStartup` | `SeedOnStartup` | `true` | migrate + seed on boot |
| `Swagger:Enabled` | `Swagger__Enabled` | dev only | force-enable in other envs |

---

## Authentication & roles

1. `POST /api/auth/register` or `/login` → `{ accessToken, refreshToken, accessTokenExpiresAt, user }`
2. Send `Authorization: Bearer <accessToken>` on every protected call.
3. When the access token expires → `POST /api/auth/refresh` with the old access token + refresh token.

The access token carries `role` and one `permissions` claim per granted permission. Authorization is
**permission-based**: endpoints require e.g. `tasks:edit`, and roles map to permission sets.

| Permission | Admin | Manager | Developer | Viewer |
|---|:---:|:---:|:---:|:---:|
| tasks:view | ✓ | ✓ | ✓ | ✓ |
| tasks:create | ✓ | ✓ | ✓ | – |
| tasks:edit | ✓ | ✓ | ✓¹ | – |
| tasks:status-update | ✓ | ✓ | ✓¹ | – |
| tasks:delete | ✓ | ✓ | – | – |
| tasks:assign | ✓ | ✓ | – | – |
| projects:view | ✓ | ✓ | ✓ | ✓ |
| projects:create / edit / delete | ✓ | ✓ | – | – |
| users:view | ✓ | ✓ | – | – |
| users:manage | ✓ | – | – | – |
| comments:create / edit / delete | ✓ | ✓ | ✓² | – |
| reports:view | ✓ | ✓ | ✓ | ✓ |
| reports:generate | ✓ | ✓ | – | – |

¹ Developers may only edit/transition tasks **assigned to them** (enforced in `TaskService`).
² Users may only edit/delete **their own** comments (Managers/Admins may moderate any).

Self-registration always creates a **Developer**. Roles are changed via `POST /api/users/{id}/roles`
(Admin only). Changing a role or password invalidates the user's refresh token.

---

## API reference

### Auth
| Method | Route | Auth |
|---|---|---|
| POST | `/api/auth/register` | anonymous |
| POST | `/api/auth/login` | anonymous |
| POST | `/api/auth/refresh` | anonymous (token pair) |
| POST | `/api/auth/logout` | bearer |
| POST | `/api/auth/change-password` | bearer |

### Users
| Method | Route | Permission |
|---|---|---|
| GET | `/api/users?pageNumber=&pageSize=` | users:view |
| GET | `/api/users/{id}` | users:view |
| PUT | `/api/users/{id}` | users:manage |
| DELETE | `/api/users/{id}` | users:manage (soft-deactivate) |
| POST | `/api/users/{id}/roles` | users:manage |

### Projects
| Method | Route | Permission |
|---|---|---|
| GET | `/api/projects?status=&search=&pageNumber=&pageSize=` | projects:view |
| GET | `/api/projects/{id}` | projects:view |
| POST | `/api/projects` | projects:create |
| PUT | `/api/projects/{id}` | projects:edit |
| DELETE | `/api/projects/{id}` | projects:delete |
| GET | `/api/projects/{id}/tasks` | tasks:view |
| GET | `/api/projects/{id}/statistics` | reports:view |

### Tasks
| Method | Route | Permission |
|---|---|---|
| GET | `/api/tasks?status=&priority=&projectId=&assigneeId=&isOverdue=&search=&sort=&pageNumber=&pageSize=` | tasks:view |
| GET | `/api/tasks/{id}` | tasks:view |
| POST | `/api/tasks` | tasks:create |
| PUT | `/api/tasks/{id}` | tasks:edit |
| DELETE | `/api/tasks/{id}` | tasks:delete (soft delete) |
| PATCH | `/api/tasks/{id}/status` | tasks:status-update |
| PATCH | `/api/tasks/{id}/assign` | tasks:assign |
| PATCH | `/api/tasks/{id}/priority` | tasks:edit |
| GET | `/api/tasks/overdue` | tasks:view |
| GET | `/api/tasks/statistics?projectId=` | reports:view |

`sort`: `createdAt` (default, desc), `dueDate`, `priority`, `title`, `status`; prefix `-` for descending.

### Comments
| Method | Route | Permission |
|---|---|---|
| GET | `/api/tasks/{taskId}/comments` | tasks:view |
| POST | `/api/tasks/{taskId}/comments` | comments:create |
| PUT | `/api/comments/{id}` | comments:edit (own only) |
| DELETE | `/api/comments/{id}` | comments:delete (own only) |

### Error shape

All handled errors return:

```json
{
  "correlationId": "0HN...",
  "timestamp": "2026-08-27T10:00:00Z",
  "status": 409,
  "message": "Cannot move a task from ToDo to Done. Allowed flow: ToDo → InProgress → InReview → Done.",
  "errors": { "Field": ["message"] }
}
```

`400` validation · `401` bad credentials/token · `403` not permitted · `404` missing · `409` business-rule violation · `500` unexpected (details only in Development).

---

## Trying the API (Bruno / .http)

Besides Swagger (`/swagger`), the repo ships two ready-to-run request collections. Both default to
the **Local** environment (`http://localhost:5252`) and carry a **Docker** environment
(`http://localhost:5000`); both are pre-filled with the seed logins.

- **`bruno/`** — a [Bruno](https://www.usebruno.com/) collection covering every endpoint. Open the
  folder in Bruno, pick an environment, run **Auth / Login** once (a post-response script stores the
  token) and every other request inherits it.
- **`http/`** — `.http` files (`auth`, `projects`, `tasks`, `comments`, `users`) for the **VS Code
  REST Client**, **JetBrains Rider/IntelliJ**, and **Visual Studio 2022** HTTP clients. Select the
  `local` env, run the `# @name login` request in a file, then send the rest. See
  [`http/README.md`](http/README.md).

---

## Business rules

- **Projects** — create/edit/delete require Manager+. Cannot delete a project that still has
  non-Done tasks. Archived projects are immutable. Setting status → Completed stamps `completedAt`.
- **Tasks**
  - Workflow: `ToDo → InProgress → InReview → Done`; single-step moves back are allowed; skipping
    a state (e.g. ToDo → Done) is rejected (`409`).
  - Developers/Viewers may only modify tasks assigned to them.
  - A user may hold at most **10** tasks `InProgress` at once.
  - Due date may not be in the past at create time.
  - New tasks may only be added to **Active** projects.
  - `OverdueEscalationService` bumps the priority of overdue, not-Done tasks one level per day
    (capped at Critical).
- **Comments** — authors edit/delete their own; Managers/Admins may moderate any.

---

## Testing

```bash
dotnet test

# with coverage (runsettings excludes generated EF migrations)
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
# HTML report (needs: dotnet tool install -g dotnet-reportgenerator-globaltool)
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coveragereport
```

78 tests, ~90% line coverage (services 90–100%, controllers 95–100%).

- **Unit** (`tests/.../Unit`) — `TaskService` / `ProjectService` / `AuthService` / `UserService` /
  `CommentService` rules and the `OverdueTaskEscalator`, each against an isolated EF-InMemory context;
  `TaskWorkflow` transition matrix (`[Theory]`).
- **Integration** (`tests/.../Integration`) — boots the app via `WebApplicationFactory` against an
  EF-InMemory database and exercises the auth lifecycle (register / login / refresh / logout /
  change-password), RBAC failures, the full task workflow, and the users/projects/comments endpoints.
- **Real-database integration** (`Integration/RealDatabaseApiTests`) — the same style against a
  throw-away **PostgreSQL container** ([Testcontainers](https://dotnet.testcontainers.org/)), so real
  Npgsql, real EF migrations and the real seeder run. [Respawn](https://github.com/jbogard/Respawn)
  truncates + reseeds between tests; [Bogus](https://github.com/bchavez/Bogus) generates payloads.
  **Needs Docker.**
- **Error-contract snapshots** (`Integration/ErrorContractTests`) — [Verify](https://github.com/VerifyTests/Verify)
  pins the shape of the shared `ErrorResponse` body for 400/404/409. Update snapshots by reviewing
  the `*.received.txt` next to each `*.verified.txt`.

### Load & smoke (k6)

[`load/`](load/README.md) holds [k6](https://k6.io/) scripts that drive the running API:

```bash
k6 run load/smoke.js                                  # 1 VU critical-path check (CI gate)
k6 run -e VUS=50 load/load.js                         # ramping load with p95 latency thresholds
k6 run -e BASE_URL=http://localhost:5000 load/smoke.js  # against docker compose
```

CI runs `smoke.js` against the built image in the `docker` job.

---

## Seed data / test credentials

Created automatically on first run (`SeedOnStartup=true`):

| Email | Password | Role |
|---|---|---|
| admin@company.com | `Admin@123` | Admin |
| manager@company.com | `Manager@123` | Manager |
| dev1@company.com | `Dev@123` | Developer |
| dev2@company.com | `Dev@123` | Developer |
| viewer@company.com | `Viewer@123` | Viewer |

Plus 3 projects (one Completed) and 5 tasks spanning every status/priority, including an overdue one.

---

## Deployment notes

- Multi-stage `Dockerfile` (SDK build → `aspnet` runtime), runs as non-root, `curl` health probe
  on `/health`, listens on `:8080`.
- Provide `JwtSettings__Secret` and `ConnectionStrings__DefaultConnection` as environment
  variables / secrets (never commit them). Startup fails fast if the secret is missing or weak.
- Behind TLS termination the app enables HTTPS redirection automatically outside Development.
- `.github/workflows/ci.yml` builds, tests (with a Postgres service), collects coverage, and
  builds the Docker image on every push/PR.

---

<sub>README rendering verified against GitHub-flavored Markdown (headings, TOC anchors, tables and
fenced code blocks) on 2026-08-27.</sub>
