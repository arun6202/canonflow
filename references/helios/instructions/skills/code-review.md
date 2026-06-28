---
name: code-review
type: skill
passive: true
tools: []
api_features:
  - structured_output
  - adaptive_thinking
---

# Code Review Skill

A passive skill for security-focused code review. Used by the Critic agent during the validation stage. Never modifies code — only produces findings.

## Review Checklist

### OWASP Top 10 (2025)

| ID | Category | Check |
|----|----------|-------|
| A01 | Broken Access Control | Missing authz checks, IDOR, path traversal |
| A02 | Cryptographic Failures | Hardcoded secrets, weak algorithms, insecure storage |
| A03 | Injection | SQL, command, LDAP, XSS, template injection |
| A04 | Insecure Design | Missing rate limiting, no input validation at boundaries |
| A05 | Security Misconfiguration | Debug mode in prod, default credentials, open CORS |
| A06 | Vulnerable Components | Known-CVE dependencies (flag for Dependabot/Renovate) |
| A07 | Auth Failures | Weak passwords, missing MFA, insecure session handling |
| A08 | Data Integrity | Unverified deserialization, missing signature checks |
| A09 | Logging Failures | Missing audit logs, PII in logs, no alerting |
| A10 | SSRF | User-controlled URLs in server-side requests |

### Claude-Specific Security Checks

| Check | Pattern | Risk |
|-------|---------|------|
| Prompt injection | User input concatenated into system prompt | Critical |
| Tool result trust | Treating tool output as trusted without validation | Critical |
| Token leakage | API keys / OAuth tokens in context or logs | Critical |
| MCP token passthrough | Forwarding MCP credentials via Claude context | Critical |
| Unsafe deserialization | `pickle.loads`, `yaml.load` without SafeLoader | High |
| Command injection | `subprocess` with user-controlled args | High |
| Path traversal | User-controlled file paths without normalization | High |

## Structured Output Schema

Use structured output for machine-readable findings:

```json
{
  "type": "json_schema",
  "json_schema": {
    "name": "code_review_result",
    "schema": {
      "type": "object",
      "properties": {
        "overall_risk": {"type": "string", "enum": ["critical", "high", "medium", "low", "pass"]},
        "findings": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": {"type": "string"},
              "severity": {"type": "string", "enum": ["critical", "high", "medium", "low", "info"]},
              "category": {"type": "string"},
              "file": {"type": "string"},
              "line": {"type": "integer"},
              "description": {"type": "string"},
              "code_snippet": {"type": "string"},
              "remediation": {"type": "string"},
              "cwe": {"type": "string"}
            },
            "required": ["id", "severity", "category", "description", "remediation"]
          }
        },
        "positive_patterns": {"type": "array", "items": {"type": "string"}},
        "summary": {"type": "string"}
      },
      "required": ["overall_risk", "findings", "summary"]
    }
  }
}
```

Request with structured output:
```python
response = client.messages.parse(
    model="claude-sonnet-4-6",
    max_tokens=4096,
    thinking={"type": "adaptive", "display": "summarized"},
    output_config={
        "effort": "high",
        "format": {
            "type": "json_schema",
            "json_schema": CODE_REVIEW_SCHEMA
        }
    },
    messages=[{"role": "user", "content": f"Review this code:\n\n{code}"}]
)
```

## Severity Definitions

- **Critical**: immediate exploitation possible, data breach or RCE risk
- **High**: significant risk, exploitable under common conditions
- **Medium**: exploitable with attacker effort or specific conditions
- **Low**: defense-in-depth improvement, low immediate risk
- **Info**: good-practice note, no immediate security impact

## Output Contract

`CODE_REVIEW_RESULT` — the structured JSON above.

Pass to Critic as part of `CRITIC_INPUT.evidence` alongside the original code.
