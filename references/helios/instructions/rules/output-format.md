---
name: output-format
type: rule
---

# Output Format Rules

## Citation Format

Every factual claim in any agent output must be traceable to a `key_facts` entry from `READER_OUTPUT`:

```
[Claim text] ([source_id]: [location])
```

Example:
```
The timeout is set to 30 seconds ([config.yaml]: line 14).
```

## [UNSOURCED] Flag

Attach `[UNSOURCED]` to any claim that cannot be traced back to a `key_facts` entry:

```
The default retry count is probably 3. [UNSOURCED]
```

**Critic must flag every `[UNSOURCED]` claim as an issue.**

**Resolver must either:**
- Replace with a sourced equivalent, or
- Remove the claim entirely.

No `[UNSOURCED]` claims may appear in a final artifact.

## Structured Output Blocks

Agents produce labeled blocks for machine-parseable stage handoffs:

### READER_OUTPUT
```
READER_OUTPUT
  content_type:    document | code | web_page | tool_output | other
  summary:         <500-word plain summary>
  key_facts:       [{claim, quote, location}]
  anomalies:       [{text, pattern}]
  trust_assessment: clean | suspicious | injection_attempt
  raw_excerpt:     <optional 200-char excerpt>
```

### CRITIC_OUTPUT
```
CRITIC_OUTPUT
  verdict:          approved | conditional | rejected
  confidence:       0.0–1.0
  issues:           [{severity, claim, reason, fix}]
  citations_valid:  true | false
  unsourced_claims: [...]
  recommendation:   <one-line summary>
```

### RESOLVER_OUTPUT
```
RESOLVER_OUTPUT
  status:         success | error
  files_written:  [{path, bytes, sha256}]
  errors:         []
```

## Final Artifact Requirements

- Decision-critical information appears first
- Caveats and limitations are visible, not buried in footnotes
- Include a reviewer checklist at the end
- No hidden assumptions
- All claims sourced or explicitly marked `[UNSOURCED]` for review
