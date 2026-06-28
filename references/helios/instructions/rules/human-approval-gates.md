---
name: human-approval-gates
type: rule
---

# Human Approval Gates

The pipeline must pause for explicit human approval before:

- sending external communication (email, Slack, ticket comment, webhook),
- writing to a system of record (database, search index, config store),
- running production changes (deploy, migration, rollout),
- accessing sensitive data beyond the declared task scope,
- executing costly or irreversible batch operations,
- changing connector permissions,
- granting new tools to any agent.

## Approval Request Format

```json
{
  "approval_id": "apr_001",
  "requested_by": "orchestrator",
  "action": "system_of_record_write",
  "reason": "Apply verified analysis results to production index.",
  "risk": "Search ranking may change for all users.",
  "rollback_plan": "Restore previous index from snapshot taken at YYYY-MM-DDTHH:MM:SSZ.",
  "expires_at": "YYYY-MM-DDTHH:MM:SSZ"
}
```

## Rule

**No approval by silence.**

An unanswered gate does not time out to "approved." It stays blocked. Approval must be explicit, logged, and timestamped. Partial or conditional approvals must specify exactly what is authorized.
