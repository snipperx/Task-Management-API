// Smoke test: 1 virtual user, one pass over the critical path. Fast enough for CI.
//   k6 run load/smoke.js
//   k6 run -e BASE_URL=http://localhost:5000 load/smoke.js
import http from 'k6/http';
import { check, group } from 'k6';
import { BASE_URL, login, authHeaders, tagged } from './lib/helpers.js';

export const options = {
  vus: 1,
  iterations: 1,
  // The checks below are the assertions — every one must pass. http_req_failed is not a
  // threshold here because the RBAC step deliberately expects a 403.
  thresholds: {
    checks: ['rate==1.0'],
  },
};

export default function () {
  group('health', () => {
    const res = http.get(`${BASE_URL}/health`, tagged('GET /health'));
    check(res, {
      'health 200': (r) => r.status === 200,
      'health reports Healthy': (r) => r.json('status') === 'Healthy',
    });
  });

  const token = login('manager');
  const auth = authHeaders(token);

  let projectId;
  group('projects', () => {
    const res = http.get(`${BASE_URL}/api/projects?status=Active&pageSize=1`, tagged('GET /api/projects', auth));
    check(res, { 'projects 200': (r) => r.status === 200 });
    projectId = res.json('items.0.id');
  });

  let taskId;
  group('create task', () => {
    const body = JSON.stringify({
      title: `smoke ${Date.now()}`,
      projectId,
      priority: 'Low',
      estimatedHours: 1,
    });
    const res = http.post(`${BASE_URL}/api/tasks`, body, tagged('POST /api/tasks', auth));
    check(res, { 'task created 201': (r) => r.status === 201 });
    taskId = res.json('id');
  });

  group('task lifecycle', () => {
    const get = http.get(`${BASE_URL}/api/tasks/${taskId}`, tagged('GET /api/tasks/:id', auth));
    check(get, { 'get task 200': (r) => r.status === 200 });

    const move = http.patch(
      `${BASE_URL}/api/tasks/${taskId}/status`,
      JSON.stringify({ status: 'InProgress' }),
      tagged('PATCH /api/tasks/:id/status', auth),
    );
    check(move, { 'status moved 200': (r) => r.status === 200 });

    const comment = http.post(
      `${BASE_URL}/api/tasks/${taskId}/comments`,
      JSON.stringify({ content: 'smoke comment' }),
      tagged('POST /api/tasks/:id/comments', auth),
    );
    check(comment, { 'comment 201': (r) => r.status === 201 });

    const del = http.del(`${BASE_URL}/api/tasks/${taskId}`, null, tagged('DELETE /api/tasks/:id', auth));
    check(del, { 'task deleted 204': (r) => r.status === 204 });
  });

  group('rbac', () => {
    const viewer = authHeaders(login('viewer'));
    const res = http.post(
      `${BASE_URL}/api/tasks`,
      JSON.stringify({ title: 'nope', projectId, priority: 'Low' }),
      tagged('POST /api/tasks (viewer)', viewer),
    );
    check(res, { 'viewer forbidden 403': (r) => r.status === 403 });
  });
}
