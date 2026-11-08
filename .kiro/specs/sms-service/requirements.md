# Requirements: SMS Service for Employees

## 1. Introduction

A service that sends SMS messages to employees. The system has no external employee identifier ("employee number"). An employee is identified by first name, last name and address. The caller picks an employee and one of their phones from a list the system provides. Sending is done through a fake SMS provider built inside this repository.

**Glossary**
- **Employee:** first name, last name and address. The three are unique together.
- **Phone (instance):** a row with a phone number and an internal `phoneId`. Belongs to one employee. The number itself is not unique.
- **Message:** a send request with a status.
- **Provider:** the (fake) SMS provider.

Requirements are written in EARS style ("WHEN ... THE SYSTEM SHALL ...").

## 2. Requirements

### R1: Create a message (`POST /messages`)
- **R1.1** WHEN a valid request is received, THE SYSTEM SHALL store the message with status `Pending` and return `202` with the message id.
- **R1.2** The input SHALL be `phoneId` and `body`.
- **R1.3** IF `phoneId` does not exist, THEN THE SYSTEM SHALL return `400` with error code `PHONE_NOT_FOUND` and SHALL NOT store a message.
- **R1.4** THE SYSTEM SHALL NOT call the provider during the `POST` request. Sending happens in the background.

### R2: Employees and phones (`GET /employees`)
- **R2.1** THE SYSTEM SHALL return all employees, each with first name, last name, address and their phones (`phoneId` and number), in one call.
- **R2.2** An employee without phones SHALL be returned with an empty phone list. Messages cannot be sent to them, since there is no `phoneId` to choose.
- **R2.3** The list is read-only (no CRUD).
- **R2.4** Phones SHALL be ordered by `phoneId` ascending (display order only).

### R3: Validation
- **R3.1** IF `phoneId` or `body` is missing or empty, THEN THE SYSTEM SHALL return `400` (`VALIDATION_ERROR`).
- **R3.2** IF `body` is longer than 160 characters, THEN THE SYSTEM SHALL return `400` (`VALIDATION_ERROR`).
- **R3.3** WHEN seeding, a phone number SHALL match the Israeli mobile format (`05` followed by 8 digits). Otherwise it SHALL NOT be stored.

### R4: Sending to the provider
- **R4.1** A background service SHALL pick `Pending` messages and send the provider only the phone number and `body` (no name, no address).
- **R4.2** IF the provider call fails with a transient error (network error, 5xx or timeout), THEN THE SYSTEM SHALL retry, up to 3 attempts in total, with a growing delay.
- **R4.3** IF all attempts fail, THEN the message SHALL move to `Failed` with reason `SEND_FAILED`.
- **R4.4** The provider call timeout SHALL be 5 seconds.
- **R4.5** WHEN the provider accepts the message, it SHALL move to `Sent`.

### R5: Status tracking (polling)
- **R5.1** THE SYSTEM SHALL poll the provider for `Sent` messages every 2 seconds (configurable in `appsettings`).
- **R5.2** WHEN the provider reports delivery, the message SHALL move to `Delivered`. WHEN it reports failure, to `Failed`.
- **R5.3** IF 2 minutes (configurable) pass without a final status, THEN the message SHALL move to `Failed` with reason `DELIVERY_TIMEOUT`.
- **R5.4** IF a status poll fails, THEN the message SHALL keep its status, a warning SHALL be logged, and the poll SHALL be retried in the next cycle.
- **R5.5** Statuses: `Pending`, `Sent`, `Delivered`, `Failed`. `Delivered` and `Failed` are final.

### R6: Reading messages
- **R6.1** `GET /messages/{id}` SHALL return the message and its status, or `404` if it does not exist.
- **R6.2** `GET /messages` SHALL return a list, with an optional status filter. An invalid status SHALL return `400` (`INVALID_STATUS_FILTER`).

### R7: Duplicate prevention
- **R7.1** WHEN an `Idempotency-Key` that was already used is received, THE SYSTEM SHALL return the existing message and SHALL NOT create a new one.
- **R7.2** A unique index in the database SHALL enforce this.
- **R7.3** Known risk: a retry after a provider timeout may send twice. Documented, not handled.

### R8: Data and running
- **R8.1** EF Core Code First with Migrations. One employee has many phones. Unique index on (first name, last name, address). No unique index on the phone number.
- **R8.2** Migrations SHALL run automatically when the API starts.
- **R8.3** Seed data SHALL be inserted on startup, only if the Employees table is empty.
- **R8.4** One command (`run.cmd`, which runs `run.ps1`) SHALL start the fake provider, the API and the UI. The database is a full SQL Server on `localhost` (default instance) with Windows Authentication (decision 57).

### R9: Fake provider
- **R9.1** A separate project in the repository, with `POST` to send and `GET` to check status.
- **R9.2** Its contract is our own assumption and SHALL be documented in the README.
- **R9.3** Fixed behaviour: `Delivered` after 3 seconds. A phone number ending with `0000` is always `Failed`.

### R10: Tests
- **R10.1** Unit tests with xUnit, using a mocked provider and EF Core InMemory.
- **R10.2** Tests SHALL cover: unknown `phoneId`, status transitions and retry, and validation (message length, required fields).
- **R10.3** Idempotency, seeder and integration tests are out of scope and SHALL be listed in the README.

### R11: UI (optional, added in decision 52)
- **R11.1** A minimal React UI: pick an employee from `GET /employees`, see their phones, write a message.
- **R11.2** A single phone SHALL be selected automatically. With several phones the user picks one. With no phones, sending is blocked with an explanation.
- **R11.3** A messages table with statuses, refreshed automatically.

## 3. Risks and limitations
- 160 characters is not accurate for Hebrew (UCS-2 allows 70 per single SMS).
- A retry after a timeout may send twice.
- No idempotency, seeder or integration tests.
- Windows only, and requires a SQL Server default instance on `localhost` (otherwise, change the connection string).
- No authentication: anyone with access to the API sees all employees and phones.
- `Database.Migrate()` on startup and polling every 2 seconds are not production-ready.
- A long provider outage during polling ends in `DELIVERY_TIMEOUT`, even though the problem is the poll, not the message.
