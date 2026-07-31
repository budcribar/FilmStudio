# Agent prompt engineering (this workspace)

How prompts are layered so agents extend Page to Movie instead of reinventing it.

## Stack of prompts (outer → inner)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Platform / Grok Build system prompt (role, tools, safety) │
│    — always on; agent cannot edit                             │
├─────────────────────────────────────────────────────────────┤
│ 2. AGENTS.md — sandbox contract                               │
│    Two worlds, :8080, startup.sh, no .env, verify yourself,  │
│    product language to user, skill routing, parallel-agent    │
│    rules. ~platform-owned; treat as immutable.                │
├─────────────────────────────────────────────────────────────┤
│ 3. AGENTS.project.md — product rules (THIS REPO)              │
│    SoT split, wizard, models.json, media, gotchas.            │
│    Short, scannable, non-negotiables first.                   │
├─────────────────────────────────────────────────────────────┤
│ 4. Skills (.grok/skills/*/SKILL.md) — on demand               │
│    YAML frontmatter: name, description (triggers), metadata.  │
│    Body: procedure; references/ for depth (lazy load).        │
├─────────────────────────────────────────────────────────────┤
│ 5. docs/* — deep inventory (CODEBASE, SERVER_DB, …)           │
│    Linked from AGENTS.project; not all inlined every turn.    │
├─────────────────────────────────────────────────────────────┤
│ 6. User chat + conversation memory / chunk summaries          │
│    Product intent, corrections, multi-turn continuity.        │
└─────────────────────────────────────────────────────────────┘
```

**Precedence:** user chat > AGENTS.project (product) > AGENTS.md (platform) for *product* decisions; platform safety/ops in AGENTS.md still bind.

## Design patterns that work here

### 1. Role + constraint before procedure
Open with *who you are* and *what success is* (e.g. “app on 8080, verified, left running”), not a laundry list of tips.

### 2. Two-world framing
Explicit agent vs user capabilities prevents the #1 failure mode: asking the user to run shell commands or open localhost.

### 3. Tables over prose
Invariants, path maps, “when changing X touch Y” — dense, scannable, high recall under context pressure.

### 4. Never / always pairs
| Never | Always |
|-------|--------|
| Parallel `*-models.json` | One `models.json` + `capability` |
| Blobs on server | IndexedDB + media ids |
| Secrets in client | `ptm_secrets` / env |

Negatives alone fail; pair with the positive default.

### 5. Progressive disclosure (skills)
- **Router:** short description + trigger phrases in frontmatter (for skill selection).
- **Playbook:** SKILL.md body — enough to execute.
- **Depth:** `references/` — load only when needed.

Avoid dumping every skill into every turn.

### 6. Shared contract before parallel agents
AGENTS.md: establish routes/types/tokens **before** multi-agent writes; non-overlapping surfaces; integrate after.

### 7. Verification as part of the prompt
Not “write code” — “typecheck + smoke + leave preview up.” Makes “done” falsifiable.

### 8. Product language for user-facing text
Internal: ports, tools, containers. External: “your film studio is in the preview.”

## Anti-patterns

| Anti-pattern | Why it fails |
|--------------|--------------|
| One giant prompt with every rule | Truncation / middle-loss; critical invariants buried |
| Only positive examples, no Never | Agent invents parallel architecture |
| Skill body = full API dump | Tokens wasted; use references/ |
| Project rules only in chat | Lost on revive / new agent (keep AGENTS.project.md) |
| Duplicate rules in 5 files | Drift; one SoT + links |
| Vague “be careful with media” | Prefer: “ids only in DTO; blobs in IndexedDB” |

## Writing / editing project prompts (checklist)

When updating `AGENTS.project.md` or adding a skill:

1. **Lead with invariants** (SoT table) — 20 lines max before detail.  
2. **Link** deep docs; don’t paste CODEBASE into AGENTS.project.  
3. **Trigger phrases** in skill YAML must match real user/agent language.  
4. **One procedure path** (“touch Y in order”) for schema/provider changes.  
5. **Verify commands** copy-pasteable.  
6. **Test the prompt:** ask a fresh agent to add a field/provider and see if it opens the right files first.  
7. After hibernate/revive, **confirm AGENTS.project.md still has content** (it was wiped empty once).

## Mapping to Page to Movie work

| Goal | Prompt layer |
|------|----------------|
| Don’t rebuild store as localStorage SoT | AGENTS.project invariants |
| Add ElevenLabs-like provider | models.json + when-changing-X table |
| UI polish | design-ui skill (on demand) |
| Auth / Neon | auth + neon skills |
| Wizard continuity | Product wizard section + types.ts |

## Recommended file roles

| File | Length | Change frequency |
|------|--------|------------------|
| `AGENTS.md` | Long | Platform |
| `AGENTS.project.md` | Short–medium | Every architecture decision |
| `docs/CODEBASE.md` | Medium | When layout shifts |
| `docs/SERVER_DB.md` | Short | Migrations / SoT |
| `.grok/skills/*/SKILL.md` | Medium | When new capability playbook needed |

Keep **rules** in AGENTS.project; **maps** in docs; **how-to craft** (this file) optional for meta work.
