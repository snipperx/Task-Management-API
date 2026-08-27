import http from 'k6/http';
import { check } from 'k6';

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5252';

export const CREDS = {
  admin: { email: 'admin@company.com', password: 'Admin@123' },
  manager: { email: 'manager@company.com', password: 'Manager@123' },
  dev: { email: 'dev1@company.com', password: 'Dev@123' },
  viewer: { email: 'viewer@company.com', password: 'Viewer@123' },
};

const JSON_HEADERS = { 'Content-Type': 'application/json' };

export function login(role = 'manager') {
  const { email, password } = CREDS[role];
  const res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ email, password }), {
    headers: JSON_HEADERS,
    tags: { name: 'POST /api/auth/login' },
  });
  check(res, { 'login 200': (r) => r.status === 200 });
  return res.json('accessToken');
}

export function authHeaders(token) {
  return { headers: { ...JSON_HEADERS, Authorization: `Bearer ${token}` } };
}

// Tag every request with a stable name so the URL id doesn't explode the metric cardinality.
export function tagged(name, extra = {}) {
  return { ...extra, tags: { ...(extra.tags || {}), name } };
}
