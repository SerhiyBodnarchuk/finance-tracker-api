# Specification Quality Checklist: Domain Entities and API DTOs

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-05-19
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.
- **Revision 3 (2026-05-19, post-feedback)**: `ReportResult` shape pivoted again. The previous revision had collapsed it to a flat array of `TransactionResponse`; that was a literal but incorrect reading of "result is just all transactions for specific period". The user clarified they wanted the summary the original README had — totals plus per-category breakdown — *instead of* returning the underlying transactions. Final shape now: `ReportResult { type, period (string descriptor), incomeTotal, expenseTotal, netTotal, categoryBreakdown }` with no `transactions` array. `CategoryBreakdownItem` is reduced to `{ category, total (signed) }` — no direction field (sign carries direction), no transaction count. Multi-category attribution: a transaction contributes to *each* attached category's breakdown line (documented divergence-from-netTotal in the assumptions). FR-018 / FR-019 / FR-020 / FR-021 / FR-022 are the new "Report response shape" group; the later FRs were renumbered up by two slots (now ending at FR-030). Acceptance scenarios in US5 were rewritten and US5's title changed back to "summary with totals and a per-category breakdown". Re-validated below; all 16 items still pass.
- **Revision 2 (2026-05-19, post-feedback)**: spec further simplified per user direction:
    1. Transactions now support **multiple categories** (`categoryIds: int[]`, non-empty) — FR-001, FR-009, FR-015 updated; new acceptance scenarios in US1 and US4.
    2. `ReportType` is now an enum (`Period`, `IsoWeek`) rather than an arbitrary string — new FR-006.
    3. `ReportRequest` envelope rewritten: `{ type: ReportType, data: object }` where `data` is type-specific. Two new request DTOs introduced: `PeriodReportData { start, end }` and `IsoWeekReportData { week }` — new FR-011..013.
    4. `ReportResult` collapsed to **just a read-only collection of `TransactionResponse`**. Removed: `incomeTotal`, `expenseTotal`, `netTotal`, `categoryBreakdown`, `currency`, `ReportPeriod` envelope, `CategoryBreakdownItem` DTO. FR-018/FR-019 rewritten; old FR-016 (CategoryBreakdownItem) deleted.
    5. Aggregation/mapping explicitly placed in Business layer (FR-021 reinforced); API layer never sees a domain entity.
    6. Renames: `parameters` → `data`, `from`/`to` → `start`/`end`, `categoryId` → `categoryIds`, `TransactionDirection` enum renamed back to `TransactionType` (matching README/CLAUDE.md and the existing `Finance.Data` convention), `CategoryCompatibility` enum renamed back to `CategoryType`.
  Re-validated below; all 16 checklist items still pass. The spec's "Out of scope" block was strengthened — rejecting `start > end`, malformed `IsoWeek.week`, empty `categoryIds`, duplicate categories, and incompatible category/direction combinations is now explicitly out of scope for this feature.
- **Revision 1 (2026-05-19, post-feedback)**: spec updated per user direction — int IDs replacing GUIDs; full-datetime timestamps on transactions replacing day-level dates; currency removed from `ReportResult`; `TransactionDirection` / `CategoryCompatibility` demoted from entities to enum attributes; DTOs relocated from API layer to Business layer with explicit mapper requirement; reports declared ad-hoc/never persisted. Six new requirements added (FR-020..022) and three success criteria updated/added (SC-003 timestamp precision, SC-006 ad-hoc reports). Re-validated below.
- **Validation pass 1 (2026-05-19)**: All items pass. Specific verification notes below.
  - *Content Quality / No implementation details*: The spec uses "shape", "contract", "value" instead of "record", "class", "struct". The single mention of records appears only in the Assumptions section, explicitly tagged as a stakeholder-stated implementation preference rather than a requirement — this is the recommended way to surface the user's stated preference without baking it into the spec.
  - *Requirement Completeness / Testable & unambiguous*: Each FR uses MUST/MUST NOT and refers to closed sets of values (e.g., "exactly two documented values", "exactly three documented values") or to concrete payloads in `README.md` so any reviewer can write a pass/fail test against it.
  - *Success Criteria / Technology-agnostic*: SC-001 through SC-005 reference behaviors (round-trip parity, field-for-field match against README examples, time-to-orientation) rather than tools or frameworks. The mention of "JSON serialization" is a wire-format requirement (JSON is part of the product surface defined in `README.md`), not an implementation choice.
  - *Edge cases identified*: Boundary dates (week edges), category compatibility "Both", non-ASCII names, high-precision decimal input, empty report periods, and one-day report periods are all enumerated.
  - *Scope is clearly bounded*: FR-022 / FR-023 / FR-024 explicitly list what this feature does *not* deliver (storage, validation enforcement, aggregation, export).
