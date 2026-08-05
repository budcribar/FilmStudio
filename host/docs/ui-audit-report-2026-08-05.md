# UI audit report (fakes mode)

**Pass 1:** route walk · **Pass 2:** static guards · **Pass 3:** sequence partial · **Pass 4:** terms gate + post-terms sequences (2026-08-05T15:12Z)

**Base:** `http://127.0.0.1:5088` · `useFakes=true`

## Product decisions (docs only — no code yet)

| Topic | Decision |
|-------|----------|
| **`/demo` and terms** | **Requires terms.** Same `TermsAgreementModal` gate as the rest of the studio. Not an exemption. |
| Code changes | Deferred until after remaining sequence tests and fix-order agreement |

## Pass 4 summary

**23 checks · 3 failures** (one of which was “demo without terms” as a *question* — resolved as **must require terms**)

### Pre-terms (UI)

| Check | Result |
|-------|--------|
| Terms modal on first load | PASS |
| Agree & continue disabled until checkbox | PASS |
| Clicks blocked: New project, nav Cost/Adaptation/Configuration | PASS |
| Direct URLs show modal: `/cost`, `/scenes`, `/import`, `/characters`, `/admin`, **`/demo`**, `/configuration` | PASS (demo included by design) |
| **API `POST /api/projects` without terms** | **FAIL — allowed (200)** |

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
| S2 Agree & Continue → `/scenes` with blocked hint | PASS |
| Strip Film disabled without shots | PASS |
| **Film length input on Cost** | **FAIL — not visible** |
| **S6 length boundaries** | **FAIL — blocked by missing input** |

## Terms accept snippet (Playwright)

```js
await page.locator("#termsCheck").check({ force: true });
await page.locator(".modal.show button.btn-primary").click({ force: true });
```

Artifacts: `artifacts/ui-audit/terms-sequence-report.md`, `terms-*.png`
