# UI audit report (fakes mode)

**Pass 1 (route walk):** 2026-08-05T13:19:56Z  
**Pass 2 (docs + static guards):** 2026-08-05  
**Pass 3 (sequence / button / input — partial):** 2026-08-05  
**Base:** `http://127.0.0.1:5088` · `useFakes=true`

## Coverage

| Kind of test | Status |
|--------------|--------|
| Routes load without hard exception text | Done (pass 1) |
| Create/delete project (API) | Done |
| Create project UI (`home-new-project`) | Partial — form found; empty-name **button disabled** |
| Sequence matrix | **Partial** (S1 done; S2/S6/S7 blocked mid-run by terms modal then fixed) |
| Button enable/disable vs readiness | Partial |
| Input boundaries | Partial (Create empty name only so far) |
| Full fake movie gen → review | Pending |

## Pass 1 issues

1. **[low]** Home img missing alt  
2. **[medium]** `/film` empty — no route (Film = `/scenes`)  
3. **[medium]** `/billing` empty — use `/account/costs`  
4. **[high→info]** Create control is `home-new-project` (audit script had wrong selectors)  
5. **[medium]** Cost length input missing when project id not bound  
6. **[medium]** Console 404 resource  

## Pass 3 findings (sequence)

1. **[high] Terms modal blocks studio** — `TermsAgreementModal` (`#terms-title`) intercepts all clicks until `#termsCheck` + **Agree & continue**. API `POST /api/users/terms/accept` alone does not clear the Blazor modal for the browser session. Any automated or first-run sequence must accept terms first or appear “broken.”  
2. **[pass] Create empty/whitespace name** — `home-create-project` stays **`disabled`** when name is whitespace (button guard, not only silent handler). Good.  
3. **[open] Agree & Continue** — control is present and **enabled** on Cost with a project even when film stage may not be ready; click testing was interrupted by terms modal; still need confirm it does not bypass `CanScenes`.  
4. **[open] Film length boundaries** — not fully measured (run stopped early).  
5. **S1** — Import / Characters / Cost / Scenes open with zero projects (PASS visit; empty-state quality not fully scored).  

### How to accept terms in UI tests

```js
await page.locator("#termsCheck").check({ force: true });
await page.locator(".modal.show button.btn-primary").click({ force: true });
```

## Static weak guards (still open)

- Agree & Continue only `disabled="@_busy"` — may navigate to `/scenes` while strip Film is disabled.  
- Deep links soft-empty without consistent CTA.  
- Strip vs page CTA parity unproven in browser after terms.

## Next explicit runs

- Complete S2–S8 after terms accept on every page load.  
- Length: `0`, `-1`, `181` → API target clamp.  
- JobRunning double-submit on Generate.  
