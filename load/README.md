# Load & smoke tests (k6)

[k6](https://k6.io/) scripts that drive the running API over HTTP.

| Script | Shape | Use |
|---|---|---|
| `smoke.js` | 1 VU, 1 iteration | Fast "is the critical path alive" check — health, auth, task lifecycle, one RBAC assertion. CI-friendly. |
| `load.js` | ramping VUs (default 20), ~1m45s | Read-heavy browse traffic + ~15% writes, with p95 latency thresholds. |

## Run

k6 is a single binary — install it (`brew install k6`, `apt`/`choco`, or the
[docker image](https://hub.docker.com/r/grafana/k6)) or run it containerised.

```bash
# against local dotnet run (http://localhost:5252)
k6 run load/smoke.js
k6 run load/load.js

# against docker compose (http://localhost:5000)
k6 run -e BASE_URL=http://localhost:5000 load/smoke.js

# tune load
k6 run -e VUS=50 load/load.js

# no local install — Docker (host.docker.internal reaches the host)
docker run --rm -i -v "$PWD/load:/load" -e BASE_URL=http://host.docker.internal:5252 \
  grafana/k6 run /load/smoke.js
```

## Thresholds

Both scripts `exit 1` if thresholds fail, so they gate a pipeline:

- `smoke.js` — zero failed requests, all checks pass.
- `load.js` — <2% failed requests, p95 `http_req_duration` < 800ms, p95 task-list < 600ms.

Tune the numbers in each file's `options.thresholds` to your target SLOs.

## Notes

- Scripts use the seed accounts (`SeedOnStartup=true`), so run against a freshly seeded DB.
- `load.js` creates and immediately deletes tasks; it leaves no residue beyond soft-deleted rows.
- Requests are tagged with stable names (`GET /api/tasks/:id`) so per-endpoint metrics don't
  fragment on the id in the path.
