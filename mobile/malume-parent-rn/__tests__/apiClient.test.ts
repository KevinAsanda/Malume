import { ApiRequestError, MalumeParentApiClient } from '../src/services/apiClient';

describe('MalumeParentApiClient', () => {
  const originalFetch = globalThis.fetch;

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  test('login stores token and attaches bearer header to searchCorridorOptions', async () => {
    const calls: Array<{ url: string; headers: Record<string, string> }> = [];

    globalThis.fetch = jest.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      calls.push({
        url,
        headers: (init?.headers as Record<string, string>) ?? {},
      });

      if (url.endsWith('/api/v1/auth/login')) {
        return new Response(
          JSON.stringify({
            accessToken: 'jwt-test-token',
            expiresAtUtc: '2026-11-29T00:00:00Z',
            userId: 'u-1',
            email: 'parent@malume.co.za',
            fullName: 'Thandi Parent',
            role: 'Parent',
          }),
          { status: 200 },
        );
      }

      return new Response(JSON.stringify([]), { status: 200 });
    }) as typeof fetch;

    const client = new MalumeParentApiClient();
    client.setBaseUrl('http://localhost:5144/');
    await client.login('parent@malume.co.za', 'MalumePass!2026');
    await client.searchCorridorOptions({
      schoolId: 'school-1',
      pickupLatitude: -26.245,
      pickupLongitude: 27.912,
      serviceDate: '2026-11-28',
    });

    expect(calls).toHaveLength(2);
    expect(calls[1].url).toContain('http://localhost:5144/api/v1/corridor/options');
    expect(calls[1].headers.Authorization).toBe('Bearer jwt-test-token');
  });

  test('throws ApiRequestError with ProblemDetails detail on failure', async () => {
    globalThis.fetch = jest.fn(async () => {
      return new Response(
        JSON.stringify({
          title: 'Domain rule violation',
          detail: 'Pickup location lies outside the 1.5 km spatial corridor.',
          status: 400,
        }),
        { status: 400 },
      );
    }) as typeof fetch;

    const client = new MalumeParentApiClient();
    client.setAccessToken('jwt-test-token');

    await expect(client.bookCorridorSeat('trip-1', 'learner-1')).rejects.toThrow(ApiRequestError);
  });
});
