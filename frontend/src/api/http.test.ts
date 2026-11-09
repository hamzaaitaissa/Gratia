import { getSession } from '../auth/session';
import { fakeToken, mockApi, signIn } from '../test/utils';
import { ApiError, request } from './http';

const me = { id: 'u1', companyId: 'c1', role: 'Employee' as const, email: 'maya@fernhilllabs.com' };
const freshToken = fakeToken({ nameid: 'u1', CompanyId: 'c1', role: 'Employee', email: 'maya@fernhilllabs.com', n: '2' });

it('refreshes an expired access token once, even for requests that fail together', async () => {
  signIn(me);
  const { calls } = mockApi({
    'GET /api/user': (req) => (req.headers.Authorization === `Bearer ${freshToken}` ? { body: ['ok'] } : { status: 401 }),
    'POST /api/auth/refresh-token': () => ({ body: { accessToken: freshToken, refreshToken: 'refresh-2', refreshTokenExpiryTime: '' } }),
  });

  const results = await Promise.all([request<string[]>('/api/user'), request<string[]>('/api/user')]);

  expect(results).toEqual([['ok'], ['ok']]);
  expect(calls.filter((c) => c.path === '/api/auth/refresh-token')).toHaveLength(1);
  expect(calls.find((c) => c.path === '/api/auth/refresh-token')?.body).toBe('refresh-1'); // the API takes a bare JSON string
  expect(getSession()?.refreshToken).toBe('refresh-2');
});

it('uses the tokens another tab already refreshed instead of spending the old refresh token', async () => {
  signIn(me);
  // Another tab refreshed: the old refresh token is spent, and the new tokens are in storage
  localStorage.setItem('gratia.session', JSON.stringify({ accessToken: freshToken, refreshToken: 'refresh-2' }));
  const { calls } = mockApi({
    'GET /api/user': (req) => (req.headers.Authorization === `Bearer ${freshToken}` ? { body: ['ok'] } : { status: 401 }),
    'POST /api/auth/refresh-token': () => ({ status: 401 }),
  });

  expect(await request<string[]>('/api/user')).toEqual(['ok']);
  expect(calls.some((c) => c.path === '/api/auth/refresh-token')).toBe(false);
  expect(getSession()?.refreshToken).toBe('refresh-2');
});

it('signs out when the refresh token is no longer valid', async () => {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ status: 401 }),
    'POST /api/auth/refresh-token': () => ({ status: 401, body: { status: 401, detail: 'Invalid refresh token' } }),
  });

  await expect(request('/api/user')).rejects.toBeInstanceOf(ApiError);
  expect(getSession()).toBeNull();
});

it('reads the ProblemDetails of a failed request', async () => {
  signIn(me);
  mockApi({ 'POST /api/transaction': () => ({ status: 400, body: { status: 400, title: 'Bad request', detail: 'Nope', traceId: 't-1' } }) });

  const error = await request('/api/transaction', { method: 'POST', body: {} }).catch((e: unknown) => e);

  expect(error).toBeInstanceOf(ApiError);
  expect((error as ApiError).status).toBe(400);
  expect((error as ApiError).traceId).toBe('t-1');
});
