# UI audit report (fakes mode)

Generated: 2026-08-05T13:19:56.755Z
Base: http://127.0.0.1:5088
useFakes: true

## Summary: 6 issues
- critical: 0
- high: 1
- medium: 4
- low: 1

## Issues
1. **[low]** (home) 1 visible img(s) missing alt
2. **[medium]** (/film) Main content very short/empty
   - len=0
3. **[medium]** (/billing) Main content very short/empty
   - len=0
4. **[high]** (projects) Could not find New/Create project control on Home
5. **[medium]** (cost) No number input for film length on Cost page
6. **[medium]** (console) Browser console error
   - Failed to load resource: the server responded with a status of 404 (Not Found)

## Notes
- BASE=http://127.0.0.1:5088 API=http://127.0.0.1:5088
- useFakes=true isAdmin=true
- nav links: /, /demo, /configuration, /adaptation, /cost, /admin, /admin/users, /admin/config, /admin/learning, /admin/book-cache, configuration, javascript:void(0)
- API created project: {"id":"local/UIAudit_API_1785935925035","label":"UI Audit API Project","title":"UI Audit API Project","path":"/tmp/ptm-workspace/projects/local/UIAudit_API_1785935925035","ownerUserId":"local","parent
- project count=2
- activate local/UIAudit_API_1785935793196 status=200
- file inputs on import: 1
- character list items: 1
- Agree & Continue present
- created delete-me project local/UIAudit_DeleteMe_1785935983960
- API delete status=200
