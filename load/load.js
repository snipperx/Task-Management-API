// Load test: ramping read-heavy traffic with a slice of writes.
//   k6 run load/load.js
//   k6 run -e BASE_URL=http://localhost:5000 -e RATE=50 load/load.js
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { BASE_URL, login, authHeaders, tagged } from './lib/helpers.js';

const listLatency = new Trend('list_tasks_latency', true);

export const options = {
  scenarios: {
    browse: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: Number(__ENV.VUS || 20) },
        { duration: '1m', target: Number(__ENV.VUS || 20) },
        { duration: '15s', target: 0 },
      ],
      gracefulStop: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<800'],
    list_tasks_latency: ['p(95)<600'],
    checks: ['rate>0.98'],
  },
};

// One login per VU, reused across iterations.
export function setup() {
  return { projectId: seedProjectId() };
}

function seedProjectId() {
  const token = login('manager');
  const res = http.get(`${BASE_URL}/api/projects?status=Active&pageSize=1`, authHeaders(token));
  return res.json('items.0.id');
}

let vuToken;

export default function (data) {
  if (!vuToken) vuToken = login(__VU % 3 === 0 ? 'manager' : 'dev');
  const auth = authHeaders(vuToken);

  group('browse', () => {
    const list = http.get(
      `${BASE_URL}/api/tasks?pageSize=20&sort=-createdAt`,
      tagged('GET /api/tasks', auth),
    );
    listLatency.add(list.timings.duration);
    check(list, { 'list 200': (r) => r.status === 200 });

    const stats = http.get(`${BASE_URL}/api/tasks/statistics`, tagged('GET /api/tasks/statistics', auth));
    check(stats, { 'stats ok': (r) => r.status === 200 || r.status === 403 });

    const overdue = http.get(`${BASE_URL}/api/tasks/overdue`, tagged('GET /api/tasks/overdue', auth));
    check(overdue, { 'overdue 200': (r) => r.status === 200 });
  });

  // ~15% of iterations also write.
  if (Math.random() < 0.15) {
    group('write', () => {
      const create = http.post(
        `${BASE_URL}/api/tasks`,
        JSON.stringify({
          title: `load ${__VU}-${__ITER}`,
          projectId: data.projectId,
          priority: 'Medium',
          estimatedHours: 2,
        }),
        tagged('POST /api/tasks', auth),
      );
      const ok = check(create, { 'create 201/403': (r) => r.status === 201 || r.status === 403 });
      if (ok && create.status === 201) {
        http.del(`${BASE_URL}/api/tasks/${create.json('id')}`, null, tagged('DELETE /api/tasks/:id', auth));
      }
    });
  }

  sleep(Math.random() * 1 + 0.5);
}
