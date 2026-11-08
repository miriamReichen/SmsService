# Tasks

The original plan had three projects and a 4-hour budget (240 minutes). It was updated to a layered structure under `server/` and a React UI under `client/` (decisions 51 to 56).

| # | Task | Estimate | Status | Demo |
|---|---|---|---|---|
| 1 | Solution skeleton (Domain, DTO, Application, Infrastructure, API, FakeProvider, Tests), model and migration | 30 min | Done | Tables are created in SQL Server |
| 2 | Seed and `GET /employees` | 20 min | Done | 5 employees. The invalid phone `12345` is skipped |
| 3 | Fake provider | 20 min | Done | `Sent`, then `Delivered`. `...0000` fails |
| 4 | `POST /messages` (validation, ProblemDetails with `code`, `Idempotency-Key`) | 35 min | Done | 202 `Pending`. The same key returns the same message |
| 5 | `GET /messages/{id}` and `GET /messages?status=` | 15 min | Done | 404, filtering, `INVALID_STATUS_FILTER` |
| 6 | Background sending with retry | 45 min | Done | `Pending` becomes `Sent` on its own |
| 7 | Polling and timeout | 25 min | Done | `Delivered` / `Failed` |
| 8 | One command (`run.cmd` / `run.ps1`) | 15 min | Done | Provider, API and UI start together |
| 8a | Minimal React UI (TypeScript, Vite 5) | 45 min | Done | Employee and phone selection form, messages table |
| 8b | Swagger UI and `run.cmd server` mode | 10 min | Done | `/swagger` |
| 9 | README | 20 min | Done | |
| 10 | Translate the SPEC and README to English | 15 min | Done | |

**Tests:** 22 unit tests (xUnit + Moq + EF InMemory), all passing.

**Out of scope:** seeder, idempotency and integration tests. Part B is a separate Word document.
