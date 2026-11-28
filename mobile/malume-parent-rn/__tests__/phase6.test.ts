import { MalumeParentApiClient } from '../src/services/apiClient';
import {
  buildExternalMapUrl,
  haversineDistanceMeters,
  projectMapPoints,
} from '../src/utils/geoMap';

describe('Phase 6 geoMap utilities', () => {
  test('haversineDistanceMeters computes accurate Sandton-to-gate distance', () => {
    const meters = haversineDistanceMeters(
      { latitude: -26.1076, longitude: 28.0567 },
      { latitude: -26.112, longitude: 28.0567 },
    );
    expect(meters).toBeGreaterThan(450);
    expect(meters).toBeLessThan(550);
  });

  test('projectMapPoints normalizes multiple pins into [0.12..0.88] canvas range', () => {
    const projected = projectMapPoints([
      { id: 'home', latitude: -26.22, longitude: 27.85 },
      { id: 'bus', latitude: -26.21, longitude: 27.86 },
      { id: 'gate', latitude: -26.2, longitude: 27.87 },
    ]);

    expect(projected).toHaveLength(3);
    expect(projected[0].xPct).toBeCloseTo(0.12, 2);
    expect(projected[2].xPct).toBeCloseTo(0.88, 2);
    expect(projected[2].yPct).toBeCloseTo(0.12, 2);
    expect(projected[0].yPct).toBeCloseTo(0.88, 2);
  });

  test('buildExternalMapUrl formats OpenStreetMap coordinate URL', () => {
    const url = buildExternalMapUrl(-26.2041, 28.0473);
    expect(url).toBe('https://www.openstreetmap.org/?mlat=-26.204100&mlon=28.047300#map=16/-26.204100/28.047300');
  });
});

describe('Phase 6 MalumeParentApiClient endpoints', () => {
  const originalFetch = globalThis.fetch;

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  test('initializes payment, verifies payment, lists escrow subscriptions, and triggers SOS', async () => {
    const calls: Array<{ url: string; method: string; body?: string }> = [];
    const mockFetch = jest.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      const body = typeof init?.body === 'string' ? init.body : undefined;
      calls.push({ url, method, body });

      let responseBody: unknown = {};
      if (url.endsWith('/payments/subscriptions/initialize')) {
        responseBody = {
          subscriptionId: 'sub-1',
          paystackReference: 'MLM-REF-123',
          authorizationUrl: 'https://checkout.paystack.com/abc',
          accessCode: 'abc',
          publicKey: 'pk_test_demo',
          totalAmountZar: 1200,
        };
      } else if (url.includes('/verify')) {
        responseBody = {
          subscriptionId: 'sub-1',
          paystackReference: 'MLM-REF-123',
          escrowStatus: 'Active',
          paymentVerified: true,
          totalAmountZar: 1200,
        };
      } else if (url.endsWith('/escrow/subscriptions')) {
        responseBody = [];
      } else if (url.endsWith('/sos')) {
        responseBody = {
          incidentId: 'inc-1',
          tripId: 'trip-1',
          driverProfileId: 'drv-1',
          driverName: 'Malume Sipho',
          vehicleRegistration: 'GP 99 MAL',
          latitude: -26.2041,
          longitude: 28.0473,
          onboardLearnerCount: 2,
          triggeredAtUtc: '2026-11-28T10:00:00Z',
          triggeredByRole: 'Parent',
          status: 'Active',
        };
      }

      return {
        ok: true,
        status: 200,
        text: async () => JSON.stringify(responseBody),
      } as Response;
    });

    globalThis.fetch = mockFetch as unknown as typeof fetch;

    const client = new MalumeParentApiClient();
    client.setBaseUrl('http://localhost:5144');
    client.setAccessToken('jwt-token');

    const init = await client.initializeSubscriptionPayment({
      routeId: 'route-1',
      learnerId: 'learner-1',
    });
    expect(init.paystackReference).toBe('MLM-REF-123');

    const verified = await client.verifySubscriptionPayment('MLM-REF-123');
    expect(verified.paymentVerified).toBe(true);

    const subs = await client.listEscrowSubscriptions();
    expect(subs).toEqual([]);

    const sos = await client.triggerTripSos('trip-1', -26.2041, 28.0473);
    expect(sos.status).toBe('Active');
    expect(calls.map(c => `${c.method} ${c.url}`)).toEqual([
      'POST http://localhost:5144/api/v1/payments/subscriptions/initialize',
      'POST http://localhost:5144/api/v1/payments/subscriptions/MLM-REF-123/verify',
      'GET http://localhost:5144/api/v1/escrow/subscriptions',
      'POST http://localhost:5144/api/v1/trips/trip-1/sos',
    ]);
  });
});
