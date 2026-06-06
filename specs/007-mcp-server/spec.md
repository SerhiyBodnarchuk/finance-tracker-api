# Feature Specification: MCP Server Integration

**Feature Branch**: `007-mcp-server`

**Created**: 2026-06-02

**Status**: Draft

**Input**: User description: "need to create a mcp server for the application. Extend the Module-1 pet project by integrating a Model Context Protocol (MCP). Produce an MCP client library and a minimal MCP server or stub, define a context schema for the domain, wire agent workflows into verify→refine loops, and compare original vs MCP-enhanced workflow."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send and Retrieve Reconciliation Context (Priority: P1)

A developer (or agent tooling) submits a reconciliation context — containing pending ambiguous transactions and the current category mappings — to the MCP server. The server stores it under a unique ID with a TTL and returns that ID. A subsequent requestAction call uses the stored context to ask the agent what categorisation to apply. The agent's response arrives via receiveResult. The developer then either confirms the suggested categories (persisting the decision) or rolls back (discarding it).

**Why this priority**: This is the core loop the entire MCP integration is built around. Without a working round-trip — send → request → receive → confirm/rollback — nothing else in the feature can be tested or demonstrated.

**Independent Test**: Can be exercised end-to-end with mocked agent responses and no live API calls. Delivers a verifiable, deterministic round-trip that CI can run on every PR.

**Acceptance Scenarios**:

1. **Given** a valid reconciliation context payload, **When** sendContext is called, **Then** the server returns a non-empty context ID and the context is retrievable by that ID until its TTL expires.
2. **Given** a stored context ID, **When** requestAction is called with action type "categorize", **Then** the server returns an action request payload containing the context fields needed for the agent to make a decision.
3. **Given** a pending action request, **When** receiveResult is called with the agent's proposed category assignments, **Then** the result is associated with the originating context and marked as awaiting confirmation.
4. **Given** a received result awaiting confirmation, **When** confirm is called, **Then** the decision is persisted and the context transitions to a confirmed state.
5. **Given** a received result awaiting confirmation, **When** rollback is called, **Then** the suggested changes are discarded and the context reverts to its pre-result state.

---

### User Story 2 - Replay Context Yields Identical Suggestions (Priority: P1)

A developer captures a context snapshot (a serialised point-in-time copy of a reconciliation context), injects it into the MCP server via sendContext, runs requestAction, and receives the same categorisation suggestion that was produced when the original context was first processed. This replay produces byte-identical proposed_changes across three consecutive runs.

**Why this priority**: Reproducibility is the primary acceptance criterion called out in the assignment. If replay does not produce identical outputs, the feature has failed its core requirement regardless of whether the round-trip works.

**Independent Test**: Exercised by a deterministic unit test that provides a fixed context snapshot and a mocked agent response stub, then asserts that the serialised output of three sequential calls is identical.

**Acceptance Scenarios**:

1. **Given** a saved context snapshot JSON file, **When** the snapshot is replayed via sendContext → requestAction with a mocked agent stub, **Then** the agent suggestion returned is identical to the suggestion recorded in the snapshot across all three replay runs.
2. **Given** two replays of the same snapshot with different wall-clock times, **When** results are compared, **Then** proposed_changes and explanation fields are byte-identical (timestamps in non-deterministic metadata are excluded from the comparison).

---

### User Story 3 - Privacy: No Sensitive Fields in Serialised Context (Priority: P1)

Before any context snapshot leaves the server (as a serialised payload, a log entry, or a replay fixture), the serialiser strips or redacts all fields that could carry PII or secrets. A dedicated safety test asserts that a fixed set of prohibited key names is absent from every serialised snapshot produced by the system.

**Why this priority**: Privacy is a non-negotiable constraint called out explicitly in the requirements. A single leaked sensitive field in a log or snapshot would be a defect, not a cosmetic issue.

**Independent Test**: A parameterised unit test that constructs snapshots with deliberately injected sensitive-looking keys and asserts that none appear in the serialised output.

**Acceptance Scenarios**:

1. **Given** a context object whose construction path might include sensitive source data, **When** the context is serialised to a snapshot, **Then** none of the prohibited key names (e.g., `password`, `secret`, `token`, `email`, `name`, `accountNumber`, `iban`, `ssn`) appear anywhere in the serialised JSON.
2. **Given** a snapshot written to the agent iteration log, **When** the log entry is inspected, **Then** it contains no raw PII and all redacted fields are replaced with the literal string `"[REDACTED]"`.

---

### User Story 4 - Verify→Refine Loop with Simulated Failure (Priority: P2)

The CI test suite includes a scenario where the first agent response is deliberately invalid (e.g., it proposes a category that is incompatible with the transaction type). The MCP workflow detects the failure, refines the prompt, and re-issues requestAction. The loop terminates when a valid response is received or a maximum iteration count is reached.

**Why this priority**: Demonstrates that the MCP integration adds resilience beyond a simple one-shot call. Important for the comparison experiment but not blocking for the core round-trip.

**Independent Test**: An integration test with a two-response mock agent: first response fails validation, second response passes. Asserts that exactly two requestAction calls were made and that the final result is the second (valid) response.

**Acceptance Scenarios**:

1. **Given** a mock agent that returns an invalid categorisation on the first call and a valid one on the second, **When** the verify→refine loop runs, **Then** the loop makes exactly two requestAction calls and the confirmed result reflects the second response.
2. **Given** a mock agent that returns invalid categorisations on every call, **When** the loop reaches the configured maximum iteration limit, **Then** the loop terminates, the context is rolled back, and the failure is recorded in the agent iteration log.

---

### User Story 5 - Agent Iteration Log (Priority: P2)

Every MCP interaction — whether a live run or a replay — appends a structured entry to a dedicated MCP iteration log (`ai-artifacts/mcp_iteration_log.txt`). Each entry captures the prompt, the exact context snapshot hash, the model name and settings used, the agent output, the diff accepted, and a one-line acceptance or rejection reason. This log is intentionally separate from the existing project agent log.

**Why this priority**: Required for the comparison experiment and for audit purposes. Enables post-hoc variance analysis across runs.

**Independent Test**: A unit test that triggers one confirm and one rollback through mocked services, then asserts that two log entries were appended with all required fields populated.

**Acceptance Scenarios**:

1. **Given** a completed confirm operation, **When** the log is read, **Then** the most recent entry contains: timestamp, prompt, contextSnapshotHash, modelName, agentOutput, acceptedDiff, and decisionReason.
2. **Given** a completed rollback operation, **When** the log is read, **Then** the entry contains the same required fields, with acceptedDiff as null and decisionReason explaining why the change was rejected.

---

### Edge Cases

- What happens when sendContext is called with an empty or null pending-transactions list? Server should reject with a validation error — a context with no transactions has nothing to reconcile.
- What happens when requestAction is called with an expired or unknown context ID? Server returns a not-found / expired error; no action is created.
- What happens when receiveResult references an action ID that was never issued? Server returns a not-found error; no state change occurs.
- What happens when confirm is called on a context that has already been confirmed? Server is idempotent — returns success without creating a duplicate decision.
- What happens when rollback is called on an already-confirmed context? Server rejects with a conflict error — confirmed decisions cannot be rolled back.
- What happens when the context snapshot contains more fields than the current schema version? Extra unknown fields are dropped during deserialisation and do not propagate to the agent.
- What happens if the agent returns a proposed change that touches a business-logic rule (e.g., redefining a category type)? The confirm gate requires explicit human approval before the change is accepted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose five MCP operations: `sendContext`, `requestAction`, `receiveResult`, `confirm`, and `rollback`, each accepting and returning strongly-typed, versioned payloads.
- **FR-002**: The MCP server MUST assign each submitted context a unique identifier and a TTL; contexts not confirmed or rolled back before TTL expiry MUST be automatically discarded.
- **FR-003**: The context schema MUST include at minimum: category mappings (id → name, type) and pending transaction batches (id, amount, date, description — description marked redacted in serialised output). No user profile section is included — the app is single-user with no authentication or identity concept.
- **FR-004**: The system MUST provide a context snapshot mechanism that serialises a point-in-time copy of a context to a deterministic JSON representation, suitable for storage as a replay fixture.
- **FR-005**: Replaying an identical context snapshot through `sendContext` → `requestAction` with an equivalent mocked agent stub MUST produce byte-identical `proposed_changes` and `explanation` fields across three consecutive invocations.
- **FR-006**: The serialiser MUST strip or replace with `"[REDACTED]"` any field whose key matches a configured list of prohibited sensitive-field names before the snapshot is written to a log, file, or external payload.
- **FR-007**: A safety unit test MUST assert that no prohibited key name appears in the serialised output of any context snapshot produced by the system under test.
- **FR-008**: The system MUST support a verify→refine loop: if the agent's proposed categorisation fails validation, the loop MUST re-issue `requestAction` with a refined prompt up to a configurable maximum iteration count, after which the context is automatically rolled back.
- **FR-009**: Any `confirm` call that would apply a change to a business-logic rule (category type redefinition, transaction type override) MUST be withheld pending explicit human approval; the context remains in a pending-approval state until approved or rejected.
- **FR-010**: The system MUST append a structured entry to a dedicated MCP iteration log (`ai-artifacts/mcp_iteration_log.txt`) on every `confirm` or `rollback` call, containing: ISO-8601 timestamp, prompt text, context snapshot hash (SHA-256 of the serialised snapshot), model name and temperature, agent output, accepted diff (or null), and a one-line decision reason. This log is separate from the existing `agent_log.txt` and MUST NOT write to it.
- **FR-011**: The context schema document MUST describe each field's purpose and TTL rationale; sensitive fields MUST be annotated as redacted.
- **FR-012**: The MCP system MUST be implemented as a dedicated `Finance.Mcp` project that sits alongside `Finance.Api` — both reference `Finance.Business` but neither references the other. Within `Finance.Mcp`, the client interface (`IMcpClient`) MUST only accept and return schema types defined in `Finance.Mcp` — it MUST NOT expose `Finance.Business` DTOs or `Finance.Data` entities at its boundary. This structural separation (separate project, not just a namespace) is what makes FR-012 verifiable by a dependency check.
- **FR-013**: The logging retention policy MUST limit the number of stored log entries and the maximum age of any entry; entries beyond the limit MUST be pruned on each write.
- **FR-014**: CI tests MUST cover: MCP round-trip (all five operations in sequence), replay determinism (three consecutive replays with identical context), privacy safety (no sensitive keys in snapshots), and at least one verify→refine loop with a simulated first-response failure.

### Key Entities

- **McpContext**: Represents the full reconciliation context stored on the MCP server. Carries a unique `id`, schema `version`, `ttl` (absolute expiry instant), `categoryMappings` (list of id/name/type tuples), `pendingTransactions` (list of transaction stubs with description redacted), `status` (active / pending-approval / confirmed / rolled-back / expired), and `createdAt`.
- **ReconciliationContext**: The domain-specific payload inside `McpContext`. Contains `ambiguousTransactionIds` (subset of pending transactions requiring categorisation), `suggestedCategoryIds` (proposed assignments, populated after `receiveResult`), and `decisionStatus`.
- **AgentActionRequest**: Produced by `requestAction`. Carries `actionId`, `contextId`, `actionType` (categorize / validate), `iterationNumber`, `promptText`, and the minimal context fields the agent needs to answer.
- **AgentResult**: Produced by `receiveResult`. Carries `actionId`, `proposed_changes` (structured diff), `explanation` (one-line string), and `receivedAt`.
- **ContextSnapshot**: An immutable, serialised point-in-time copy of a `McpContext`. Carries the context `id`, `serialisedAt` timestamp, a SHA-256 `hash` of the serialised JSON, and the full redacted JSON body. Used as a replay fixture.
- **IterationLogEntry**: One record appended per `confirm` or `rollback`. Contains all fields required by FR-010. Stored append-only in the agent iteration log, subject to the retention policy (FR-013).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Replaying the same `ContextSnapshot` fixture three consecutive times produces byte-identical `proposed_changes` and `explanation` fields on all three runs (0% output variance).
- **SC-002**: All five MCP operations complete a full round-trip within a single deterministic test scenario with zero test failures across three CI runs.
- **SC-003**: The privacy safety test asserts that zero prohibited key names appear in any serialised context snapshot; this test passes on every CI run.
- **SC-004**: The verify→refine loop test demonstrates that the system re-issues `requestAction` exactly once when the first mocked response is invalid, and terminates correctly when the maximum iteration limit is exceeded.
- **SC-005**: Every `confirm` and `rollback` call in the test suite produces a correctly structured `IterationLogEntry` with all required fields populated (no nulls for mandatory fields).
- **SC-006**: The `Finance.Mcp` project has no direct reference to `Finance.Data`, and `Finance.Mcp`'s client interface exposes no `Finance.Business` DTOs or `Finance.Data` entities at its boundary — verifiable by inspecting the `.csproj` and public interface signatures.

## Assumptions

- The MCP system lives in a dedicated **`Finance.Mcp` project** — a 4th production project alongside `Finance.Api`, both referencing `Finance.Business`. This requires a constitution MINOR amendment before implementation begins. The dependency graph is: `Finance.Api → Finance.Business → Finance.Data` and `Finance.Mcp → Finance.Business → Finance.Data`; `Finance.Api` and `Finance.Mcp` do not reference each other.
- All MCP operations are in-process method calls (not HTTP endpoints), consistent with the project's no-external-dependencies constraint.
- Context snapshots are stored in-memory only during a session; the replay fixture JSON files are written to disk under `specs/007-mcp-server/contracts/snapshots/` for use in tests but are not persisted at runtime.
- No user profile is included in the context — the app is single-user with no authentication, so there is no identity concept to represent.
- Context TTL defaults to 30 minutes of inactivity; the value is configurable for tests (can be set to a short duration to test expiry behaviour).
- Agent responses in CI tests are provided by deterministic mocks — no live Claude API calls are made during automated testing.
- The reconciliation context covers transaction categorisation decisions only; balance queries, reporting, and export operations are out of scope for this feature.
- Transaction descriptions are treated as potentially sensitive and are always redacted (`"[REDACTED]"`) in serialised snapshots and log entries, even though the in-memory entity retains the original value for business logic.
- The `proposed_changes` field in `AgentResult` follows the structured JSON format `{ "proposed_changes": [...], "explanation": "..." }` consistent with the agent-prompt schema described in the task brief.
- The comparison experiment (3 original-flow runs vs 3 MCP-flow runs with variance metrics) is a separate deliverable and is out of scope for this specification; this spec covers the MCP server and client only.
- The MCP iteration log is a dedicated append-only file at `ai-artifacts/mcp_iteration_log.txt`, separate from the existing `ai-artifacts/agent_log.txt`. The two logs are never written to interchangeably.
