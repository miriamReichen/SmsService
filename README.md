# SMS Service for Employees

A service that sends SMS messages to employees through an SMS provider. Built as a Full Stack home assignment.

**Stack:** ASP.NET Core 9 Web API, EF Core (Code First + Migrations), SQL Server, React + TypeScript (Vite), xUnit.

## What I built

- **API** (Controllers):
  - `GET /employees`: employees with their phones (read-only).
  - `POST /messages`: takes `{ phoneId, body }` and an optional `Idempotency-Key` header. Returns `202 { id, status: "Pending" }`.
  - `GET /messages/{id}`: message status, or `404`.
  - `GET /messages?status=`: newest first, up to 100, optional status filter.
- **Background sending:** a `BackgroundService` runs every 2 seconds. It sends `Pending` messages to the provider and polls the status of `Sent` ones.
- **Fake SMS provider:** a separate small server with `POST /send` and `GET /status/{id}`.
- **Database:** SQL Server via EF Core Code First. Migrations and seed data run automatically when the API starts.
- **Minimal React UI:** pick an employee and one of their phones, write a message, and watch the status table refresh every 2 seconds.
- **Swagger UI** at `/swagger`.
- **22 unit tests** (xUnit, Moq, EF Core InMemory).

## How to run

**Requirements:** Windows, .NET 9 SDK, Node.js 18+, and a SQL Server default instance on `localhost` with Windows Authentication.

From the repository root:

```
.\run.cmd           # everything: provider, API and UI. Opens the UI and Swagger
.\run.cmd server    # provider and API only. Opens Swagger
```

| Component | URL |
|---|---|
| UI | http://localhost:5173 |
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Fake provider | http://localhost:5100 |

No SQL script is needed. The `SmsService` database is created and seeded on the first run. To use a different SQL Server, change `ConnectionStrings:Default` in `server/src/SmsService.API/appsettings.json`. Press `Ctrl+C` to stop everything.

**Tests:** from `server/`, run `dotnet test`.

**Try in the UI:**
- Dana Cohen has two phones, so you pick one. The message reaches `Delivered`.
- Yossi Levi has one phone, which is selected automatically.
- Michal Avraham's phone ends with `0000`, so the message reaches `Failed`.
- Ron Israeli has no phone, so sending is blocked.

(The seed data and the UI are in Hebrew.)

## Project structure

```
server/
  src/SmsService.Domain/          Entities: Employee, Phone, Message, MessageStatus
  src/SmsService.DTO/             API contracts and error codes
  src/SmsService.Application/     Logic: MessageService, MessageDispatcher. Interfaces: IAppDbContext, ISmsProviderClient
  src/SmsService.Infrastructure/  EF Core, Migrations, seed, SmsProviderClient (HttpClient), MessageProcessor
  src/SmsService.API/             Controllers, error handling (ProblemDetails), Swagger
  src/SmsService.FakeProvider/    Fake SMS provider
  tests/SmsService.Tests/         Unit tests
client/                           React UI
.kiro/specs/sms-service/          Full SPEC: requirements, design, tasks, decisions
run.cmd, run.ps1                  One-command startup
```

## Key assumptions

1. **The "employee number" the system does not have is an employee ID, not a phone number.** There is no business employee ID. An employee is identified by first name, last name and address, which are unique together (unique index). Phone numbers are visible in the API.
2. **`to` is represented by `phoneId`.** The caller picks an employee from `GET /employees`, then one of their phones, and sends its `phoneId`. In the UI a single phone is selected automatically. `phoneId` is a technical database row id, not an "employee number". The provider receives only the phone number and the message body.
3. **The provider was not supplied, so I built a fake one with my own contract:**
   - `POST /send { to, body }` returns `202 { providerMessageId }`.
   - `GET /status/{id}` returns `{ status }`.
   - Deterministic behaviour: `Sent` right away, `Delivered` after 3 seconds. A number ending with `0000` always fails.
4. **Phone format:** Israeli mobile, `05` followed by 8 digits. Numbers are validated when seeding, and invalid numbers are not stored (the seed data includes `12345` to show this).
5. **Message length:** up to 160 characters. In Hebrew (UCS-2) the real single-SMS limit is 70. I chose 160 as a simple limit and note the gap.

## Main decisions

The full list, with reasons and rejected options, is in [`.kiro/specs/sms-service/decisions.md`](.kiro/specs/sms-service/decisions.md). The main ones:

| Topic | Decision | Why |
|---|---|---|
| Sending | `POST` stores `Pending` and returns 202. A background service sends | The request is fast and does not depend on the provider being available |
| Statuses | `Pending`, `Sent`, `Delivered`, `Failed` | Separates "provider accepted" from "delivered" |
| Tracking | Polling every 2 seconds (configurable) | Simple, the provider only needs a GET |
| Send failure | Up to 3 attempts with a growing delay, 5 second timeout, then `Failed` with `SEND_FAILED` | Resilience to transient errors |
| Stuck message | After 2 minutes without a final status: `Failed` with `DELIVERY_TIMEOUT` | No message is polled forever |
| Poll failure | Keep the status, log a warning, retry next cycle | No false `Failed` for a message that may have arrived |
| Double click | Optional `Idempotency-Key` with a filtered unique index. The same key returns the same message | Simple protection against client duplicates |
| Errors | `ProblemDetails` (RFC 7807) with a `code` field | Standard and easy to handle on the client |
| Structure | Layers: Domain, DTO, Application, Infrastructure, API | Logic is tested without HTTP or SQL. A second provider is another `ISmsProviderClient` |

**Implementation details I kept as they are:**
- A 404 returns `MESSAGE_NOT_FOUND`.
- When the provider reports `Failed` after sending, the reason is also `SEND_FAILED`. There is no separate `DELIVERY_FAILED`.
- A 4xx from the provider is not retried. Only network errors, timeouts and 5xx count as transient.
- With 3 attempts there are two delays in practice: 1 second and 2 seconds.
- The status filter is case-insensitive. Numeric values (e.g. `1`) are rejected.

## What I chose not to do, and why

- **Integration tests** against a real SQL Server, so the idempotency unique index is not tested automatically. I focused on unit tests of the critical logic because of the time limit. EF InMemory does not enforce SQL Server constraints.
- **Idempotency and seeder tests:** the code exists and was checked manually, but has no automated tests.
- **Duplicate prevention towards the provider:** a retry after a timeout may send a message twice, because the provider may have accepted it even though the response was lost. This is a known risk.
- **Authentication, employee CRUD and paging:** not required, or did not fit in the time.
- **Docker:** it was not installed on my development machine, so startup is a PowerShell script.

## Known limitations and risks

- Windows only, and requires SQL Server on `localhost`.
- `Database.Migrate()` on startup is not suitable for production with several instances.
- Polling every 2 seconds and a single background worker inside the API process will not handle large volumes.
- Retries run in memory. If the server crashes mid-send, the message stays `Pending` and is sent again on the next start, which may cause a duplicate.
- If polling keeps failing (provider down), messages end as `DELIVERY_TIMEOUT`, even though the problem is the poll and not the message.
- Without authentication, anyone who can reach the API sees all employees and phones.

## What I would add with more time

- Integration tests against SQL Server (e.g. Testcontainers or a test database), including idempotency and concurrency.
- An idempotency key towards the provider, to close the retry duplicate.
- A provider callback (webhook) instead of polling, or in addition to it.
- A separate `DELIVERY_FAILED` failure reason.
- Batch claiming with a lease, so several workers can run in parallel without sending twice.
- Paging for `GET /messages`.
- Health checks, metrics, and Docker Compose.

## Use of AI

I used Kiro, an AI-based development environment:
- **SPEC:** built in a conversation with the AI, in a spec-driven approach. The AI presented options with pros and cons, and I made every decision. The full SPEC is in [`.kiro/specs/sms-service/`](.kiro/specs/sms-service/).
- **Implementation:** the AI wrote the code, tests and startup script according to the SPEC, and ran the build, the tests and an end-to-end run.
- **Part B document:** written together with the AI, and submitted separately as a Word document.
- **Why:** to work in a structured way within a short time, and to document every decision.

## Time spent

About 4 hours.
