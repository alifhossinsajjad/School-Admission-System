# AI Usage Report (AI_USAGE.md)

This document provides a transparent overview of how generative AI tools (specifically **Claude 3.5 Sonnet** and **ChatGPT / GPT-4o**) were utilized during development, where AI suggestions were found to be suboptimal or architecturally flawed, and how engineering discernment was applied to resolve them.

---

## 1. AI Tools Utilized & Scope of Assistance

- **Tools:** Anthropic Claude 3.5 Sonnet & OpenAI ChatGPT (GPT-4o).
- **Scope of Use:**
  - Generating initial DTO classes strictly adhering to the JSON schema in the assignment brief.
  - Scaffolding repetitive boilerplate code for API controllers and service interfaces.
  - Designing initial test fixtures for high-concurrency simulation (`Task.WhenAll`).

---

## 2. Flaws & Suboptimal Solutions in AI Output and How We Corrected Them

### Issue 1: In-Memory Locking vs. Multi-Instance Concurrency (Bonus Section 1.6)
- **AI Suggestion:** When prompted to prevent race conditions during seat holds, the AI initially recommended wrapping the seat reservation block in a C# in-memory `SemaphoreSlim` or a synchronized `lock` statement.
- **Why It Was Inadequate:** While an in-memory lock passes single-instance unit tests, it completely fails the architectural requirement for multi-process scalability (Bonus Section §1.6: running two separate backend processes on different ports against the same database without exceeding total seats). In a multi-instance or load-balanced setup, each backend process has its own memory heap; hence, in-memory locks do not protect against cross-process overselling.
- **Our Resolution:** We discarded the in-memory synchronization approach in favor of **PostgreSQL database-level row locking (`SELECT ... FOR UPDATE`)** inside an atomic database transaction (`IsolationLevel.ReadCommitted`). This guarantees that PostgreSQL itself serializes access to the target class row across any number of backend processes, instances, or containers.

### Issue 2: Latency in Hold Expiry & Seat Release (Rule 2)
- **AI Suggestion:** The AI initially proposed relying solely on a periodic background hosted service (`IHostedService`) that sweeps the database every 10–15 seconds to update expired holds from `HELD` to `EXPIRED`.
- **Why It Was Inadequate:** If a hold's 600-second window expires at $t=10.0$s and the background sweeper only runs at $t=15.0$s, there is a 5-second dead window during which an expired seat is incorrectly shown as unavailable to waiting applicants. This directly violates the rule requiring that seats automatically return to availability immediately upon expiry without requiring any API call.
- **Our Resolution:** We refactored all seat-availability queries and status lookups to evaluate expiration dynamically in real-time:
  $$\text{IsActive} = (\text{Status} == \text{"HELD"} \land \text{ExpiresAt} > \text{DateTime.UtcNow})$$
  Under this logic, the exact millisecond `ExpiresAt` is passed, the hold is instantaneously evaluated as expired, and the seat is immediately available for the next applicant—independent of background task intervals. The background sweeper was retained merely for database hygiene.

### Issue 3: C# Roslyn Compiler Error with Record Attributes
- **AI Suggestion:** The AI generated C# positional records with `[JsonPropertyName(...)]` directly placed on positional parameters.
- **Why It Was Inadequate:** In C# 12 / .NET 8, applying property attributes to record parameters requires the `[property: ...]` target specifier; otherwise, the Roslyn compiler halts with error `CS0592: Attribute 'JsonPropertyName' is not valid on this declaration type`.
- **Our Resolution:** We replaced positional record constructors with explicit, type-safe class models featuring parameterless constructors (necessary for System.Text.Json deserialization) and parameterized constructor overloads for clean unit test authoring.
