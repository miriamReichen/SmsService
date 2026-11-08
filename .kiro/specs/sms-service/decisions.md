# Decisions and Assumptions

Every decision was made by me during the spec work. The AI presented the options with their pros and cons, and I chose. Numbers follow the order in which decisions were made. Some decisions were later replaced, and this is noted in the table.

| # | Decision | Reason | Rejected |
|---|---|---|---|
| 1 | `to` is first name, last name and address | Close to the exam's wording | Replaced by 30 |
| 2 | Send to the first phone found | Simple | Replaced by 30 (the caller picks the phone) |
| 3 | Two tables: Employee and Phone | Clean unique index on the employee | One flat table |
| 4 | `GET /employees` endpoint, and server-side validation with 400 | Covers both the UI and direct API calls | List embedded in the UI |
| 5 | Exact name matching | Values come from the list | Cancelled (sending is by `phoneId`) |
| 6 | Our own fake provider, as a separate project | Control over failures, and a real "external" service for Part B | The exam's provider (not received), an internal endpoint |
| 7 | Statuses: `Pending`, `Sent`, `Delivered`, `Failed` | Separates "provider accepted" from "delivered" | 3 or 5 statuses |
| 8 | Status tracking by polling | Simple, the provider only needs a GET | Callback (webhook), both |
| 9 | Validate `phoneId`, `body`, and the phone format when seeding | Covers both readings of "phone validation" in the exam | |
| 10 | Israeli mobile format: `05` + 8 digits | Simple | E.164, both |
| 11 | Max 160 characters (the real Hebrew limit is 70) | Simple | 1000, 70 |
| 12 | Retry: 3 attempts with a growing delay | Resilient to transient errors | No retry, Polly |
| 13 | Provider call timeout: 5 seconds | Reasonable for a local provider | 10 seconds |
| 14 | Background sending, `POST` returns 202 | `POST` does not depend on the provider | Sending inside the request |
| 15 | `Idempotency-Key` header with a unique index | Solves double clicks | Design only, key also towards the provider |
| 16 | PowerShell script, LocalDB, Windows Authentication | Docker was not installed | Docker Compose. Database changed in 57 |
| 17 | `Database.Migrate()` on API startup | One command, no manual step | Separate `dotnet ef database update` |
| 18 | Seed in code on startup | Allows phone validation | `HasData`, SQL script |
| 19 | Phones ordered by Id | Deterministic | `Priority`, `IsPrimary`. Now display order only |
| 20 | Employee without phones: 400 with a clear code | Immediate feedback | Replaced by 32 |
| 21 | Tests cover: phone handling, statuses and retry, validation | The core logic | Idempotency |
| 22 | Unit tests only | Fast | Integration tests |
| 23 | xUnit | Most common in .NET | NUnit, MSTest |
| 24 | UI as the last task, only if time allows | Protects the core | Replaced by 52 |
| 25 | Hebrew while working, English for submission | No syncing during the work | Two copies in parallel |
| 26 | Poll every 2 seconds, in `appsettings` | The demo shows changes almost immediately | 5 or 10 seconds, per-message backoff |
| 27 | Per-message delivery timeout with `DELIVERY_TIMEOUT` | No stuck messages | No timeout, max poll count |
| 28 | Timeout value: 2 minutes | The demo does not wait long | 5 minutes, 30 seconds |
| 29 | Poll failure: ignore and retry next cycle | Simple, no false `Failed` | Failure counter, immediate retry |
| 30 | `POST /messages` takes `phoneId` and `body` | No string matching, short request | Name and address in every request |
| 31 | `GET /employees` returns phones with full numbers, in one call | Simple | Separate phones endpoint |
| 32 | Employee without phones is listed with an empty list | The user sees that the employee exists but has no phone | Hide them |
| 33 | In the UI, a single phone is selected automatically | Fewer clicks | Always manual |
| 34 | Assumption: the "employee number" the exam rules out is an employee ID, not a phone number | My reading of the exam | Phones must never be exposed |
| 35 | Three projects in one solution | Simple | Replaced by 51 |
| 36 | .NET 9 | Matches the installed SDK | .NET 8 |
| 37 | Controllers | Familiar, built-in validation | Minimal APIs |
| 38 | Message stores `PhoneId` only | No deletes in scope (no CRUD) | Snapshot of the number |
| 39 | Message responses include the employee and the phone number | Clear where each message went | |
| 40 | Errors as `ProblemDetails` with a `code` field | Standard in .NET | Custom error shape |
| 41 | Codes: `VALIDATION_ERROR`, `PHONE_NOT_FOUND`, `INVALID_STATUS_FILTER`. Reasons: `SEND_FAILED`, `DELIVERY_TIMEOUT` | Covers every defined case | |
| 42 | 404 for an unknown message id | Standard | 400 |
| 43 | 400 for an invalid status filter | Clear feedback | Empty list |
| 44 | `Idempotency-Key` is optional | Does not break simple callers | Required |
| 45 | Same key with a different body: return the existing message | Simple | 409 Conflict with a request hash |
| 46 | `GET /messages`: newest first, no paging, max 100 | Simple | Paging |
| 47 | Fake provider is deterministic: `Delivered` after 3 seconds, `...0000` always `Failed` | Predictable demo | Random, configurable |
| 48 | No seeder tests | Time | |
| 49 | Part B outside the AI's scope | My instruction at the time | Replaced by 60 |
| 50 | No UI | Time | Replaced by 52 |
| 51 | Layered structure: Domain, DTO, Application, Infrastructure, API, Tests under `server/`. Fake provider as a separate project | Clean separation, easy to explain | 3 projects, a middle ground |
| 52 | Minimal React UI (form and table, no styling) under `client/` | Shows the whole flow | No UI |
| 53 | Accept going over 4 hours if needed, and report the real time | Layers and UI were added | Cutting features |
| 54 | Test libraries: xUnit + Moq | Fewer dependencies | FluentAssertions (v8 is paid) |
| 55 | Vite pinned to 5.x | Works with the installed Node 21.5.0 | Upgrading Node |
| 56 | Name prefix: `SmsService` | | |
| 57 | Full SQL Server (`localhost`, default instance `MSSQLSERVER`, Windows Authentication) instead of LocalDB | The database appears in the server I work with in SSMS | LocalDB, LocalDB with an override |
| 58 | Swagger UI at `/swagger` (`Swashbuckle.AspNetCore.SwaggerUI` 10.3.1 only, on top of the built-in .NET 9 OpenAPI), Development only | Familiar, lets me test the server without the UI | Scalar, `.http` file, nothing |
| 59 | `run.cmd` starts everything and opens the UI and Swagger. `run.cmd server` starts provider and API only | The same command for both modes | Manual `dotnet` commands |
| 60 | Part B written together with the AI | My request | Writing it alone |
| 61 | Part B is submitted as a Word document, in Hebrew | My request | Markdown in the repo, a README section |
| 62 | Part B length: about one and a half pages | The exam prefers one clear argument | Two full pages |
| 63 | Part B, second provider: primary and backup (failover) | Simple, availability first | Rule-based routing by price, percentage split |
| 64 | Part B, provider outage: a circuit breaker per provider | Does not waste attempts on a provider known to be down | Switching only after all retries |
| 65 | Part B, load: the first bottleneck is serial sending in a single worker (`SendPendingAsync`) | The first and most measurable limit | Polling, intake |
| 66 | Part B, order of changes: batch claiming with a lease, parallel sending with rate limits, campaign endpoint, callback, and only then a queue and a separate worker | Fix what breaks first | A dedicated queue first |
| 67 | Part B, crash mid-send: send `Message.Id` to the provider as an idempotency key. Without provider support: at-least-once | The id already exists, and a resend becomes safe | Accept duplicates |
| 68 | Part B, double click: describe the existing protection and the gap, and make the key required for campaigns | Mostly implemented already | Description only |
| 69 | Part B, success without delivery: track the delivery rate per provider, with an alert | Connects to monitoring | Canary numbers, both |
| 70 | Part B, monitoring: everything, in priority order. (1) Business metrics with alerts, (2) health checks and structured logs, (3) tracing, dashboards and SLOs | Clear order instead of a list of technologies | One option only |
| 71 | Part B, what I would not do now: no queue before batch claiming and parallelism, no price routing, no microservices or Kubernetes | | "Exactly once", skipping the callback |

## Implementation details left as they are

These came up during implementation, and I chose to keep them as implemented:
- A 404 returns the code `MESSAGE_NOT_FOUND`.
- A `Failed` reported by the provider after sending also uses the reason `SEND_FAILED`. There is no separate `DELIVERY_FAILED` (listed under future work).
- A 4xx from the provider is not retried. Only network errors, timeouts and 5xx count as transient.
- With 3 attempts there are two delays in practice: 1 second and 2 seconds.
- The status filter is case-insensitive. Numeric values (e.g. `1`) are rejected.
