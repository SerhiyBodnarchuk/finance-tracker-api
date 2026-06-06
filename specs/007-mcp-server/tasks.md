# Tasks: MCP Server Integration

**Input**: Design documents from `specs/007-mcp-server/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — spec.md FR-007 and FR-014 explicitly require CI test coverage.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

---

## Phase 1: Setup (Project Scaffolding)

**Purpose**: Constitution amendment and project file creation. No production code is written until T001 (the amendment) is merged.

**⚠️ CRITICAL**: T001 must be merged before any project files are created. T002–T006 must all complete before Phase 2 begins.

- [ ] T001 File constitution MINOR amendment in `.specify/memory/constitution.md` to add `Finance.Mcp` as a permitted 4th production project and `Finance.Mcp.UnitTests` as a 5th test project; document the sibling dependency pattern (`Finance.Api` and `Finance.Mcp` both reference `Finance.Business`, neither references the other)
- [ ] T002 Create `src/backend/FinanceTracker/Finance.Mcp/Finance.Mcp.csproj` as a `net10.0` class library with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`; add project to `src/backend/FinanceTracker/FinanceTracker.slnx`
- [ ] T003 Create `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/Finance.Mcp.UnitTests.csproj` as a `net10.0` test project; add project to `src/backend/FinanceTracker/FinanceTracker.slnx`
- [ ] T004 Add `<ProjectReference Include="..\Finance.Business\Finance.Business.csproj" />` to `src/backend/FinanceTracker/Finance.Mcp/Finance.Mcp.csproj`
- [x] T005 ~~Add Finance.Mcp reference to Finance.Api.csproj~~ — **REMOVED**: Finance.Api has no HTTP endpoints for MCP and no controllers that consume IMcpServer; Finance.Api and Finance.Mcp are true siblings with no cross-reference needed
- [ ] T006 Add test dependencies to `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/Finance.Mcp.UnitTests.csproj`: `xunit.v3`, `Microsoft.NET.Test.Sdk` (17.x), `xunit.runner.visualstudio` (3.x), `Moq` (4.20.x); add `<ProjectReference Include="..\..\Finance.Mcp\Finance.Mcp.csproj" />`

**Checkpoint**: `dotnet restore` and `dotnet build` succeed with zero errors across the full solution.

---

## Phase 2: Foundational (Schema Types, Interfaces, and Infrastructure Skeletons)

**Purpose**: All schema types and service interfaces that every user story depends on. Must be complete before any user story implementation begins.

**⚠️ CRITICAL**: All user story phases depend on this phase.

### Enums

- [ ] T007 [P] Create `ContextStatus` enum in `src/backend/FinanceTracker/Finance.Mcp/Schema/ContextStatus.cs` with values: `Active`, `PendingApproval`, `Confirmed`, `RolledBack`, `Expired`
- [ ] T008 [P] Create `ActionType` enum in `src/backend/FinanceTracker/Finance.Mcp/Schema/ActionType.cs` with values: `Categorize`, `Validate`

### Value-Object Records (no dependencies on other schema types)

- [ ] T009 [P] Create `CategoryMappingItem` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/CategoryMappingItem.cs` with fields: `int Id`, `string Name`, `string Type`
- [ ] T010 [P] Create `PendingTransactionItem` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/PendingTransactionItem.cs` with fields: `int Id`, `decimal Amount`, `string TransactionType`, `DateTime Date`, `string Description`; add XML doc comment noting Description is always `[REDACTED]` in serialised output
- [ ] T011 [P] Create `ProposedChange` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/ProposedChange.cs` with fields: `int TransactionId`, `string TargetField`, `object ProposedValue`
- [ ] T012 [P] Create `ConfirmResult` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/ConfirmResult.cs` with fields: `bool RequiresApproval`, `ContextStatus Status`
- [ ] T013 [P] Create `IterationLogEntry` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/IterationLogEntry.cs` with fields: `string Timestamp`, `string Prompt`, `string ContextSnapshotHash`, `string ModelName`, `string AgentOutput`, `string? AcceptedDiff`, `string DecisionReason`

### Composite Records (depend on value-object records)

- [ ] T014 Create `ReconciliationContext` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/ReconciliationContext.cs` with fields: `IReadOnlyList<int> AmbiguousTransactionIds`, `IReadOnlyDictionary<int, IReadOnlyList<int>> SuggestedCategoryIds`, `ContextStatus DecisionStatus`
- [ ] T015 Create `McpContext` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/McpContext.cs` with fields: `string Id`, `int Version`, `DateTime Ttl`, `IReadOnlyList<CategoryMappingItem> CategoryMappings`, `IReadOnlyList<PendingTransactionItem> PendingTransactions`, `ReconciliationContext Reconciliation`, `ContextStatus Status`, `DateTime CreatedAt`
- [ ] T016 Create `AgentActionRequest` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/AgentActionRequest.cs` with fields: `string ActionId`, `string ContextId`, `ActionType ActionType`, `int IterationNumber`, `string PromptText`, `IReadOnlyDictionary<string, object> ContextFields`
- [ ] T017 Create `AgentResult` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/AgentResult.cs` with fields: `string ActionId`, `IReadOnlyList<ProposedChange> ProposedChanges`, `string Explanation`, `DateTime ReceivedAt`
- [ ] T018 [P] Create `ContextSnapshot` record in `src/backend/FinanceTracker/Finance.Mcp/Schema/ContextSnapshot.cs` with fields: `string ContextId`, `DateTime SerialisedAt`, `string Hash`, `string RedactedJson`

### Interfaces and Infrastructure Skeletons

- [ ] T019 Create `IMcpContextStore` interface and `McpContextStore` implementation in `src/backend/FinanceTracker/Finance.Mcp/McpContextStore.cs`; `McpContextStore` uses `ConcurrentDictionary<string, McpContext>` and checks TTL passively on every `Get` call (returns null and sets status to `Expired` if `context.Ttl < DateTime.UtcNow`)
- [ ] T020 Create `IContextRedactor` interface and `ContextRedactor` skeleton in `src/backend/FinanceTracker/Finance.Mcp/Redaction/ContextRedactor.cs`; define `static readonly HashSet<string> ProhibitedKeys` (case-insensitive) with all 9 keys from `specs/007-mcp-server/contracts/sensitive-fields.md`; implement `Redact(McpContext)` replacing `description` on all `PendingTransactionItems` with `"[REDACTED]"` before serialisation (full enforcement tested in Phase 5)
- [ ] T021 Create `IMcpIterationLogger` interface and `McpIterationLogger` skeleton in `src/backend/FinanceTracker/Finance.Mcp/Logging/McpIterationLogger.cs`; implement append-only write to `ai-artifacts/mcp_iteration_log.txt` using the `--- MCP ENTRY START ---` / `--- MCP ENTRY END ---` fenced format from D7 in plan.md (retention policy enforcement tested in Phase 7)
- [ ] T022 Create `IMcpServer` interface in `src/backend/FinanceTracker/Finance.Mcp/IMcpServer.cs` declaring all six operations per `specs/007-mcp-server/contracts/mcp-operations.md`: `SendContext`, `RequestAction`, `ReceiveResult`, `Confirm`, `Rollback`, `ApproveConfirm`
- [ ] T023 Create `IMcpClient` interface and `McpClient` implementation in `src/backend/FinanceTracker/Finance.Mcp/Client/McpClient.cs`; `IMcpClient` exposes the five primary operations (no `ApproveConfirm`) using only `Finance.Mcp` schema types; `McpClient` delegates to the injected `IMcpServer`

**Checkpoint**: `dotnet build` succeeds. All schema types compile. Interfaces exist but implementations may be skeletal (throw `NotImplementedException`).

---

## Phase 3: User Story 1 — Send and Retrieve Reconciliation Context (Priority: P1) 🎯 MVP

**Goal**: Full working round-trip through all five MCP operations, registered in DI, with a test that exercises the complete `sendContext → requestAction → receiveResult → confirm → rollback` flow.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~McpServerRoundTripTests"` — all scenarios in that class must pass.

- [ ] T024 [US1] Implement `McpServer.SendContext` in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: validate non-empty `pendingTransactions` and non-empty `ambiguousIds` subset; assign `Id` (e.g., `Guid.NewGuid().ToString("N")`); set `Status = Active`, `Ttl = DateTime.UtcNow.AddMinutes(ttlMinutes ?? 30)`; store via `IMcpContextStore`; return context ID
- [ ] T025 [US1] Implement `McpServer.RequestAction` in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: retrieve context by ID; return `InvalidOperationException` if not found, expired, or in terminal state; check iteration count against configured max; assign `ActionId`; build `ContextFields` from redacted context (call `IContextRedactor.Redact`); increment `IterationNumber`; return `AgentActionRequest`
- [ ] T026 [US1] Implement `McpServer.ReceiveResult` in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: look up pending action by `ActionId`; throw if not found or already resolved; associate `AgentResult` with its context; mark action as resolved
- [ ] T027 [US1] Implement `McpServer.Confirm` in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: check `ProposedChanges` for `TargetField` values `"categoryType"` or `"transactionType"`; if found, set `Status = PendingApproval` and return `ConfirmResult { RequiresApproval = true }`; otherwise set `Status = Confirmed`; call `IMcpIterationLogger.Log` in both branches; return `ConfirmResult`
- [ ] T028 [US1] Implement `McpServer.Rollback` in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: throw `InvalidOperationException` if context is already `Confirmed`; set `Status = RolledBack`; call `IMcpIterationLogger.Log` with `AcceptedDiff = null` and the provided reason
- [ ] T029 [US1] Implement `McpServer.ApproveConfirm` in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: throw `InvalidOperationException` if context is not in `PendingApproval`; set `Status = Confirmed`; call `IMcpIterationLogger.Log`
- [x] T030 ~~Register MCP services in Finance.Api/Program.cs~~ — **REMOVED**: Finance.Mcp is a standalone sibling; Finance.Api has no MCP endpoints and must not reference Finance.Mcp
- [ ] T031 [US1] Write `McpServerRoundTripTests` in `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/McpServerRoundTripTests.cs` using strict Moq on `IMcpContextStore`, `IContextRedactor`, `IMcpIterationLogger`; cover all five acceptance scenarios from spec US1 plus the idempotent-confirm and reject-rollback-on-confirmed edge cases

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~McpServerRoundTripTests"` — all tests green.

---

## Phase 4: User Story 2 — Replay Context Yields Identical Suggestions (Priority: P1)

**Goal**: A fixed `ContextSnapshot` fixture replayed three consecutive times produces byte-identical `ProposedChanges` and `Explanation`.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~ContextSnapshotReplayTests"` — all three replay assertions pass.

- [ ] T032 [P] [US2] Implement snapshot serialisation in `src/backend/FinanceTracker/Finance.Mcp/Schema/ContextSnapshot.cs` (or a `ContextSnapshotSerializer` helper): use `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and `WriteIndented = false`; compute SHA-256 hash from `Encoding.UTF8.GetBytes(json)`; expose as `ContextSnapshot ContextSnapshot.Create(McpContext redactedContext)`
- [ ] T033 [US2] Add `TakeSnapshot(string contextId)` method to `IMcpServer` and implement in `McpServer.cs`: retrieve context, call `IContextRedactor.Redact`, serialise to `ContextSnapshot`; return the snapshot
- [ ] T034 [US2] Write `ContextSnapshotReplayTests` in `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/ContextSnapshotReplayTests.cs`: load `specs/007-mcp-server/contracts/snapshots/reconciliation-example.json` as a fixed input; call `SendContext → RequestAction` with a deterministic mock agent stub three times; assert `ProposedChanges` and `Explanation` are byte-identical across all three runs; assert that two snapshots of the same context at different times produce identical `Hash` values

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~ContextSnapshotReplayTests"` — all tests green.

---

## Phase 5: User Story 3 — Privacy: No Sensitive Fields in Serialised Context (Priority: P1)

**Goal**: A parameterised safety test confirms no prohibited key name appears in any serialised snapshot produced by the system.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~ContextRedactionSafetyTests"` — all parameterised cases pass.

- [ ] T035 [US3] Complete `ContextRedactor.Redact` in `src/backend/FinanceTracker/Finance.Mcp/Redaction/ContextRedactor.cs`: after replacing `description` fields with `"[REDACTED]"`, serialise to JSON and verify that no key in `ProhibitedKeys` appears in the resulting JSON string (case-insensitive scan); if any prohibited key is found, throw `InvalidOperationException` with the offending key name — this is the runtime safety net; the unit test is the static assertion
- [ ] T036 [US3] Write parameterised `ContextRedactionSafetyTests` in `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/ContextRedactionSafetyTests.cs`: one `[Theory]` with `[InlineData]` per prohibited key (all 9 from `specs/007-mcp-server/contracts/sensitive-fields.md`); for each key, construct a context snapshot and assert the key does not appear anywhere in the serialised JSON; also assert `description` fields on all `PendingTransactionItems` are `"[REDACTED]"` in the serialised output

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~ContextRedactionSafetyTests"` — all 9+ parameterised test cases green.

---

## Phase 6: User Story 4 — Verify→Refine Loop with Simulated Failure (Priority: P2)

**Goal**: The server re-issues `RequestAction` when the agent's first response is invalid, and auto-rolls-back when the iteration limit is exceeded.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~VerifyRefineLoopTests"` — both acceptance scenarios pass.

- [ ] T037 [US4] Implement iteration counter and loop enforcement in `src/backend/FinanceTracker/Finance.Mcp/McpServer.cs`: make max iteration count configurable (default: 3) via constructor parameter; in `RequestAction`, check current iteration number; if at limit, call `Rollback` internally, append a log entry with reason `"max iterations exceeded"`, and return a result indicating loop exhaustion (a new `ActionRequestResult` type with `LoopExhausted = true`); update `IMcpServer` and `AgentActionRequest` / return type accordingly
- [ ] T038 [US4] Write `VerifyRefineLoopTests` in `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/VerifyRefineLoopTests.cs`: scenario 1 — mock agent returns invalid categorisation on first call (incompatible category type) and valid on second; assert exactly two `RequestAction` calls were made and the final `Confirm` succeeds; scenario 2 — mock agent always returns invalid; assert the loop auto-rolls-back at the configured max and the log entry contains `"max iterations exceeded"`

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~VerifyRefineLoopTests"` — both tests green.

---

## Phase 7: User Story 5 — Agent Iteration Log (Priority: P2)

**Goal**: Every `Confirm` and `Rollback` call appends a structurally complete entry to `ai-artifacts/mcp_iteration_log.txt`; the retention policy prunes entries beyond the configured cap.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~IterationLogTests"` — all assertions pass.

- [ ] T039 [US5] Complete `McpIterationLogger` in `src/backend/FinanceTracker/Finance.Mcp/Logging/McpIterationLogger.cs`: implement `Log(IterationLogEntry entry)` as a file-append using the fenced block format (`--- MCP ENTRY START ---` / `--- MCP ENTRY END ---`, `Key: Value` lines for all FR-010 fields); after appending, apply retention: parse existing entries, discard any older than 30 days or beyond the 500-entry cap, rewrite the file with surviving entries only
- [ ] T040 [US5] Write `IterationLogTests` in `src/backend/FinanceTracker/tests/Finance.Mcp.UnitTests/IterationLogTests.cs`: use a temp file path so tests are hermetic; trigger one `Confirm` and one `Rollback` through a real `McpIterationLogger` instance; assert two entries are appended; assert the confirm entry has all FR-010 fields non-null; assert the rollback entry has `AcceptedDiff` as null; assert the retention pruning removes entries that exceed the configured cap when a new write occurs

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~IterationLogTests"` — all tests green.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Verify the full solution builds and all tests pass; log the implementation.

- [ ] T041 [P] Run `dotnet build --no-restore -c Release` from `src/backend/FinanceTracker/` and confirm zero errors and zero warnings
- [ ] T042 Run `dotnet test --no-build -c Release` from `src/backend/FinanceTracker/` and confirm all tests in all five test projects pass (including the new `Finance.Mcp.UnitTests`)
- [ ] T043 Append an entry to `ai-artifacts/agent_log.txt` documenting the MCP server implementation: timestamp, model, prompt summary, decision (accepted), reason (constitution amendment + Finance.Mcp project introduced to separate protocol from domain logic)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately; T001 must complete before T002–T006
- **Phase 2 (Foundational)**: Requires Phase 1 complete — blocks all user story phases
- **Phase 3 (US1)**: Requires Phase 2 complete — no dependency on US2–US5
- **Phase 4 (US2)**: Requires Phase 3 complete (needs working `SendContext`/`RequestAction`)
- **Phase 5 (US3)**: Requires Phase 2 complete — independent of US1, US2 (tests `ContextRedactor` in isolation)
- **Phase 6 (US4)**: Requires Phase 3 complete (extends `McpServer` loop behaviour)
- **Phase 7 (US5)**: Requires Phase 3 complete (tests logger via `Confirm`/`Rollback` paths)
- **Phase 8 (Polish)**: Requires all desired user story phases complete

### User Story Dependencies

| Story | Depends On | Can Parallelise With |
|-------|------------|----------------------|
| US1 (P1) | Phase 2 | US3 (shares nothing) |
| US2 (P1) | US1 (needs `SendContext`) | US3, US4 setup |
| US3 (P1) | Phase 2 | US1, US4, US5 |
| US4 (P2) | US1 (extends `McpServer`) | US5 |
| US5 (P2) | US1 (uses `Confirm`/`Rollback`) | US4 |

### Within Each Phase

- Enum tasks (T007, T008) before composite records (T014, T015)
- Value-object records (T009–T013) can all run in parallel
- `McpContextStore` (T019) before `McpServer` (T024–T029)
- All `McpServer` operation tasks (T024–T029) depend on T019–T023 but are themselves sequential (all edit the same file)
- Test tasks always follow their implementation task

---

## Parallel Example: Phase 2 (Foundational)

```
# Launch all independent schema type tasks simultaneously:
T007 — ContextStatus enum
T008 — ActionType enum
T009 — CategoryMappingItem record
T010 — PendingTransactionItem record
T011 — ProposedChange record
T012 — ConfirmResult record
T013 — IterationLogEntry record

# Then (after T007 is done):
T014 — ReconciliationContext
T015 — McpContext          (after T014)
T016 — AgentActionRequest  (after T008)
T017 — AgentResult         (after T011)
T018 — ContextSnapshot

# Then in parallel:
T019 — McpContextStore
T020 — ContextRedactor
T021 — McpIterationLogger

# Then sequentially:
T022 — IMcpServer interface (after T015, T016, T017)
T023 — McpClient           (after T022)
```

---

## Implementation Strategy

### MVP (User Story 1 Only)

1. Complete Phase 1 (Setup) → project files exist
2. Complete Phase 2 (Foundational) → schema types and interfaces compiled
3. Complete Phase 3 (US1) → full round-trip working
4. **STOP and VALIDATE**: run `dotnet test --filter "FullyQualifiedName~McpServerRoundTripTests"`
5. Merge if all green

### Incremental Delivery

1. MVP (US1) → round-trip tests green → merge
2. Add US3 (privacy safety) → can be done before or after US2 → merge
3. Add US2 (replay) → replay tests green → merge
4. Add US4 (verify→refine loop) → loop tests green → merge
5. Add US5 (iteration log) → log tests green → merge
6. Phase 8 (polish) → full `dotnet test` clean → merge

---

## Notes

- `[P]` tasks touch distinct files and have no unresolved dependencies — safe to run in parallel
- `[Story]` label traces each task back to the acceptance scenario it satisfies
- T001 (constitution amendment) is a pre-requisite for all project-file creation — never skip it
- All tests use strict Moq (`MockBehavior.Strict`) — unexpected calls are test failures
- `Finance.Api.csproj` adding a reference to `Finance.Mcp` (T005) is intentional and correct — `Finance.Api` is the composition root and must reference `Finance.Mcp` to register its services
- The `mcp_iteration_log.txt` retention rewrite in T039 is the only place that reads before writing — all other log writes are pure appends per the constitution append-only rule
