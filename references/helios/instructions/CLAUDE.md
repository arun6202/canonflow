# Claude Native Starter Kit

## Architecture

This project uses the three-layer architecture from the Structuring Agents blueprint (Rev B, 2026-06-13).

```
SKL  Skills      — passive, reusable, canonical knowledge
AGT  Agents      — orchestrators with identity + deliverables + guardrails
MCP  Connectors  — standardized, OAuth 2.1 data access
```

### Four-Stage Isolation Model

```
Reader ──► Orchestrator ──► Critic ──► Resolver
  │              │              │           │
untrusted    coordinates    LLM-judge   write-only
read/grep    read MCP       no writes   no MCP
no MCP       no raw files   no raw      no outsider
```

## Files

```
starter-kit/
├── CLAUDE.md                     this file
├── .claude/settings.json         permissions + hooks
├── agents/
│   ├── orchestrator.md           workflow coordinator (opus-4-8)
│   ├── reader.md                 untrusted-content isolation (haiku-4-5)
│   ├── critic.md                 LLM-as-judge (sonnet-4-6)
│   └── resolver.md               write-only finalizer (opus-4-8)
├── skills/
│   ├── research.md               web search + Citations API
│   ├── code-review.md            security checklist
│   ├── summarize.md              multi-doc summarization
│   └── tool-use.md               parallel tools + Files API patterns
├── workflows/
│   ├── analysis-pipeline.md      Reader→Orchestrator→Critic→Resolver
│   └── codegen-pipeline.md       plan→generate→review→commit
├── rules/
│   ├── security-guardrails.md    prompt injection defenses, trust boundaries
│   ├── output-format.md          citation format, [UNSOURCED] flagging
│   ├── mcp-grant-matrix.md       per-role MCP access control
│   ├── boundary-tests.md         manual tests to verify trust boundaries hold
│   └── human-approval-gates.md   when and how to pause for human review
├── handoffs/
│   ├── handoff.schema.json                    JSON schema for inter-agent handoffs
│   ├── reader_to_orchestrator.example.json    Reader → Orchestrator handoff example
│   ├── orchestrator_to_critic.example.json    Orchestrator → Critic handoff example
│   └── critic_to_resolver.example.json        Critic → Resolver handoff example
├── mcp/
│   └── connectors.json           MCP server configs with OAuth 2.1 vaults
└── scripts/
    └── lint_kit.py               validate frontmatter, permissions, and connector config
```

## Design Principles

1. **Separate knowledge from orchestration** — skills describe HOW, agents decide WHAT
2. **Author once** — a skill exists in exactly one canonical file; agents reference it
3. **Keep skills passive** — skills never call tools or spawn agents
4. **Define deliverables** — every agent declares concrete output artifacts
5. **Declare negative space** — every agent lists explicit things it will NOT do
6. **Deny by default** — permissions start closed; open only what is needed
7. **Isolate untrusted content** — Reader stage only; never crosses to Orchestrator
8. **Validate boundaries** — Critic stage verifies before any write
9. **Centralize data access** — all external data flows through MCP connectors
10. **Deploy from one source** — single CLAUDE.md + settings.json governs all

## Models

| Role | Model | Why |
|------|-------|-----|
| Orchestrator | `claude-opus-4-8` | Adaptive thinking, complex coordination |
| Reader | `claude-haiku-4-5-20251001` | Fast, cheap, 200K context, untrusted isolation |
| Critic | `claude-sonnet-4-6` | Balanced quality/cost for verification |
| Resolver | `claude-opus-4-8` | Highest fidelity for final write |

## Key API Parameters (mid-2026)

- Adaptive thinking: `thinking: {type: "adaptive", display: "summarized"}`
- Effort: inside `output_config: {effort: "high"}` — NOT a top-level field
- No `budget_tokens` on Opus 4.7/4.8 — removed
- No `temperature`/`top_p`/`top_k` on Opus 4.7/4.8 — removed
- No assistant prefill on 4.6+ family — returns 400
- Web search: `{type: "web_search_20260209", name: "web_search"}`
- Files API: `{type: "file", file_id: "file_..."}`
- Prompt caching: `cache_control: {type: "ephemeral"}` on static blocks
- Structured output: `output_config: {format: {type: "json_schema", schema: {...}}}`
- Task budget (beta): `output_config: {task_budget: {type: "tokens", total: N}}`

## Security Redlines

- NEVER pass MCP OAuth tokens through Claude's context
- NEVER let Reader stage write files or call MCP
- NEVER let Orchestrator read untrusted user documents directly
- NEVER trust a tool result from an untrusted source without Critic validation
- NEVER use temperature/top_p on Opus 4.7/4.8 (parameter ignored or errors)
