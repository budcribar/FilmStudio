# UI audit report (fakes mode)

**Pass 1:** route walk · **Pass 2:** static guards · **Pass 3:** sequence partial · **Pass 4:** terms gate + post-terms sequences (2026-08-05T15:12Z)

**Base:** `http://127.0.0.1:5088` · `useFakes=true`

## Pass 4 summary

**23 checks · 3 failures**

### Pre-terms (what must stay blocked)

| Check | Result |
|-------|--------|
| Terms modal on first load | PASS |
| Agree & continue disabled until checkbox | PASS |
| Clicks blocked: New project, nav Cost/Adaptation/Configuration | PASS |
| Direct URLs still show modal: `/cost`, `/scenes`, `/import`, `/characters`, `/admin`, `/demo`, `/configuration` | PASS |
| **API `POST /api/projects` without terms** | **FAIL — allowed (200)** |

UI shell is gated by the modal; **REST create is not**. A client that skips the UI can create projects without accepting terms.

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
| S2 Agree & Continue enabled (project, no book) | PASS |
| S2 Agree → `/scenes` with blocked empty-state hint | PASS (`blockedHint=true`) |
| Strip Film step disabled without shots | PASS |
| **Film length number input on Cost** | **FAIL — not visible** |
| **S6 length boundaries** | **FAIL — no input to test** |

## Earlier issues (still open)

- `/film`, `/billing` blank (no routes)
- Console 404 resource
- Agree enabled before film-ready (navigates to scenes; empty state soft-blocks)

## Terms accept snippet (Playwright)

```js
await page.locator("#termsCheck").check({ force: true });
await page.locator(".modal.show button.btn-primary").click({ force: true });
```

Artifacts: `artifacts/ui-audit/terms-sequence-report.md` + `terms-*.png`
