# Domain Docs

How the engineering skills read this repo's domain docs when they explore the code.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root.
- **`docs/adr/`**: read the ADRs that touch the area you are about to change.

If these files do not exist, **continue and say nothing**. Do not flag that they are missing. Do not suggest that you create them first. The `/domain-modeling` skill creates them when a term or a decision gets resolved. `/grill-with-docs` and `/improve-codebase-architecture` call that skill.

## File structure

This is a single-context repo:

```
/
├── GLOSSARY.md
├── docs/adr/
│   ├── 0001-source-generated-json.md
│   └── 0002-in-process-mcp-servers.md
└── src/
```

## Use the glossary's vocabulary

When your output names a domain concept, use the term that `GLOSSARY.md` defines. This applies to issue titles, refactor proposals, hypotheses and test names. Do not use a synonym that the glossary tells you to avoid.

If the concept is not in the glossary yet, one of two things is true. You may be inventing language the project does not use, so think again. Or the glossary has a real gap, so note it for `/domain-modeling`.

## Flag ADR conflicts

If your output contradicts an existing ADR, say so openly. Do not override it silently. For example:

> _Contradicts ADR-0001 (source-generated JSON), but worth reopening because..._
