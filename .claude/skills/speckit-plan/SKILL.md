---
name: "speckit-plan"
description: "Execute the implementation planning workflow using the plan template to generate design artifacts."
argument-hint: "Optional guidance for the planning phase"
compatibility: "Requires spec-kit project structure with .specify/ directory"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/plan.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

**Check for extension hooks (before planning)**:
- Check if `.specify/extensions.yml` exists in the project root.
- If it exists, read it and look for entries under the `hooks.before_plan` key
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- When constructing slash commands from hook command names, replace dots (`.`) with hyphens (`-`). For example, `speckit.git.commit` → `/speckit-git-commit`.
- For each executable hook, output the following based on its `optional` flag:
  - **Optional hook** (`optional: true`):
    ```
    ## Extension Hooks

    **Optional Pre-Hook**: {extension}
    Command: `/{command}`
    Description: {description}

    Prompt: {prompt}
    To execute: `/{command}`
    ```
  - **Mandatory hook** (`optional: false`):
    ```
    ## Extension Hooks

    **Automatic Pre-Hook**: {extension}
    Executing: `/{command}`
    EXECUTE_COMMAND: {command}

    Wait for the result of the hook command before proceeding to the Outline.
    ```
- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently

## Outline

1. **Setup**: Run `.specify/scripts/powershell/setup-plan.ps1 -Json` from repo root and parse JSON for FEATURE_SPEC, IMPL_PLAN, SPECS_DIR, BRANCH. For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

2. **Load context**: Read FEATURE_SPEC and `.specify/memory/constitution.md` (active rules only — do NOT read `.specify/memory/constitution-history.md`). Load IMPL_PLAN template (already copied).

3. **Execute plan workflow**: Follow the structure in IMPL_PLAN template to:
   - Fill Technical Context (mark unknowns as "NEEDS CLARIFICATION")
   - Fill Constitution Check section from constitution
   - Evaluate gates (ERROR if violations unjustified)
   - Phase 0: Assess complexity and fill `## Research & Decisions` (see Phase 0 rules below)
   - Phase 0: Fill `## Artifact Generation Decisions` table
   - Phase 1: Generate only the conditional artifacts declared "Yes" in that table
   - Phase 1: Update agent context by running the agent script
   - Re-evaluate Constitution Check post-design

4. **Stop and report**: Command ends after Phase 2 planning. Report branch, IMPL_PLAN path, and generated artifacts.

5. **Check for extension hooks**: After reporting, check if `.specify/extensions.yml` exists in the project root.
   - If it exists, read it and look for entries under the `hooks.after_plan` key
   - If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
   - Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
   - For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
     - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
     - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
   - When constructing slash commands from hook command names, replace dots (`.`) with hyphens (`-`). For example, `speckit.git.commit` → `/speckit-git-commit`.
   - For each executable hook, output the following based on its `optional` flag:
     - **Optional hook** (`optional: true`):
       ```
       ## Extension Hooks

       **Optional Hook**: {extension}
       Command: `/{command}`
       Description: {description}

       Prompt: {prompt}
       To execute: `/{command}`
       ```
     - **Mandatory hook** (`optional: false`):
       ```
       ## Extension Hooks

       **Automatic Hook**: {extension}
       Executing: `/{command}`
       EXECUTE_COMMAND: {command}
       ```
   - If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently

## Phases

### Phase 0: Research & Decisions

1. **Assess complexity**: Decide whether this feature warrants a separate `research.md`.
   - **Create `research.md`** when the feature has many unknowns, non-obvious technology choices, significant trade-offs, external integration research, or five or more distinct decisions worth documenting.
   - **Write decisions inline** in the `## Research & Decisions` section of `plan.md` when the feature is straightforward, all choices are determined by the constitution and spec, and there are only a few decisions to note.
   - **Omit the section entirely** if the feature is so simple there are no non-obvious decisions to record.

2. **Extract unknowns from Technical Context** and resolve them:
   - For each NEEDS CLARIFICATION → research the answer
   - For each dependency or integration → find best practices
   - Document each resolved decision in the chosen location (inline or research.md)

3. **Decision format** (used whether inline or in research.md):
   ```
   ### D1 — [Decision title]
   **Chosen**: [what was decided]
   **Rationale**: [why]
   **Alternatives considered**: [what else was evaluated and why rejected]
   ```

4. **Fill `## Artifact Generation Decisions`** table in plan.md, answering Yes/No for each optional artifact based on these rules:
   - `research.md` → Yes if step 1 determined separate file; No if inline or omitted
   - `data-model.md` → Yes if the feature introduces or modifies owned domain entities, DTOs, enums, or schemas; No for infra/tooling/CI features
   - `contracts/` → Yes if the feature defines a communication boundary (HTTP endpoint, service interface, event schema, CLI contract, inter-process protocol); No for purely internal changes
   - `quickstart.md` → Yes if the feature requires manual verification steps or cannot be fully validated by automated tests; No if the test suite covers everything

**Output**: `## Research & Decisions` section in plan.md filled (or research.md created), `## Artifact Generation Decisions` table filled

### Phase 1: Design & Contracts

**Prerequisites:** Phase 0 complete, Artifact Generation Decisions table filled

Generate **only** the artifacts declared "Yes" in the Artifact Generation Decisions table:

1. **If `data-model.md` = Yes**: Extract entities from feature spec:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **If `contracts/` = Yes**: Define interface contracts:
   - Identify what interfaces the project exposes to users or other systems
   - Document the contract format appropriate for the project type
   - Examples: public APIs for libraries, command schemas for CLI tools, endpoints for web services, grammars for parsers, UI contracts for applications

3. **If `quickstart.md` = Yes**: Write the manual verification walkthrough.

4. **Agent context update**:
   - Update the plan reference between the `<!-- SPECKIT START -->` and `<!-- SPECKIT END -->` markers in `CLAUDE.md` to point to the plan file (the IMPL_PLAN path)

**Output**: only the artifacts declared "Yes", plus updated agent context file

## Key rules

- Use absolute paths for filesystem operations; use project-relative paths for references in documentation and agent context files
- ERROR on gate failures or unresolved clarifications
