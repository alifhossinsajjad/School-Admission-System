# Project Assumptions & Design Decisions

This document outlines key engineering decisions, concurrency guarantees, and assumptions made for the **School Admission System**.

---

## 1. Concurrency Across Multiple Backend Instances (Bonus §1.6)

### Requirement:
> *"একই database ব্যবহার করে backend-এর দুইটা process আলাদা port-এ একসাথে চললেও held + confirmed সংখ্যা totalSeats -এর বেশি হবে না। কীভাবে নিশ্চিত করলেন, ASSUMPTIONS.md -এ লিখবেন।"*

### How We Guaranteed This:
1. **Database-Level Row Locking (`SELECT ... FOR UPDATE`):**
   - Application memory locks (like C# `lock`, `SemaphoreSlim`, or `ConcurrentDictionary`) only work within a single process.
   - To guarantee absolute correctness across **multiple backend processes** (e.g., Process A on port 8080 and Process B on port 8081 connecting to the same PostgreSQL database), synchronization must occur at the **database layer**.
   - When a hold request is processed, we open a transaction with `IsolationLevel.ReadCommitted` and execute:
     ```sql
     SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE;
     ```
   - PostgreSQL places an exclusive row-level lock on the class row. Any concurrent transaction (from any backend process or port) attempting to inspect or alter seats for that class is blocked until the active transaction commits or rolls back.
2. **Atomic Verification Inside the Lock:**
   - Under the protection of the lock, we calculate:
     $$\text{HeldSeats} + \text{ConfirmedSeats} < \text{TotalSeats}$$
   - If capacity is exhausted, transaction immediately aborts and returns `409 Conflict`.
   - If seats remain, the new hold is inserted and committed atomically.
   - This makes race conditions and over-allocation mathematically impossible across any number of load-balanced backend processes.

---

## 2. Automatic Hold Expiry Without API Call (Rule 2)

### Requirement:
> *"মেয়াদ শেষ হলে seat নিজে থেকে ফেরত যাবে, কাউকে কোনো API call করতে হবে না।"*

### Implementation:
- A dual approach is utilized:
  1. **Dynamic Real-Time Calculation:**
     Whenever class availability or hold status is queried (`GET /classes`, `GET /classes/{id}`, `GET /holds/{id}`, `GET /me/holds`), the system evaluates:
     $$\text{IsActive} = (\text{Status} == \text{'HELD'} \land \text{ExpiresAt} > \text{UtcNow})$$
     If `ExpiresAt <= UtcNow`, the hold is immediately treated as `EXPIRED`, and the seat becomes instantly available without waiting for any background task or explicit API call.
  2. **Background Sweeper (`HoldCleanupBackgroundService`):**
     An ASP.NET Core `BackgroundService` runs periodically to update expired database rows (`Status = 'EXPIRED'`), ensuring database records remain clean and consistent.

---

## 3. Student Identification & Validation

- The specification stipulates that student identity is transmitted via the `X-Student-Id` HTTP header and represents a 17-digit birth registration number (জন্ম নিবন্ধন নম্বর).
- **Validation Rule:**
  - Header is validated with regex `^\d{17}$`.
  - Missing, malformed, non-numeric, or incorrect length values immediately return `400 Bad Request` with an explicit error message (Rule 7: *"ভুল input-এ 4xx দিতে হবে, কখনো 500 নয়"*).

---

## 4. Single Active Hold Restriction (Rule 3)

### Requirement:
> *"একজন শিক্ষার্থী একটা class-এ একই সময়ে একটার বেশি active আবেদন বা নিশ্চিত ভর্তি রাখতে পারবে না।"*

- During hold creation (inside the row lock), the system verifies whether the student already possesses:
  - An active hold (`Status == 'HELD' && ExpiresAt > UtcNow`), OR
  - A confirmed admission (`Status == 'CONFIRMED'`) in that class.
- If so, the request is rejected with `409 Conflict`.

---

## 5. Idempotent Confirmation & Cancellation (Rules 4 & 5)

- **Confirmation Idempotency:** If `POST /holds/{holdId}/confirm` is invoked multiple times for an already confirmed hold, it returns `200 OK` with `{ "holdId": "...", "status": "CONFIRMED" }` without incrementing `confirmedSeats`.
- **Ownership Verification:** Only the student whose `X-Student-Id` matches the hold's `StudentId` can confirm or cancel the hold (`403 Forbidden` if mismatched).
- **Cancellation:** `DELETE /holds/{holdId}` transitions an active hold to `CANCELLED` and returns `204 NoContent`. Confirmed admissions cannot be cancelled via this endpoint (`400 Bad Request`).

