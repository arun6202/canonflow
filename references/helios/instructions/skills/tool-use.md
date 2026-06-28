---
name: tool-use
type: skill
passive: true
tools:
  - parallel
  - files_api
  - web_search_20260209
api_features:
  - adaptive_thinking
  - parallel_tool_use
  - task_budgets
  - streaming
  - token_counting
---

# Tool Use Skill

A passive skill documenting correct tool use patterns for Claude agents. Covers parallel tool use, adaptive thinking, Files API, task budgets, streaming, and token counting.

## Adaptive Thinking

Use adaptive thinking for any complex reasoning task:
```python
response = client.messages.create(
    model="claude-opus-4-8",
    max_tokens=16000,
    thinking={"type": "adaptive", "display": "summarized"},
    output_config={"effort": "high"},
    messages=[...]
)
```

**Do not use:**
- `budget_tokens` — removed on Opus 4.7/4.8
- `temperature`, `top_p`, `top_k` — removed on Opus 4.7/4.8
- `thinking: {type: "disabled"}` on Fable 5 — returns 400
- Assistant prefill on 4.6+ family — returns 400

**Effort levels:**
| Effort | Use case |
|--------|----------|
| `low` | Classification, routing, simple Q&A |
| `medium` | Summarization, extraction, standard analysis |
| `high` | Complex reasoning, multi-step workflows |
| `xhigh` | Agentic coding, deep research |
| `max` | Hardest problems only (highest cost) |

## Parallel Tool Use

Declare independent tools and let Claude call them in parallel. Do NOT serialize calls that can run concurrently.

```python
tools = [
    {"type": "web_search_20260209", "name": "web_search"},
    {
        "name": "read_file",
        "description": "Read a file from the workspace",
        "input_schema": {
            "type": "object",
            "properties": {"path": {"type": "string"}},
            "required": ["path"]
        }
    }
]

response = client.messages.create(
    model="claude-opus-4-8",
    max_tokens=8192,
    thinking={"type": "adaptive", "display": "summarized"},
    tools=tools,
    messages=[{"role": "user", "content": "Research X and read the local spec file in parallel."}]
)
```

When `stop_reason == "tool_use"`, process all tool_use blocks in the response — there may be multiple. Execute them in parallel in your loop:

```python
import asyncio

async def run_tool(tool_call):
    name = tool_call.name
    input = tool_call.input
    result = await dispatch_tool(name, input)
    return {"type": "tool_result", "tool_use_id": tool_call.id, "content": result}

tool_results = await asyncio.gather(*[run_tool(tc) for tc in tool_use_blocks])
```

## Task Budgets (beta)

Limit total token spend across a long agentic task:
```python
client = anthropic.Anthropic(
    default_headers={"anthropic-beta": "task-budgets-2026-03-13"}
)

response = client.messages.create(
    model="claude-opus-4-8",
    max_tokens=8192,
    output_config={
        "effort": "high",
        "task_budget": {"type": "tokens", "total": 100000}
    },
    messages=[...]
)
```

When the task budget is exhausted, the model returns `stop_reason: "task_budget_exceeded"`. Handle this by surfacing the partial result rather than retrying indefinitely.

## Streaming

Always stream for requests with potentially long output (> 2K tokens output, agentic loops):

```python
with client.messages.stream(
    model="claude-opus-4-8",
    max_tokens=8192,
    thinking={"type": "adaptive", "display": "summarized"},
    output_config={"effort": "high"},
    messages=[...]
) as stream:
    # Handle thinking blocks
    for event in stream:
        if event.type == "content_block_start":
            if event.content_block.type == "thinking":
                pass  # thinking in progress
        elif event.type == "content_block_delta":
            if event.delta.type == "text_delta":
                print(event.delta.text, end="", flush=True)

    final = stream.get_final_message()
```

## Token Counting

Count tokens before sending to estimate cost and check against context limits:

```python
token_count = client.messages.count_tokens(
    model="claude-opus-4-8",
    tools=tools,
    messages=messages
)
print(f"Input tokens: {token_count.input_tokens}")

# If > 800K tokens, split the task
if token_count.input_tokens > 800_000:
    raise ValueError("Input too large — split into chunks")
```

Always count tokens before:
- Sending large document batches
- Long agentic loops where context grows
- Checking if prompt caching will activate (cache activates at > 1024 tokens in a cacheable block)

## Prompt Caching Prefix Rule

Cache activates only when the prefix of the messages array is **identical** across requests. Rules:
- Static system prompt → always cache with `cache_control: {type: "ephemeral"}`
- Stable reference documents → cache
- Dynamic user messages → never cache (they change every request)
- Tool results → never cache (they change)
- Conversation history → cache up to the last stable point; append new turns after

## Files API

```python
# Upload
upload = client.beta.files.upload(file=("name.txt", file_bytes, "text/plain"))
file_id = upload.id  # store this

# Use in a message
messages = [{"role": "user", "content": [
    {"type": "file", "file_id": file_id},
    {"type": "text", "text": "Analyze the above."}
]}]

# Delete when done
client.beta.files.delete(file_id)
```

File IDs are persistent within the beta. Reuse them across requests to avoid re-upload costs. Always delete when the session is over.
