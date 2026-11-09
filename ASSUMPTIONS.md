# Project Assumptions & Design Decisions

This document details the architectural decisions, concurrency guarantees, and system assumptions implemented for the **School Admission System**.

---

## 1. Concurrency Across Multiple Backend Instances (Bonus Section 1.6)

### Requirement:
> *"Ensure that even if two separate backend processes run on different ports against the same database, the invariant (heldSeats + confirmedSeats <= totalSeats) is strictly preserved without over-allocation."*

### Architectural Solution:
1. **Database-Level Row Locking (`SELECT ... FOR UPDATE`):**
   - Application-level memory synchronization primitives (such as C# `lock`, `SemaphoreSlim`, or `ConcurrentDictionary`) are strictly bound to a single process heap and cannot coordinate across multiple running processes or containers.
   - To guarantee complete thread safety across **multiple distributed backend instances** (e.g., Process A on port 8080 and Process B on port 8081 sharing the same PostgreSQL database), synchronization is enforced directly at the **database engine layer**.
   - During seat reservation (`POST /classes/{classId}/holds`), an atomic transaction is initiated with `IsolationLevel.ReadCommitted` executing:
     ```sql
     SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE;
     ```
   - PostgreSQL places an exclusive row-level lock on the target class. Any competing transaction from any backend instance attempting to reserve seats in that class is queued until the current transaction commits or rolls back.

2. **Atomic Verification Inside the Critical Section:**
   - Within the locked boundary, the system dynamically calculates:
     $$\text{HeldSeats} + \text{ConfirmedSeats} < \text{TotalSeats}$$
   - If capacity is exhausted, the transaction immediately terminates and returns `409 Conflict`.
   - If seats remain available, a new hold record is inserted and committed atomically.
   - This prevents race conditions and eliminates the possibility of overselling across any number of load-balanced backend processes.

---

## 2. Automatic Hold Expiry Without API Calls (Rule 2)

### Requirement:
> *"Application hold duration is determined by environment variable HOLD_DURATION_SECONDS (default 600). When duration expires, the seat must automatically return to available status without requiring any external API call."*

### Architectural Solution:
- A dual-layer strategy is employed:
  1. **Dynamic Real-Time Calculation:**
     Whenever class availability or application status is queried (`GET /classes`, `GET /classes/{classId}`, `GET /holds/{holdId}`, `GET /me/holds`), the system evaluates hold validity in real-time:
     $$\text{IsActive} = (\text{Status} == \text{"HELD"} \land \text{ExpiresAt} > \text{UtcNow})$$
     The exact millisecond `ExpiresAt <= UtcNow`, the hold is immediately treated as `EXPIRED` and excluded from `heldSeats`. The seat becomes instantly accessible to new applicants without waiting for background timers or external triggers.
  2. **Background Sweeper (`HoldCleanupBackgroundService`):**
     An ASP.NET Core `BackgroundService` runs periodically in the background to update expired database rows (`Status = "EXPIRED"`), ensuring persisted database state remains clean and auditable.

---

## 3. Student Identification & Input Validation

- In accordance with the specification, student identity is provided via the `X-Student-Id` HTTP header, representing a 17-digit national birth registration number.
- **Validation Rules:**
  - Header is validated using the regular expression `^\d{17}$`.
  - Empty, malformed, non-numeric, or incorrect-length headers immediately return `400 Bad Request` with an informative error description (Rule 7: Invalid input returns 4xx, never 500).

---

## 4. Single Active Hold Restriction (Rule 3)

### Requirement:
> *"A student cannot maintain more than one active hold or confirmed admission in the same class simultaneously."*

- During hold creation (inside the database row lock), the system verifies whether the applicant already holds:
  - An active hold (`Status == "HELD" && ExpiresAt > UtcNow`), OR
  - A confirmed admission (`Status == "CONFIRMED"`) in that class.
- If an existing active record is detected, the request is rejected with `409 Conflict`.

---

## 5. Idempotent Confirmation & Hold Cancellation (Rules 4 & 5)

- **Confirmation Idempotency:** If `POST /holds/{holdId}/confirm` is invoked multiple times for an already confirmed hold, it returns `200 OK` with `{ "holdId": "...", "status": "CONFIRMED" }` without incrementing `confirmedSeats`.
- **Ownership Verification:** An applicant can only confirm or cancel holds linked to their own `X-Student-Id`. Unauthorized attempts return `403 Forbidden`.
- **Cancellation:** `DELETE /holds/{holdId}` transitions an active hold to `CANCELLED` and returns `204 NoContent`. Confirmed admissions cannot be cancelled through this endpoint (`400 Bad Request`).
