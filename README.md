# 🎓 School Admission System

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![React 18](https://img.shields.io/badge/React-18-61DAFB.svg?style=flat&logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6.svg?style=flat&logo=typescript)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1.svg?style=flat&logo=postgresql)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED.svg?style=flat&logo=docker)](https://www.docker.com/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-CSS-06B6D4.svg?style=flat&logo=tailwindcss)](https://tailwindcss.com/)
[![Automated Tests](https://img.shields.io/badge/xUnit%20Tests-12%20Passed-brightgreen.svg?style=flat)]()

A production-grade, enterprise-ready full-stack School Admission System engineered for **OnnoRokom Projukti Limited** (Assistant Software Engineer Recruitment Assignment). Built with strict adherence to a First-Come, First-Served (FCFS) seat allocation model under heavy concurrent traffic with zero lottery and absolute over-allocation prevention.

---

## 📑 Table of Contents

- [Architectural Overview](#-architectural-overview)
- [Key Features & Business Rules](#-key-features--business-rules)
- [Monorepo Structure](#-monorepo-structure)
- [Quick Start with Docker Compose](#-quick-start-with-docker-compose)
- [Local Development Setup](#-local-development-setup)
- [API Contract & Specifications](#-api-contract--specifications)
- [Concurrency & Distributed Lock Strategy](#-concurrency--distributed-lock-strategy)
- [Automated Testing Strategy](#-automated-testing-strategy)
- [Environment Configuration](#-environment-configuration)

---

## 🏛 Architectural Overview

The solution is architected following **Clean Architecture** and **Domain-Driven Design (DDD)** principles, guaranteeing strict separation of concerns, testability, and deterministic behavior:

```mermaid
flowchart TD
    subgraph Client["Presentation Layer (Client)"]
        UI["React 18 SPA (Vite + TS + Tailwind)"]
        Applicant["Student / Guardian View\n(17-digit Birth Reg ID)"]
        Admin["School Admin Portal\n(Class & Shift Provisioning)"]
        UI --> Applicant
        UI --> Admin
    end

    subgraph API["Backend API Layer (ASP.NET Core 8)"]
        Controllers["Skinny Controllers\n(Classes / Holds / Me)"]
        Middleware["Global Exception & CORS Middleware"]
        Controllers --> Middleware
    end

    subgraph Domain["Domain & Application Layer"]
        Services["Domain Services (SeatHoldService, SchoolClassService)\nPure Business Logic & Invariant Enforcement"]
        BackgroundService["HoldCleanupBackgroundService\n(Periodic Sweep Worker)"]
    end

    subgraph Data["Infrastructure & Data Layer"]
        UoW["Unit of Work Pattern\n(Execution Strategy + ReadCommitted Isolation)"]
        Repos["Repositories (SchoolClassRepository, SeatHoldRepository)"]
        EF["Entity Framework Core 8"]
        UoW --> Repos
        Repos --> EF
    end

    subgraph Storage["Persistence Layer"]
        Postgres[("PostgreSQL Database\nSELECT ... FOR UPDATE Row Locks")]
    end

    Client -->|HTTP / JSON + X-Student-Id| Controllers
    Controllers --> Services
    Services --> UoW
    BackgroundService --> UoW
    EF --> Postgres
```

---

## 🎯 Key Features & Business Rules

1. **Guaranteed Capacity Invariant (`held + confirmed <= totalSeats`):**  
   Under extreme concurrent load, seat inventory is rigorously protected using PostgreSQL pessimistic row-level locking (`SELECT ... FOR UPDATE`). No seat is ever over-allocated or double-booked.
2. **Deterministic Auto-Expiry (Zero API Calls Needed):**  
   Seat holds are time-bounded by `HOLD_DURATION_SECONDS` (default: `600s` / 10 minutes). Real-time inventory queries dynamically evaluate `ExpiresAt > UtcNow`, instantly freeing expired seats for other applicants without requiring any manual trigger. A background hosted worker (`HoldCleanupBackgroundService`) maintains status consistency.
3. **Single Active Application per Student (Rule 3):**  
   Each student (identified via their 17-digit birth registration number) can hold at most one active or confirmed seat in any specific class at any point in time.
4. **Applicant Ownership & Security (Rule 4):**  
   Seat confirmations (`POST /holds/{id}/confirm`) and cancellations (`DELETE /holds/{id}`) strictly verify ownership against the `X-Student-Id` header, returning `403 Forbidden` for unauthorized attempts.
5. **Idempotent Confirmations (Rule 5):**  
   Submitting admission confirmation multiple times succeeds idempotently without double-counting seats or generating corrupt state.
6. **Multi-Instance Concurrency Safety (Bonus 1):**  
   Database-level row locking guarantees consistency across multiple load-balanced backend processes running on separate ports or containers against a shared database.
7. **Strict 4xx Status Contract (Rule 7):**  
   All invalid inputs, business constraint violations, and unauthorized operations yield structured 4xx status codes (`400`, `403`, `404`, `409`)—never raw `500 Internal Server Error`.

---

## 📁 Monorepo Structure

```text
school-admission-system/
├── docker-compose.yml              # Root multi-container orchestration
├── README.md                       # Master system documentation
├── ASSUMPTIONS.md                  # Comprehensive architectural assumptions
├── AI_USAGE.md                     # AI pair-programming disclosure report
│
├── backend/                        # C# (.NET 8 Web API + PostgreSQL)
│   ├── SchoolAdmission.sln
│   ├── Dockerfile                  # Multi-stage optimized container build
│   ├── src/
│   │   └── SchoolAdmission.Api/
│   │       ├── Controllers/        # ClassesController, HoldsController, MeController
│   │       ├── Services/           # SeatHoldService, SchoolClassService, HoldCleanupBackgroundService
│   │       ├── Repositories/       # ISchoolClassRepository, ISeatHoldRepository & Impls
│   │       ├── Data/               # AppDbContext, UnitOfWork, IUnitOfWork
│   │       ├── Models/             # SchoolClass, SeatHold, HoldStatus
│   │       ├── DTOs/               # Strongly-typed request/response models
│   │       ├── Middleware/         # GlobalExceptionMiddleware
│   │       └── Program.cs          # Minimal & clean application bootstrap
│   └── tests/
│       └── SchoolAdmission.Tests/  # xUnit Concurrency & Integration Test Suite
│
└── frontend/                       # React 18 + Vite + TypeScript + Tailwind CSS
    ├── Dockerfile                  # Production Nginx container build
    ├── package.json
    ├── src/
    │   ├── components/             # Live Countdown Timer, ClassCard, Modal, StatusBadges
    │   ├── pages/                  # ClassList, AdminPortal, ActiveHoldView, MyApplications
    │   ├── services/               # Centralized Axios API Client
    │   └── hooks/                  # usePolling, useCountdown, useStudentId
    └── vite.config.ts
```

---

## 🚀 Quick Start with Docker Compose

The entire ecosystem (PostgreSQL, .NET 8 Backend API, and React Frontend) starts with a single command without requiring any local SDK installations:

```bash
docker compose up --build
```

### Active Service Endpoints:

| Service | Accessible URL | Description |
|---|---|---|
| **Frontend Web Portal** | [http://localhost:5173](http://localhost:5173) | Reactive Admission Portal & Admin Screen |
| **Backend REST API** | [http://localhost:8080](http://localhost:8080) | Core Admission Engine (.NET 8 Kestrel) |
| **Swagger OpenAPI Docs** | [http://localhost:8080/swagger](http://localhost:8080/swagger) | Interactive API Explorer & Schema Specs |
| **PostgreSQL Database** | `localhost:5432` | Relational Data Store (`neondb` / local) |

To stop all services:
```bash
docker compose down
```

---

## 💻 Local Development Setup

### 1. Backend Setup (.NET 8 SDK)

1. Navigate to the backend directory:
   ```bash
   cd backend
   ```
2. Configure environment variables in `.env` or set `DATABASE_URL`:
   ```env
   PORT=5000
   DATABASE_URL=postgresql://postgres:postgres@localhost:5432/school_admission_db
   HOLD_DURATION_SECONDS=600
   ```
3. Run database migrations / ensure schema and start API:
   ```bash
   dotnet run --project src/SchoolAdmission.Api
   ```
   Interactive Swagger UI will be live at `http://localhost:5000/swagger`.

### 2. Frontend Setup (Node.js 18+)

1. Navigate to the frontend directory:
   ```bash
   cd frontend
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Launch development server:
   ```bash
   npm run dev
   ```
   Frontend will run at `http://localhost:5173`.

---

## 📡 API Contract & Specifications

All request and response payloads use camelCase JSON formatting. The identity of the student is conveyed via the standard `X-Student-Id` header (17-digit birth registration number).

| # | HTTP Method | Path | Request Header | Description & Status Codes |
|---|---|---|---|---|
| 1 | `POST` | `/classes` | - | Create new class (`201 Created`) |
| 2 | `GET` | `/classes` | - | List all classes with real-time seat inventory (`200 OK`) |
| 3 | `GET` | `/classes/{classId}` | - | Retrieve single class inventory details (`200 OK`, `404`) |
| 4 | `POST` | `/classes/{classId}/holds` | `X-Student-Id` | Apply for a seat hold (`201 Created`, `409 Conflict` if full) |
| 5 | `GET` | `/holds/{holdId}` | - | Inspect hold status: `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED` (`200 OK`, `404`) |
| 6 | `POST` | `/holds/{holdId}/confirm` | `X-Student-Id` | Submit admission payment & confirm seat (`200 OK`, `400`, `403`, `404`) |
| 7 | `DELETE` | `/holds/{holdId}` | `X-Student-Id` | Cancel hold and immediately release seat (`204 NoContent`, `403`, `404`) |
| 8 | `GET` | `/me/holds` | `X-Student-Id` | List all applications belonging to current student (`200 OK`, `400`) |

---

## ⚡ Concurrency & Distributed Lock Strategy

To satisfy **Rule 1** (`held + confirmed <= totalSeats`) under high-volume burst traffic, the system implements **Pessimistic Row-Level Locking** at the database layer:

```mermaid
sequenceDiagram
    autonumber
    actor StudentA as Student A (Thread 1)
    actor StudentB as Student B (Thread 2)
    participant API as Backend Service
    participant DB as PostgreSQL (Classes Table)

    Note over StudentA, StudentB: Both apply concurrently for the LAST available seat
    StudentA->>API: POST /classes/{classId}/holds
    StudentB->>API: POST /classes/{classId}/holds

    API->>DB: BEGIN TRANSACTION (ReadCommitted)
    API->>DB: SELECT * FROM "Classes" WHERE "Id" = @id FOR UPDATE (Thread 1 acquires lock)
    Note over DB: Thread 2 blocked waiting for lock release

    API->>DB: Calculate: held (19) + confirmed (80) < total (100) -> Valid!
    API->>DB: INSERT INTO "Holds" VALUES ('h_101', 'HELD', ...)
    API->>DB: COMMIT TRANSACTION (Thread 1 releases lock)
    API-->>StudentA: 201 Created (Hold ID: h_101)

    Note over DB: Thread 2 now enters critical section with lock
    API->>DB: Calculate: held (20) + confirmed (80) = 100 -> FULL!
    API->>DB: ROLLBACK TRANSACTION
    API-->>StudentB: 409 Conflict ("No available seats left in this class.")
```

### Why this is Multi-Instance Safe:
Because the lock is held inside the database engine itself rather than in-memory (`lock` / `Monitor` / `Semaphore`), multiple independent backend containers running on separate machines or ports synchronizing on the same PostgreSQL database remain 100% collision-free.

---

## 🧪 Automated Testing Strategy

The repository contains an enterprise automated test suite targeting critical edge cases and stress scenarios:

### Running Backend Tests:
```bash
dotnet test
```

### Key Automated Tests Included:
1. **`ConcurrencyTests` (Race Condition & Over-allocation Verification):**  
   Fires 50 simultaneous asynchronous threads competing for a class configured with only 5 seats. Verifies that **exactly 5 succeed (HTTP 201)**, **exactly 45 receive HTTP 409**, and the database contains zero over-allocated seats.
2. **`HoldLifecycleTests`:**  
   - Complete lifecycle: Reserve Hold ➔ Confirm Payment ➔ Verification.
   - Idempotency test: Multiple rapid calls to `/confirm` do not increment confirmed seats more than once.
   - Immediate seat recovery upon cancellation (`DELETE /holds/{holdId}`).
   - Automatic seat recovery when hold duration expires without payment.
3. **`ValidationAndRulesTests`:**  
   - Strict 17-digit birth registration ID validation (`400 Bad Request`, never 500).
   - Duplicate active hold prevention for a single student (`409 Conflict`).
   - Cross-student cancellation and confirmation security enforcement (`403 Forbidden`).

---

## ⚙️ Environment Configuration

| Variable Name | Default Value | Description |
|---|---|---|
| `PORT` | `8080` (Docker) / `5000` (Local) | HTTP listener port for the .NET Kestrel server |
| `DATABASE_URL` | PostgreSQL URI string | Connection string supporting local or cloud (NeonDB SSL) |
| `HOLD_DURATION_SECONDS` | `600` (10 minutes) | Expiration window for unconfirmed holds |
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Development` | ASP.NET Core environment profile |
| `VITE_API_URL` | `http://localhost:8080` | Backend API base URL consumed by Vite frontend |

---

## 🏆 Submission Deliverables

- [x] **Public GitHub Repository** with atomic, progressive commit history.
- [x] **Root `docker-compose.yml`** orchestrating full stack with a single command.
- [x] **Production `Dockerfile`** for both Backend (.NET 8 SDK) and Frontend (Nginx).
- [x] **`README.md`** comprehensive setup and testing guide.
- [x] **`ASSUMPTIONS.md`** detailed architectural and business rationale.
- [x] **`AI_USAGE.md`** complete disclosure of AI assistance and human engineering review.
- [x] **Multi-Instance Bonus Achieved** (PostgreSQL row-level locking across separate processes).

---

Developed with ❤️ for **OnnoRokom Projukti Limited** recruitment assignment.
