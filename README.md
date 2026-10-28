# Capitec Appointment Booking System

A branch appointment booking demonstration built with **.NET 9, Angular 18 and SQL Server**. The solution uses a standalone `AppointmentAPI` project with `Application_Tier` and `Data_Tier` and provides a responsive booking UI, server-side validation, capacity-aware booking, idempotent retries, automated tests, and Docker support.

> This is a technical-assessment/demo project. Branches and the confirmation experience are simulated. The app does not send real email or SMS messages and is not affiliated with or endorsed by Capitec Bank.

## Features

- Responsive, accessible booking UI with branch selection, weekday date selection, available time slots, field-level validation and a booking summary.
- Server-side validation of branch, date, time, name, email, phone number and idempotency key.
- Configurable slot capacity (`Booking:SlotCapacity`) and advance-booking window (`Booking:AdvanceBookingDays`).
- SQL Server serializable transaction around the slot-capacity check, backed by an index on `(BranchId, StartsAt)`, to prevent concurrent bookings exceeding capacity.
- Persisted unique idempotency key. A retry with the same key and payload returns the saved appointment; a key reused with different details is rejected.
- Confirmation/reference shown only after the database transaction commits. No real notification is sent.
- EF Core migration for the initial schema and sample branches.
- Health endpoint at `/health` and OpenAPI document in Development.

## Solution layout

```text
Capitec_AppointmentBookingSystem/
├── AppointmentAPI/                           # Standalone HTTP API, startup, error handling
├── Application_Tier/                          # Booking rules, DTOs, service
├── Data_Tier/                                 # EF Core entities, DbContext, migration
├── Application_Tier.Tests/                    # Service tests (SQLite in-memory)
├── capitec_appointmentbookingsystem.client/   # Angular UI
├── Dockerfile
└── docker-compose.yml
```

## Requirements

For local development:

- .NET 9 SDK
- Node.js 22 and npm
- SQL Server LocalDB on Windows, or a SQL Server instance

For Docker:

- Docker Desktop / Docker Engine with Compose

## Run with Docker (recommended)

The container setup runs SQL Server and the API/UI separately. The Angular production build is copied into the ASP.NET Core `wwwroot`, so one browser URL serves the frontend and API.

1. From the solution root, set a strong local SQL Server password (PowerShell example):

   ```powershell
   $env:MSSQL_SA_PASSWORD = "Use-A-Strong-Local-Password_123!"
   docker compose up --build
   ```

   On macOS/Linux:

   ```bash
   export MSSQL_SA_PASSWORD='Use-A-Strong-Local-Password_123!'
   docker compose up --build
   ```

2. Open [http://localhost:8080](http://localhost:8080). The API health endpoint is [http://localhost:8080/health](http://localhost:8080/health).
3. Stop the services with `docker compose down`. Appointment data persists in the `appointment-sql-data` Docker volume. To delete that local demo data as well, use `docker compose down -v`.

The Compose file has a development-only fallback password to make a first run easy. Set `MSSQL_SA_PASSWORD` explicitly for any shared environment; never reuse the fallback password outside a disposable local demo.

## Run locally

The supplied `appsettings.json` uses SQL Server LocalDB and attaches the existing `Data_Tier/AppointmentDb.mdf` file. Run from the solution root on Windows with LocalDB installed:

```powershell
dotnet restore .\Capitec_AppointmentBookingSystem.sln
dotnet run --project .\AppointmentAPI
```

`AppointmentAPI` listens on `http://localhost:5095`. In a second terminal, run the Angular UI on `https://localhost:60726`; its Vite proxy forwards `/api` requests to the API:

```bash
cd capitec_appointmentbookingsystem.client
npm ci
npm start
```

The development proxy forwards `/api`, `/health` and `/openapi` requests to `AppointmentAPI` at `http://localhost:5095`. If you use a separate SQL Server instance, set `ConnectionStrings__AppointmentDb` to its connection string before starting the API. For example:

```powershell
$env:ConnectionStrings__AppointmentDb = "Server=localhost;Database=AppointmentBooking;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run --project .\AppointmentAPI
```

EF Core applies versioned migrations at startup. Back up the existing MDF before first run; if it already contains tables from a different schema, inspect and reconcile that schema before applying the initial migration.

## Configuration

| Setting | Default | Description |
|---|---:|---|
| `ConnectionStrings:AppointmentDb` | LocalDB MDF | SQL Server connection string |
| `Booking:SlotCapacity` | `4` | Maximum confirmed appointments per branch/time slot |
| `Booking:AdvanceBookingDays` | `60` | Maximum days ahead customers can book |

Configuration can be supplied through environment variables, for example `Booking__SlotCapacity=6`.

Branches are seeded in the initial migration. Appointment slots are generated from the branch's business hours (09:00–16:00, every 30 minutes) and available capacity is calculated from saved appointments. Weekends and past times are rejected.

## API

### `GET /api/branches`

Returns active branches.

### `GET /api/appointments/availability?branchId=1&date=2026-10-27`

Returns available half-hour slots and their remaining capacity for a branch/date.

### `POST /api/appointments`

Example request:

```json
{
  "branchId": 1,
  "date": "2026-10-27",
  "time": "10:00",
  "fullName": "Alex Morgan",
  "email": "alex@example.com",
  "phoneNumber": "082 123 4567",
  "idempotencyKey": "8d1e3a61-58dd-4a11-a4d2-a3b34e45c0bd"
}
```

A new booking returns `201 Created`; an identical retry returns the already saved result. A full slot returns `409 Conflict`. Validation errors return `400 Bad Request` with a user-readable message.

## Validation and concurrency notes

- Angular validation improves the experience, but the API repeats all important validation because clients cannot be trusted.
- The browser disables the submit button while the request is pending. The idempotency key is retained on failures and regenerated only after a successful response.
- The API checks and writes within a SQL Server `Serializable` transaction. The composite index supports range locking for the branch/time predicate. The unique idempotency-key index protects against duplicate requests at the database level.
- The API response/confirmation is produced only after `SaveChangesAsync` and transaction commit succeed.
- In a multi-instance production deployment, run EF migrations as a controlled release step rather than allowing every instance to migrate on startup. Add monitoring, rate limiting and authentication/authorization if the system is exposed beyond the assessment environment.

## Test

Run the backend service tests:

```bash
dotnet test Application_Tier.Tests/Application_Tier.Tests.csproj
```

The tests cover branch retrieval, idempotent retry behaviour and capacity rejection using an isolated in-memory SQLite database.

Run Angular tests:

```bash
cd capitec_appointmentbookingsystem.client
npm ci
npm test -- --watch=false
```

Build the frontend for production:

```bash
npm run build
```

Build the API:

```bash
dotnet build AppointmentAPI/AppointmentAPI.csproj -c Release
```

## Design choices

The implementation intentionally avoids unnecessary patterns and dependencies. Controllers stay thin, booking rules live in one application service, EF Core owns persistence, DTOs define the API contract, and Angular keeps HTTP calls in one service. SQL Server remains the source of truth for booking capacity and duplicate protection.
