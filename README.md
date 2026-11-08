# School Admission System - Backend API

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791.svg)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-purple.svg)](https://learn.microsoft.com/ef/core/)
[![Tests](https://img.shields.io/badge/Tests-12%20Passed-brightgreen.svg)]()

High-performance, concurrency-safe School Admission System built with **C# (.NET 8)**, **Entity Framework Core 8**, and **PostgreSQL** for **OnnoRokom Projukti Limited** recruitment assignment.

---

## Architecture & Features

- **Strict API Contract Conformance:** Endpoints match the JSON contract byte-for-byte.
- **First-Come First-Served & Absolute Concurrency Safety:**
  - Uses PostgreSQL row-level locks (`SELECT ... FOR UPDATE`) inside atomic database transactions (`ReadCommitted`).
  - Guaranteed `heldSeats + confirmedSeats <= totalSeats` under high concurrency, even with multiple backend instances.
- **Automatic Hold Expiry (Zero API Call):**
  - Hold duration configured via `HOLD_DURATION_SECONDS` (default: 600s / 10 minutes).
  - Seat calculations immediately reflect expired holds dynamically (`expiresAt <= UtcNow`).
  - Background sweeping service (`HoldCleanupBackgroundService`) maintains database status consistency.
- **Idempotency & Business Invariants:**
  - Per-student single active/confirmed hold restriction in each class (Rule 3).
  - Multiple confirmation calls succeed idempotently without double-counting seats (Rule 5).
  - Comprehensive 4xx error formatting with user-friendly messages, never 500 (Rule 7).

---

## API Endpoints

Base URL: `http://localhost:8080`

| # | Method | Path | Header | Description |
|---|---|---|---|---|
| 1 | `POST` | `/classes` | - | Create new class (`{ "name": "...", "totalSeats": 120 }`) |
| 2 | `GET` | `/classes` | - | List all classes with real-time seat statistics |
| 3 | `GET` | `/classes/{classId}` | - | Get specific class seat status |
| 4 | `POST` | `/classes/{classId}/holds` | `X-Student-Id` | Apply / Hold a seat (returns `409` if full) |
| 5 | `GET` | `/holds/{holdId}` | - | Check application/hold status (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`) |
| 6 | `POST` | `/holds/{holdId}/confirm` | `X-Student-Id` | Confirm admission (pay fee) |
| 7 | `DELETE` | `/holds/{holdId}` | `X-Student-Id` | Cancel application hold (`204 NoContent`) |
| 8 | `GET` | `/me/holds` | `X-Student-Id` | List all applications belonging to the student |

---

## Getting Started

### Option 1: Run with Docker Compose (Recommended)

```bash
docker compose up --build
```
This starts PostgreSQL and the Backend API on `http://localhost:8080`.

### Option 2: Run Locally with .NET 8 SDK

1. Ensure PostgreSQL is running and update connection string in `appsettings.json` or set `DATABASE_URL`.
2. Run the API:
   ```bash
   dotnet run --project src/SchoolAdmission.Api
   ```
3. Swagger UI is available at `http://localhost:8080/swagger`.

---

## Running Automated Tests

Run the full xUnit test suite (12 tests including high-concurrency race condition verification):

```bash
dotnet test
```

### Automated Tests Included:
1. **`ConcurrencyTests`**: Simulates 50 simultaneous applicants competing for a class with only 5 seats. Verifies exactly 5 receive `201 Created`, 45 receive `409 Conflict`, and zero overselling occurs.
2. **`HoldLifecycleTests`**:
   - End-to-end hold & confirm flow.
   - Idempotent confirmation (calling confirm multiple times does not increment confirmed count).
   - Seat release on cancellation (`DELETE /holds/{holdId}`).
   - Automatic seat recovery on hold expiration.
3. **`ValidationAndRulesTests`**:
   - 17-digit birth registration number validation (`400 Bad Request`, never 500).
   - Duplicate active hold prevention for a single student (`409 Conflict`).
   - Cross-student access prohibition (`403 Forbidden`).
   - Retrieval of student's applications via `GET /me/holds`.

