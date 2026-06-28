---
name: codegen-pipeline
type: workflow
stages:
  - orchestrator
  - critic
  - resolver
skills_used:
  - code-review
  - tool-use
api_features:
  - adaptive_thinking
  - structured_output
  - parallel_tool_use
  - streaming
---

# Code Generation Pipeline

A three-stage workflow for generating, reviewing, and writing code. Skips the Reader stage (no untrusted external documents involved) but preserves the Critic gate before any write.

## Overview

```
[Task Specification]
        │
        ▼
┌─────────────────┐
│  ORCHESTRATOR   │  opus-4-8 — plan + generate
│  (plan/codegen) │  Adaptive thinking, parallel tool use
└────────┬────────┘
         │  CRITIC_INPUT (code + spec + tests)
         ▼
┌─────────────────┐
│     CRITIC      │  sonnet-4-6 — code review
│  (code-review)  │  Uses code-review.md skill
└────────┬────────┘
         │ approved
         ▼
┌─────────────────┐
│    RESOLVER     │  opus-4-8 — write files
│  (write-only)   │  No MCP, no analysis
└────────┬────────┘
         │
         ▼
[Code Written to Disk]
```

## Stage 1 — Plan and Generate

### Planning

Orchestrator receives a task spec and produces a `CODEGEN_PLAN`:
```
CODEGEN_PLAN
  task:           <one-line description>
  files:          list of files to create/modify
  approach:       <technical approach, 2–3 sentences>
  dependencies:   list of libraries/imports required
  test_strategy:  <how to verify correctness>
  risks:          list of potential issues
```

Use adaptive thinking for planning:
```python
plan_response = client.messages.create(
    model="claude-opus-4-8",
    max_tokens=4096,
    thinking={"type": "adaptive", "display": "summarized"},
    output_config={"effort": "high"},
    messages=[{
        "role": "user",
        "content": f"Plan the implementation of: {task_spec}"
    }]
)
```

### Code Generation with Parallel Tool Use

Read existing files in parallel while generating:
```python
tools = [
    {
        "name": "read_file",
        "description": "Read an existing source file",
        "input_schema": {
            "type": "object",
            "properties": {"path": {"type": "string"}},
            "required": ["path"]
        }
    },
    {
        "name": "list_directory",
        "description": "List files in a directory",
        "input_schema": {
            "type": "object",
            "properties": {"path": {"type": "string"}},
            "required": ["path"]
        }
    }
]

# Claude will call read_file on multiple files in parallel
gen_response = client.messages.create(
    model="claude-opus-4-8",
    max_tokens=16384,
    thinking={"type": "adaptive", "display": "summarized"},
    output_config={"effort": "xhigh"},
    tools=tools,
    messages=[{
        "role": "user",
        "content": f"Implement according to this plan:\n{plan}\n\nRead any existing files you need."
    }]
)
```

**Generated artifact:**
```
CODEGEN_ARTIFACT
  plan:      <CODEGEN_PLAN reference>
  files:     list of {path, content, language}
  tests:     list of {path, content, test_framework}
  notes:     <implementation notes for Critic>
```

## Stage 2 — Code Review (Critic)

Critic applies the `code-review` skill to every generated file.

**CRITIC_INPUT:**
```
CRITIC_INPUT (codegen variant)
  task:           <original task spec>
  analysis:       <generated code files>
  evidence:       <CODEGEN_PLAN — the spec the code was written against>
  claims_map:     [{file, spec_requirement}]
```

**CRITIC_OUTPUT includes:**
- Standard code-review structured output (from `code-review.md` skill)
- `spec_compliance`: list of spec requirements and whether each is met
- `test_coverage`: are the tests meaningful and covering edge cases?

**Rejection conditions:**
- Any `critical` or `high` severity finding in `code_review_result`
- `spec_compliance` fails for any required feature
- Tests are absent or trivially pass without validating behavior

**Maximum 3 Critic iterations.** On third rejection, escalate to user with full finding list.

## Stage 3 — Write (Resolver)

**RESOLVER_INPUT (codegen variant):**
```
RESOLVER_INPUT
  content:        list of {path, content}
  target_dir:     "src/"
  format:         code
  critic_verdict: approved
  checksum_before: {path: sha256}  -- verify no concurrent modification
```

Resolver writes files atomically:
1. Write each file to `{path}.tmp`
2. Verify SHA-256 of written content matches expected
3. Move `{path}.tmp` → `{path}`
4. Clean up `.tmp` files on any error

## Streaming for Long Code Generation

Always stream code generation to avoid timeout on large files:

```python
with client.messages.stream(
    model="claude-opus-4-8",
    max_tokens=16384,
    thinking={"type": "adaptive", "display": "summarized"},
    output_config={"effort": "xhigh"},
    messages=[...]
) as stream:
    for text in stream.text_stream:
        print(text, end="", flush=True)
    final = stream.get_final_message()
```

## Task Budget for Large Codegen Jobs

```python
client_with_budget = anthropic.Anthropic(
    default_headers={"anthropic-beta": "task-budgets-2026-03-13"}
)

response = client_with_budget.messages.create(
    model="claude-opus-4-8",
    max_tokens=32768,
    output_config={
        "effort": "xhigh",
        "task_budget": {"type": "tokens", "total": 500_000}
    },
    messages=[...]
)

if response.stop_reason == "task_budget_exceeded":
    # Return partial implementation with TODO markers
    return partial_result_with_todos(response)
```

## Mid-Conversation System Messages

For long codegen sessions, inject updated instructions mid-conversation (beta):
```python
messages = [
    {"role": "user", "content": "Implement feature X"},
    {"role": "assistant", "content": "...initial plan..."},
    {"role": "system", "content": "UPDATED CONSTRAINT: All functions must have type annotations."},
    {"role": "user", "content": "Continue with the implementation."}
]
```

Requires beta header: `anthropic-beta: mid-conversation-system-2026-04-07`

## Error Handling

| Error | Response |
|-------|----------|
| Critic rejects 3× | Escalate to user, provide Critic's finding list |
| Task budget exceeded | Return partial code with `# TODO: budget_exceeded` markers |
| Resolver path traversal | Halt, log, do not write |
| Test generation fails | Warn user; still proceed with code delivery |
| MCP connector failure | Proceed without reference data; note the gap |
