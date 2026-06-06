# Implementation Plan: MCP Server Integration

**Branch**: `007-mcp-server` | **Date**: 2026-06-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-mcp-server/spec.md`

## Summary

Introduce a dedicated `Finance.Mcp` project — a 4th production project sitting alongside `Finance.Api`, both referencing `Finance.Business` — that exposes five MCP operations: `sendContext`, `requestAction`, `receiveResult`, `confirm`, and `rollback`. The server stores contexts in-memory with TTL enforcement, serialises point-in-time snapshots for deterministic replay, redacts sensitive fields before any snapshot leaves the server, supports a verify→refine loop with configurable iteration limits, enforces a human approval gate on business-logic-touching confirms, and appends structured entries to a dedicated MCP iteration log at `ai-artifacts/mcp_iteration_log.txt`. Agent responses are mocked in all tests; no live API calls are made in CI. A constitution MINOR amendment is required before implementation to permit the 4th production project.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: `System.Text.Json` (already in project), `System.Security.Cryptography` (in-box for SHA-256), `Moq` 4.20.x (test-only)

**Storage**: In-memory only — `ConcurrentDictionary` in a singleton `McpContextStore`; context snapshots written as JSON fixture files to `specs/007-mcp-server/contracts/snapshots/` for replay tests only, never persisted at runtime

**Testing**: xUnit v3, `Xunit.Assert`, Moq (mocked agent responses); all MCP tests in a new `Finance.Mcp.UnitTests` project

**Target Platform**: In-process .NET 10 class library, loaded by the same host as `Finance.Api`

**Project Type**: Dedicated class library (`Finance.Mcp`) — 4th production project, sibling of `Finance.Api`

**Performance Goals**: Not performance-sensitive; single-user, in-process, no latency budget

**Constraints**: No external process or network calls; CI runs with no external dependencies; all agent responses mocked in tests; MCP log is append-only and must never be read before writing

**Scale/Scope**: Single-user; handful of MCP operations per agent session; in-memory state reset on restart

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked after Phase 1.*

| # | Check | Status | Notes |
|---|-------|--------|-------|
| 1 | Dependency direction preserved (`Api → Business → Data`) | ⚠️ Amendment required | `Finance.Mcp → Finance.Business → Finance.Data` preserves the one-way direction. `Finance.Api` and `Finance.Mcp` are siblings — neither references the other. The constitution currently names three production projects; adding `Finance.Mcp` requires a MINOR amendment. See Complexity Tracking. |
| 2 | Any new report type uses factory + strategy | ✅ N/A | This feature introduces no new report types. |
| 3 | New data in singleton in-memory repositories with deterministic int IDs | ✅ Pass | `McpContextStore` registered as singleton. Context IDs are ephemeral string keys (not asserted by value in tests), consistent with Principle III intent. |
| 4 | Required test coverage areas addressed | ✅ Pass | Constitution §IV lists "MCP context serialization, replay consistency, and sensitive-field redaction". All five user stories have dedicated test classes in the new `Finance.Mcp.UnitTests` project. |
| 5 | No out-of-scope technology introduced | ✅ Pass | No SQL, EF Core, Docker, auth, or external process. In-process only. |

**Pre-implementation gate**: A constitution MINOR amendment must be merged before the `Finance.Mcp` project is created. The amendment adds `Finance.Mcp` as a permitted 4th production project and documents the sibling dependency pattern.

## Research & Decisions

### D1 — MCP layer placement
**Chosen**: A dedicated `Finance.Mcp` project — a 4th production project alongside `Finance.Api`, both referencing `Finance.Business`. Dependency graph: `Finance.Api → Finance.Business → Finance.Data` and `Finance.Mcp → Finance.Business → Finance.Data`. `Finance.Api` and `Finance.Mcp` do not reference each other.
**Rationale**: MCP is a protocol/infrastructure adapter layer, not business logic. `Finance.Business` already has a clear identity (domain services, DTOs, mappers, strategies). Placing the MCP protocol infrastructure there conflates concerns the same way putting controllers in Business would. A dedicated project makes FR-012's "client must not expose domain types" structurally enforceable via a `.csproj` dependency check, not just a naming convention. The pattern mirrors `Finance.Api` (HTTP adapter) — `Finance.Mcp` is the agent-protocol adapter over the same business core.
**Alternatives considered**: `Finance.Business/Mcp/` sub-namespace — rejected because it conflates protocol infrastructure with domain logic, makes FR-012's isolation convention-only (not enforced by the compiler), and causes `Finance.Business.UnitTests` to test both domain logic and MCP protocol behaviour in the same project. The constitution amendment cost is low; the architectural clarity gained is permanent.

### D2 — Client/server separation within Finance.Mcp
**Chosen**: `IMcpServer` is the full server contract (all five operations plus `ApproveConfirm`). `McpServer` calls `Finance.Business` services via their interfaces (`ICategoryService`, `ITransactionService`) for validation. `IMcpClient` is a thin facade — it accepts and returns only `Finance.Mcp` schema types and delegates to `IMcpServer`. Because `IMcpClient` is defined in `Finance.Mcp` (which references `Finance.Business`), the *project* can reach Business services, but the *client interface* does not expose them. FR-012 is satisfied structurally.
**Rationale**: Natural separation — the server implementation needs Business services; the client surface does not. The project boundary makes this explicit without additional conventions.
**Alternatives considered**: A single unified interface with no client/server distinction — rejected because the spec requires an identifiable client library as a deliverable and because mixing server internals into the client surface would leak Business types through the client API.

### D3 — Context store lifetime and TTL eviction
**Chosen**: `IMcpContextStore` registered as a DI **singleton** (matching repository registration policy, Principle III). Contexts stored in a `ConcurrentDictionary<string, McpContext>`. TTL enforced passively: every `Get` checks `context.Ttl` against `DateTime.UtcNow` and returns null (setting status to `Expired`) if past.
**Rationale**: Consistent with the project's singleton-repository pattern. Passive eviction avoids background threads, which add complexity and non-determinism to tests.
**Alternatives considered**: Active eviction via `System.Threading.Timer` — rejected; adds a background thread without benefit for a single-user app and makes tests non-deterministic.

### D4 — Replay determinism mechanism
**Chosen**: Snapshots serialised using `System.Text.Json` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and `WriteIndented = false`. SHA-256 hash computed from the UTF-8 bytes of the resulting string. Replay is deterministic because (a) snapshot content is immutable once created, (b) the mocked agent stub returns a fixed `AgentResult` keyed on the snapshot hash, and (c) `System.Text.Json` produces stable property order for record types.
**Rationale**: No new serialisation library needed. Record types emit properties in declaration order, which is stable across CLR runs.
**Alternatives considered**: Alphabetically sorting JSON properties before hashing — rejected as unnecessary overhead given record-type stability guarantees.

### D5 — Sensitive field redaction
**Chosen**: A `static readonly HashSet<string>` of prohibited key names (case-insensitive): `password`, `secret`, `token`, `email`, `name`, `accountnumber`, `iban`, `ssn`, `description`. `ContextRedactor` replaces `pendingTransactions[*].description` with `"[REDACTED]"` before any serialisation. No other current schema fields require redaction (amounts, dates, category names, and integer IDs are not PII). New sensitive fields added to the schema must be explicitly registered in the prohibited-key set.
**Rationale**: Explicit, centrally configured, and trivially testable. The safety test asserts against this known set.
**Alternatives considered**: Attribute-based redaction (`[Redacted]` on DTO properties) — rejected as over-engineering for a two-field requirement.

### D6 — Human approval gate
**Chosen**: `McpServer.Confirm()` inspects `AgentResult.proposed_changes` for entries whose `targetField` is one of the business-logic-protected fields (`categoryType`, `transactionType`). If found, the context transitions to `ContextStatus.PendingApproval` and `Confirm()` returns a `ConfirmResult` with `RequiresApproval = true`. A separate `ApproveConfirm(contextId)` method completes the confirmation. Both methods live on `IMcpServer`.
**Rationale**: Keeps the gate entirely in the Business layer. The API can surface `RequiresApproval = true` as HTTP 202 if an endpoint is ever added.
**Alternatives considered**: Always requiring approval for every confirm — rejected because it would break non-business-logic categorisation tests and add friction to the verify→refine loop.

### D7 — MCP iteration log format and retention
**Chosen**: `ai-artifacts/mcp_iteration_log.txt` is append-only. Each entry is a fenced block (`--- MCP ENTRY START ---` / `--- MCP ENTRY END ---`) with `Key: Value` lines for all FR-010 fields. Retention policy: on each write, entries older than 30 days or beyond a 500-entry cap are pruned (file rewritten with surviving entries only).
**Rationale**: Consistent visual format with the existing `agent_log.txt`. The 30-day / 500-entry cap prevents unbounded growth without needing a database.
**Alternatives considered**: JSON-Lines format — rejected to maintain visual consistency with `agent_log.txt`. SQLite — rejected (out of scope per constitution).

## Artifact Generation Decisions

| Artifact | Generate? | Reason |
|---|---|---|
| `research.md` | No | All decisions are inline above; the feature is well-constrained by the constitution and spec. |
| `data-model.md` | Yes | Introduces 6 new record types and 2 new enums, all owned by Finance.Mcp. |
| `contracts/` | Yes | Defines the MCP operation interface, context schema, snapshot format, and sensitive-field list — a clear inter-component communication boundary. |
| `quickstart.md` | No | All acceptance criteria are fully covered by automated tests; no manual verification steps required. |

## Project Structure

### Documentation (this feature)

```text
specs/007-mcp-server/
├── plan.md                               # This file
├── data-model.md                         # New entities, enums, validation rules
├── contracts/
│   ├── mcp-operations.md                 # 5 operations + ApproveConfirm interface contract
│   ├── mcp-context-schema.json           # JSON schema for McpContext serialisation
│   ├── sensitive-fields.md               # Prohibited key list with per-field rationale
│   └── snapshots/
│       └── reconciliation-example.json   # Example context snapshot fixture for replay tests
└── tasks.md                              # Generated by /speckit-tasks
```

### Source Code

```text
src/backend/FinanceTracker/
│
├── Finance.Mcp/                           # NEW — 4th production project (requires constitution amendment)
│   ├── Finance.Mcp.csproj                 # References Finance.Business only; no Finance.Data or Finance.Api
│   ├── Schema/
│   │   ├── McpContext.cs                  # record: Id, Version, Ttl, CategoryMappings, PendingTransactions, Status, CreatedAt
│   │   ├── ReconciliationContext.cs       # record: AmbiguousTransactionIds, SuggestedCategoryIds, DecisionStatus
│   │   ├── AgentActionRequest.cs          # record: ActionId, ContextId, ActionType, IterationNumber, PromptText, ContextFields
│   │   ├── AgentResult.cs                 # record: ActionId, ProposedChanges, Explanation, ReceivedAt
│   │   ├── ContextSnapshot.cs             # record: ContextId, SerialisedAt, Hash, RedactedJson
│   │   ├── IterationLogEntry.cs           # record: Timestamp, Prompt, ContextSnapshotHash, ModelName, AgentOutput, AcceptedDiff, DecisionReason
│   │   ├── ContextStatus.cs               # enum: Active, PendingApproval, Confirmed, RolledBack, Expired
│   │   └── ActionType.cs                  # enum: Categorize, Validate
│   ├── IMcpServer.cs                      # SendContext, RequestAction, ReceiveResult, Confirm, Rollback, ApproveConfirm
│   ├── McpServer.cs                       # Calls ICategoryService / ITransactionService for validation; depends on IMcpContextStore, IContextRedactor, IMcpIterationLogger
│   ├── IMcpContextStore.cs
│   ├── McpContextStore.cs                 # Singleton ConcurrentDictionary; passive TTL eviction
│   ├── Client/
│   │   ├── IMcpClient.cs                  # Thin facade; only Finance.Mcp schema types at its boundary
│   │   └── McpClient.cs                   # Delegates to IMcpServer
│   ├── Redaction/
│   │   ├── IContextRedactor.cs
│   │   └── ContextRedactor.cs             # Replaces description with [REDACTED]; enforces prohibited-key set
│   └── Logging/
│       ├── IMcpIterationLogger.cs
│       └── McpIterationLogger.cs          # Appends to ai-artifacts/mcp_iteration_log.txt; enforces retention policy
│
├── Finance.Api/
│   └── Program.cs                         # Add DI registrations for Finance.Mcp services (singleton store, scoped server/client/redactor/logger)
│                                          # Finance.Api.csproj does NOT add a ProjectReference to Finance.Mcp
│                                          # (registrations use the concrete types directly from Finance.Mcp)
│
└── tests/
    └── Finance.Mcp.UnitTests/             # NEW — mirrors Finance.Mcp; references Finance.Mcp + Moq
        ├── Finance.Mcp.UnitTests.csproj
        ├── McpServerRoundTripTests.cs     # US1: all 5 operations sequentially; strict Moq on ICategoryService/ITransactionService
        ├── ContextSnapshotReplayTests.cs  # US2: 3 consecutive replays produce byte-identical proposed_changes
        ├── ContextRedactionSafetyTests.cs # US3: parameterised — no prohibited keys in any snapshot
        ├── VerifyRefineLoopTests.cs       # US4: 2-response mock; max-iteration triggers auto-rollback
        └── IterationLogTests.cs           # US5: confirm/rollback entries contain all required FR-010 fields
```

**Structure Decision**: All new production code lives in `Finance.Mcp` (a dedicated project). `Finance.Business` is unchanged. `Finance.Api` is touched only to register MCP services in `Program.cs`. All MCP tests live in `Finance.Mcp.UnitTests`. A constitution MINOR amendment adding `Finance.Mcp` as a permitted 4th production project must be merged before the project files are created.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| 4th production project (`Finance.Mcp`) — constitution §I names exactly three | MCP is a protocol/infrastructure adapter layer structurally identical in role to `Finance.Api` (HTTP adapter). A dedicated project makes FR-012's domain-type isolation compiler-enforced rather than convention-only, and keeps `Finance.Business` focused on domain logic. | `Finance.Business/Mcp/` sub-namespace: leaves FR-012 enforcement as a naming convention (not a hard `.csproj` boundary); conflates protocol infrastructure with domain logic; and forces `Finance.Business.UnitTests` to cover both domain and MCP protocol behaviour in the same project. The architectural clarity of a dedicated project is permanent; the amendment cost is a one-time process step. |
