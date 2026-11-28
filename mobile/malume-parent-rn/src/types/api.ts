export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  fullName: string;
  role: string;
  tenantFleetId?: string | null;
  tenantSchoolId?: string | null;
}

export interface SchoolDto {
  id: string;
  name: string;
  emisNumber?: string | null;
  addressText: string;
  gateLatitude: number;
  gateLongitude: number;
  gateGeofenceRadiusMeters: number;
  adminUserId?: string | null;
}

export interface LearnerDto {
  learnerId: string;
  parentUserId: string;
  schoolId: string;
  fullName: string;
  grade: string;
  homeLatitude: number;
  homeLongitude: number;
  homeAddressText: string;
  emergencyContactPhone: string;
}

export interface CreateLearnerRequest {
  schoolId: string;
  fullName: string;
  grade: string;
  homeLatitude: number;
  homeLongitude: number;
  homeAddressText: string;
  emergencyContactPhone: string;
}

export interface LearnerQrBadgeDto {
  learnerId: string;
  schoolId: string;
  learnerFullName: string;
  qrPayload: string;
  qrPublicKeyBase64: string;
}

export interface DailyPinDto {
  learnerId: string;
  serviceDate: string;
  direction: string;
  directionCode: number;
  pinCode: string;
  verifierHash: string;
}

export interface ScheduledRouteDto {
  routeId: string;
  vehicleId: string;
  driverProfileId: string;
  schoolId: string;
  routeName: string;
  direction: string;
  monthlyPriceZar: number;
  activeSubscribedSeats: number;
  isActive: boolean;
}

export interface RouteEnrollmentDto {
  subscriptionId: string;
  routeId: string;
  learnerId: string;
  billingYearMonth: string;
  totalAmountZar: number;
  escrowStatus: string;
}

export interface ParentActiveTripDto {
  tripId: string;
  routeId: string;
  routeName: string;
  direction: string;
  directionCode: number;
  serviceDate: string;
  tripStatus: string;
  schoolId: string;
  schoolName: string;
  driverFullName: string;
  vehicleRegistration: string;
  learnerId: string;
  learnerFullName: string;
  attendanceStatus: string;
  sequenceOrder: number;
  estimatedArrivalUtc?: string | null;
  driverLatitude?: number | null;
  driverLongitude?: number | null;
  driverLocationUpdatedAtUtc?: string | null;
}

export interface EtaCascadeUpdateDto {
  learnerId: string;
  parentUserId: string;
  learnerFullName: string;
  newSequenceOrder: number;
  previousEtaUtc?: string | null;
  newEtaUtc: string;
  minutesEarlier: number;
}

export interface MarkLearnerAbsentResponse {
  tripId: string;
  learnerId: string;
  availableCorridorSeats: number;
  downstreamEtaUpdates: EtaCascadeUpdateDto[];
}

export interface CorridorAdHocOptionDto {
  tripId: string;
  routeId: string;
  routeName: string;
  direction: string;
  serviceDate: string;
  driverProfileId: string;
  driverFullName: string;
  vehicleMakeModel: string;
  vehicleRegistration: string;
  availableCorridorSeats: number;
  distanceMeters: number;
  hasSgbEndorsement: boolean;
}

export interface CorridorBookingResultDto {
  tripId: string;
  manifestEntryId: string;
  learnerId: string;
  sequenceOrder: number;
  remainingCorridorSeats: number;
  estimatedArrivalUtc?: string | null;
}

export interface OffPeakDriverOptionDto {
  driverProfileId: string;
  driverFullName: string;
  vehicleId: string;
  vehicleMakeModel: string;
  vehicleRegistration: string;
  distanceMeters: number;
}

export interface DriverTelemetryPingDto {
  tripId: string;
  latitude: number;
  longitude: number;
  speedKph: number;
  bearingDegrees: number;
  isSosActive: boolean;
  recordedAtUtc: string;
}

export interface SosAlertDto {
  tripId: string;
  driverProfileId: string;
  driverName: string;
  vehicleRegistration: string;
  latitude: number;
  longitude: number;
  onboardLearnerCount: number;
  triggeredAtUtc: string;
  notes?: string | null;
}
