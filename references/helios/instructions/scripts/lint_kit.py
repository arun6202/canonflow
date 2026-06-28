#!/usr/bin/env python3
"""Dependency-free lint check for the Structuring Agents starter kit.

Usage:
    python scripts/lint_kit.py [path/to/starter-kit]

Checks:
  - agents/*.md  : required frontmatter keys, Guardrails section, [UNSOURCED] mention
  - skills/*.md  : required frontmatter keys, passive skills have no mcp_grants
  - .claude/settings.json : deny list present (deny-by-default)
  - mcp/connectors.json   : no raw secrets, default_policy is deny
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REQUIRED_AGENT_KEYS = ["name", "model", "role", "mcp_grants"]
REQUIRED_SKILL_KEYS = ["name", "type", "passive"]

RAW_SECRET_PATTERN = re.compile(
    r'"(?:password|api_key|token|secret)"\s*:\s*"(?!\$\{)(?!\*)[^"]'
)


def parse_frontmatter(text: str) -> dict[str, str]:
    if not text.startswith("---"):
        return {}
    end = text.find("\n---", 3)
    if end == -1:
        return {}
    block = text[3:end].strip()
    data: dict[str, str] = {}
    for line in block.splitlines():
        if ":" in line and not line.startswith(" "):
            key, _, value = line.partition(":")
            data[key.strip()] = value.strip()
    return data


def lint_agent(path: Path) -> list[str]:
    errors: list[str] = []
    text = path.read_text(encoding="utf-8")
    fm = parse_frontmatter(text)
    if not fm:
        errors.append(f"{path}: missing frontmatter")
        return errors
    for key in REQUIRED_AGENT_KEYS:
        if key not in fm:
            errors.append(f"{path}: missing frontmatter key '{key}'")
    if "## Guardrails" not in text and "## Negative Space" not in text:
        errors.append(f"{path}: agent should include a '## Guardrails' section")
    if "unsourced" not in text.lower() and "[UNSOURCED]" not in text:
        errors.append(f"{path}: agent should state how it handles unsourced claims")
    return errors


def lint_skill(path: Path) -> list[str]:
    errors: list[str] = []
    text = path.read_text(encoding="utf-8")
    fm = parse_frontmatter(text)
    if not fm:
        errors.append(f"{path}: missing frontmatter")
        return errors
    for key in REQUIRED_SKILL_KEYS:
        if key not in fm:
            errors.append(f"{path}: missing frontmatter key '{key}'")
    if fm.get("passive") == "true" and "mcp_grants" in text:
        errors.append(f"{path}: passive skill should not declare mcp_grants")
    return errors


def lint_settings(root: Path) -> list[str]:
    p = root / ".claude" / "settings.json"
    if not p.exists():
        return [".claude/settings.json missing — deny-by-default cannot be verified"]
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return [f".claude/settings.json: invalid JSON — {exc}"]
    perms = data.get("permissions", {})
    if not perms.get("deny"):
        return [".claude/settings.json: 'permissions.deny' list is empty or missing"]
    return []


def lint_connectors(root: Path) -> list[str]:
    errors: list[str] = []
    p = root / "mcp" / "connectors.json"
    if not p.exists():
        return ["mcp/connectors.json missing"]
    text = p.read_text(encoding="utf-8")
    if RAW_SECRET_PATTERN.search(text):
        errors.append("mcp/connectors.json: possible raw secret value — use secret_env references instead")
    try:
        data = json.loads(text)
    except json.JSONDecodeError as exc:
        return [f"mcp/connectors.json: invalid JSON — {exc}"]
    if data.get("default_policy") != "deny":
        errors.append("mcp/connectors.json: 'default_policy' should be 'deny'")
    return errors


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path.cwd()
    errors: list[str] = []

    agents_dir = root / "agents"
    if agents_dir.exists():
        for path in sorted(agents_dir.glob("*.md")):
            errors.extend(lint_agent(path))
    else:
        errors.append(f"agents/ directory not found under {root}")

    skills_dir = root / "skills"
    if skills_dir.exists():
        for path in sorted(skills_dir.glob("*.md")):
            errors.extend(lint_skill(path))
    else:
        errors.append(f"skills/ directory not found under {root}")

    errors.extend(lint_settings(root))
    errors.extend(lint_connectors(root))

    if errors:
        print("Lint failed:\n")
        for e in errors:
            print(f"  - {e}")
        return 1

    print("Lint passed. Starter kit structure looks OK.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
