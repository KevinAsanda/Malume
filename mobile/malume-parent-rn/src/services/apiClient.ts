import {
  AuthResponse,
  CorridorAdHocOptionDto,
  CorridorBookingResultDto,
  CreateLearnerRequest,
  DailyPinDto,
  LearnerDto,
  LearnerQrBadgeDto,
  MarkLearnerAbsentResponse,
  OffPeakDriverOptionDto,
  ParentActiveTripDto,
  RouteEnrollmentDto,
  ScheduledRouteDto,
  SchoolDto,
} from '../types/api';

export const DEFAULT_API_BASE_URL = 'http://10.0.2.2:5144';

export class ApiRequestError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
  }
}

export class MalumeParentApiClient {
  private baseUrl: string = DEFAULT_API_BASE_URL;
  private accessToken: string | null = null;

  setBaseUrl(url: string): void {
    const trimmed = url.trim().replace(/\/+$/, '');
    this.baseUrl = trimmed || DEFAULT_API_BASE_URL;
  }

  getBaseUrl(): string {
    return this.baseUrl;
  }

  setAccessToken(token: string | null): void {
    this.accessToken = token;
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  async login(email: string, password: string): Promise<AuthResponse> {
    const auth = await this.request<AuthResponse>('/api/v1/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email: email.trim(), password }),
      skipAuth: true,
    });
    this.accessToken = auth.accessToken;
    return auth;
  }

  async registerParent(
    email: string,
    fullName: string,
    phoneNumber: string,
    password: string,
  ): Promise<AuthResponse> {
    const auth = await this.request<AuthResponse>('/api/v1/auth/register', {
      method: 'POST',
      body: JSON.stringify({
        email: email.trim(),
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
        role: 'Parent',
      }),
      skipAuth: true,
    });
    this.accessToken = auth.accessToken;
    return auth;
  }

  listSchools(): Promise<SchoolDto[]> {
    return this.request<SchoolDto[]>('/api/v1/schools');
  }

  listLearners(): Promise<LearnerDto[]> {
    return this.request<LearnerDto[]>('/api/v1/learners');
  }

  createLearner(payload: CreateLearnerRequest): Promise<LearnerDto> {
    return this.request<LearnerDto>('/api/v1/learners', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  }

  getLearnerQrBadge(learnerId: string): Promise<LearnerQrBadgeDto> {
    return this.request<LearnerQrBadgeDto>(`/api/v1/learners/${learnerId}/qr-badge`);
  }

  getLearnerDailyPin(
    learnerId: string,
    serviceDate: string,
    direction: string,
  ): Promise<DailyPinDto> {
    const qs = `serviceDate=${encodeURIComponent(serviceDate)}&direction=${encodeURIComponent(direction)}`;
    return this.request<DailyPinDto>(`/api/v1/learners/${learnerId}/daily-pin?${qs}`);
  }

  listScheduledRoutes(): Promise<ScheduledRouteDto[]> {
    return this.request<ScheduledRouteDto[]>('/api/v1/routes');
  }

  enrollLearnerOnRoute(routeId: string, learnerId: string): Promise<RouteEnrollmentDto> {
    return this.request<RouteEnrollmentDto>(`/api/v1/routes/${routeId}/enrollments`, {
      method: 'POST',
      body: JSON.stringify({ learnerId }),
    });
  }

  getParentActiveTrips(serviceDate?: string): Promise<ParentActiveTripDto[]> {
    const suffix = serviceDate ? `?serviceDate=${encodeURIComponent(serviceDate)}` : '';
    return this.request<ParentActiveTripDto[]>(`/api/v1/trips/parent/active${suffix}`);
  }

  markLearnerAbsent(tripId: string, learnerId: string): Promise<MarkLearnerAbsentResponse> {
    return this.request<MarkLearnerAbsentResponse>(
      `/api/v1/trips/${tripId}/learners/${learnerId}/mark-absent`,
      {
        method: 'POST',
        body: JSON.stringify({}),
      },
    );
  }

  searchCorridorOptions(params: {
    schoolId: string;
    pickupLatitude: number;
    pickupLongitude: number;
    serviceDate?: string;
    maxDistanceMeters?: number;
    direction?: string;
  }): Promise<CorridorAdHocOptionDto[]> {
    const queryParts = [
      `schoolId=${encodeURIComponent(params.schoolId)}`,
      `pickupLatitude=${encodeURIComponent(String(params.pickupLatitude))}`,
      `pickupLongitude=${encodeURIComponent(String(params.pickupLongitude))}`,
    ];
    if (params.serviceDate) {
      queryParts.push(`serviceDate=${encodeURIComponent(params.serviceDate)}`);
    }
    if (params.maxDistanceMeters !== undefined) {
      queryParts.push(`maxDistanceMeters=${encodeURIComponent(String(params.maxDistanceMeters))}`);
    }
    if (params.direction) {
      queryParts.push(`direction=${encodeURIComponent(params.direction)}`);
    }
    return this.request<CorridorAdHocOptionDto[]>(`/api/v1/corridor/options?${queryParts.join('&')}`);
  }

  bookCorridorSeat(
    tripId: string,
    learnerId: string,
    pickupLatitude?: number,
    pickupLongitude?: number,
  ): Promise<CorridorBookingResultDto> {
    return this.request<CorridorBookingResultDto>(`/api/v1/corridor/trips/${tripId}/book`, {
      method: 'POST',
      body: JSON.stringify({ learnerId, pickupLatitude, pickupLongitude }),
    });
  }

  searchOffPeakDrivers(
    pickupLatitude: number,
    pickupLongitude: number,
    maxDistanceMeters = 5000,
  ): Promise<OffPeakDriverOptionDto[]> {
    const qs = `pickupLatitude=${encodeURIComponent(String(pickupLatitude))}&pickupLongitude=${encodeURIComponent(String(pickupLongitude))}&maxDistanceMeters=${encodeURIComponent(String(maxDistanceMeters))}`;
    return this.request<OffPeakDriverOptionDto[]>(`/api/v1/corridor/off-peak-drivers?${qs}`);
  }

  private async request<T>(
    path: string,
    init?: RequestInit & { skipAuth?: boolean },
  ): Promise<T> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };

    if (!init?.skipAuth && this.accessToken) {
      headers.Authorization = `Bearer ${this.accessToken}`;
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers: {
        ...headers,
        ...(init?.headers as Record<string, string> | undefined),
      },
    });

    const text = await response.text();
    if (!response.ok) {
      let message = `HTTP ${response.status}`;
      if (text) {
        try {
          const problem = JSON.parse(text) as { title?: string; detail?: string };
          message = problem.detail || problem.title || message;
        } catch {
          message = text;
        }
      }
      throw new ApiRequestError(response.status, message);
    }

    return (text ? JSON.parse(text) : undefined) as T;
  }
}
