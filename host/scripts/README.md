# Host scripts

```bash
./host/scripts/run-api-ui.sh
PORT=5090 ./host/scripts/run-api-ui.sh

./host/scripts/run-ui-tests.sh
START_API=0 PLAYWRIGHT_BASE_URL=http://127.0.0.1:5080 ./host/scripts/run-ui-tests.sh
```

| Variable | Role |
|----------|------|
| `ASPNETCORE_URLS` | API listen address |
| `PLAYWRIGHT_BASE_URL` | Playwright target |
| `PORT` | Default 5080 for both |
| `START_API` | `1` start Api in background (default) |
