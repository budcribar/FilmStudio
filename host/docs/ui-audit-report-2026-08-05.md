# UI audit report (fakes mode)

**Pass 1:** route walk · **Pass 2:** static guards · **Pass 3:** sequence partial · **Pass 4:** terms gate + post-terms sequences (2026-08-05T15:12Z)

**Base:** `http://127.0.0.1:5088` · `useFakes=true`

## Product decisions (docs only — code deferred)

| Topic | Decision |
|-------|----------|
| **`/demo` and terms** | **Public — terms not required.** Demo gallery must be usable without accepting Terms of Service. |
| Current behavior (pass 4) | Terms modal **incorrectly** appeared on `/demo` → treat as a **bug**, not a pass. |
| Code changes | Still deferred until remaining tests / explicit go-ahead |

## Pass 4 summary (corrected interpretation)

### Pre-terms (UI)

| Check | Result (corrected) |
|-------|---------------------|
| Terms modal on first load (studio) | PASS — expected |
| Agree & continue disabled until checkbox | PASS |
| Clicks blocked: New project, nav Cost/Adaptation/Configuration | PASS |
| Modal on `/cost`, `/scenes`, `/import`, `/characters`, `/admin`, `/configuration` | PASS — expected for studio |
| **Modal on `/demo`** | **FAIL — demo is public; must not require terms** |
| **API `POST /api/projects` without terms** | **FAIL — allowed (200)**; studio API should still enforce terms |

### Accept terms

| Check | Result |
|-------|--------|
| Automate `#termsCheck` + primary Agree | PASS |
| Stays dismissed after reload | PASS |

### Post-terms sequences

| Check | Result |
|-------|--------|
| S7 empty name → Create disabled | PASS |
| S7 UI create with valid name | PASS |
| S2 Agree → `/scenes` with blocked hint | PASS |
| Strip Film disabled without shots | PASS |
| Film length input on Cost | FAIL — not visible |
| S6 length boundaries | FAIL — blocked by missing input |

## Terms accept snippet (Playwright) — studio only

```js
await page.locator("#termsCheck").check({ force: true });
await page.locator(".modal.show button.btn-primary").click({ force: true });
```

Demo tests should load `/demo` **without** accepting terms and assert **no** terms modal.

Artifacts: `artifacts/ui-audit/terms-sequence-report.md`, `terms-*.png`
