# AI Usage Report (AI_USAGE.md)

This report details how AI assistance was leveraged during development, where potential discrepancies/suboptimal solutions were caught, and how engineering discernment was applied.

---

## 1. AI Tools Used
- **Tool:** Antigravity AI Coding Assistant (powered by Google DeepMind Gemini 3.8 Flash).
- **Scope of Use:**
  - Initial solution scaffolding and boilerplate generation.
  - Designing API DTO models matching the exact specification schema.
  - Generating initial structure for concurrency simulation tests in xUnit.

---

## 2. Discrepancies / Suboptimal AI Suggestions & How They Were Corrected

### Discrepancy 1: In-Memory Locking vs. Multi-Instance Concurrency (Bonus §1.6)
- **AI Suggestion:** The initial code suggestion proposed using a C# in-memory `SemaphoreSlim` or `lock` statement around the hold creation block.
- **The Issue:** While an in-memory lock passes single-instance unit tests, it fails completely when two backend instances run on separate ports/containers connecting to the same PostgreSQL database (Bonus Section §1.6). In that scenario, each process has its own memory space, leading to race conditions and overselling.
- **Our Correction:** We enforced **database-level row locking (`SELECT ... FOR UPDATE`)** inside an atomic transaction. This guarantees that PostgreSQL serializes seat reservation requests regardless of how many backend processes or containers are running.

### Discrepancy 2: Hold Expiry Latency (Rule 2)
- **AI Suggestion:** Initially, AI implemented a scheduled background timer (`IHostedService`) to sweep and update expired records in the database.
- **The Issue:** Relying solely on a periodic sweeper means a hold whose deadline passed at $t=10.0$s might not free its seat until the sweeper runs at $t=15.0$s, violating the requirement: *"মেয়াদ শেষ হলে seat নিজে থেকে ফেরত যাবে, কাউকে কোনো API call করতে হবে না"*.
- **Our Correction:** We refactored all seat-counting and status-query logic to evaluate expiration dynamically:
  $$\text{IsActive} = (\text{Status} == \text{'HELD'} \land \text{ExpiresAt} > \text{DateTime.UtcNow})$$
  As a result, the moment the clock ticks past `expiresAt`, the seat is instantly recognized as available, while the background service acts as a secondary database hygiene sweeper.

### Discrepancy 3: C# Record Attribute Placement
- **AI Suggestion:** In positional records, `[JsonPropertyName]` was placed directly on positional parameters, causing C# compiler error `CS0592: Attribute 'JsonPropertyName' is not valid on this declaration type`.
- **Our Correction:** We converted the DTOs into standard class models with explicit properties and dual constructors (parameterless for JSON deserialization and parameterized for clean test authoring).

