---
name: mcp-grant-matrix
type: rule
---

# MCP Grant Matrix

**Default policy: deny all.** Only permissions listed here are active.

## Per-Role Tool Access

| Role | Allowed | Denied |
|------|---------|--------|
| Reader | `file.read`, `grep` | All MCP connectors, writes, web fetch, shell |
| Orchestrator | `mcp.trusted-read`, `workflow.plan`, `workflow.dispatch` | Untrusted file reads, writes, shell |
| Critic | `file.read` (verified payloads only) | All MCP connectors, writes, untrusted docs, shell |
| Resolver | `artifact.write` | All MCP connectors, reads of untrusted docs, shell, web fetch |

## MCP Connector Access

| Connector | Reader | Orchestrator | Critic | Resolver |
|-----------|--------|--------------|--------|----------|
| `trusted-read` | ✗ | ✓ read-only | ✗ | ✗ |
| `artifact-store` | ✗ | ✗ | ✗ | ✓ write-only |

Connector definitions live in `mcp/connectors.json`. Agents reference connectors by logical name only — never store URLs, credentials, or tokens in agent context.

## Human Approval Gates

These actions require **explicit human approval** before proceeding:

| Trigger | Who | Required |
|---------|-----|----------|
| External communication | Any agent | Yes |
| System-of-record write | Orchestrator / Resolver | Yes |
| Production change or deploy | Orchestrator | Yes |
| New connector grant | Admin | Yes |
| New tool grant to any agent | Admin | Yes |
| Costly or irreversible batch | Orchestrator | Yes |

**No approval by silence.** A pending gate stays blocked until explicitly approved and logged.

## Changing Grants

To add or remove a permission:
1. Update this file
2. Update `.claude/settings.json` (`allow` / `deny` lists)
3. Update the relevant agent frontmatter (`mcp_grants:`)
4. Run `scripts/lint_kit.py` to verify consistency
