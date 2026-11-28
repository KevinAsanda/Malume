package za.co.malume.driver.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class LoginRequestDto(
    val email: String,
    val password: String,
)

@Serializable
data class AuthResponseDto(
    val accessToken: String,
    val expiresAtUtc: String,
    val userId: String,
    val email: String,
    val fullName: String,
    val role: String,
    val tenantFleetId: String? = null,
    val tenantSchoolId: String? = null,
)

@Serializable
data class DriverProfileDto(
    val id: String,
    val userId: String,
    val fullName: String,
    val email: String,
    val fleetId: String? = null,
    val complianceStatus: String,
    val operatingMode: String,
    val prdpNumber: String,
    val prdpExpiryDate: String? = null,
    val policeClearanceExpiryDate: String? = null,
)

@Serializable
data class DriverTripSummaryDto(
    val tripId: String,
    val routeId: String,
    val routeName: String,
    val direction: String,
    val directionCode: Int,
    val schoolId: String,
    val schoolName: String,
    val vehicleId: String,
    val vehicleRegistration: String,
    val serviceDate: String,
    val status: String,
    val availableCorridorSeats: Int,
    val totalStops: Int,
    val completedStops: Int,
    val startedAtUtc: String? = null,
    val arrivedAtSchoolGateUtc: String? = null,
    val completedAtUtc: String? = null,
)

@Serializable
data class TripManifestStopDto(
    val manifestEntryId: String,
    val learnerId: String,
    val learnerFullName: String,
    val grade: String,
    val schoolId: String,
    val homeAddressText: String,
    val emergencyContactPhone: String,
    val sequenceOrder: Int,
    val stopLatitude: Double,
    val stopLongitude: Double,
    val bookingType: String,
    val attendanceStatus: String,
    val handoverMethod: String,
    val handoverTimestampUtc: String? = null,
    val estimatedArrivalUtc: String? = null,
    val qrPublicKeyVerifier: String,
    val dailyPinVerifierHash: String,
    val wasSyncedOffline: Boolean,
)

@Serializable
data class TripManifestDto(
    val tripId: String,
    val routeId: String,
    val routeName: String,
    val direction: String,
    val directionCode: Int,
    val serviceDate: String,
    val status: String,
    val schoolId: String,
    val schoolName: String,
    val schoolGateLatitude: Double,
    val schoolGateLongitude: Double,
    val schoolGateGeofenceRadiusMeters: Int,
    val vehicleRegistration: String,
    val driverComplianceStatus: String,
    val stops: List<TripManifestStopDto>,
)

@Serializable
data class HandoverEventItemRequestDto(
    val eventId: String,
    val learnerId: String,
    val action: String,
    val method: String,
    val recordedAtUtc: String,
    val latitude: Double,
    val longitude: Double,
    val wasSyncedOffline: Boolean,
    val qrPayload: String? = null,
    val fallbackPin: String? = null,
)

@Serializable
data class BatchSyncHandoversRequestDto(
    val events: List<HandoverEventItemRequestDto>,
)

@Serializable
data class HandoverSyncItemResultDto(
    val eventId: String,
    val learnerId: String,
    val outcome: String,
    val errorMessage: String? = null,
)

@Serializable
data class BatchSyncHandoversResponseDto(
    val tripId: String,
    val appliedCount: Int,
    val duplicateCount: Int,
    val rejectedCount: Int,
    val results: List<HandoverSyncItemResultDto>,
)

@Serializable
data class TriggerSosRequestDto(
    val latitude: Double,
    val longitude: Double,
    val notes: String? = null,
)

@Serializable
data class SosAlertDto(
    val tripId: String,
    val driverProfileId: String,
    val driverName: String,
    val vehicleRegistration: String,
    val latitude: Double,
    val longitude: Double,
    val onboardLearnerCount: Int,
    val triggeredAtUtc: String,
    val notes: String? = null,
)

data class DriverTelemetryPingDto(
    val tripId: String,
    val latitude: Double,
    val longitude: Double,
    val speedKph: Double,
    val bearingDegrees: Double,
    val isSosActive: Boolean,
    val recordedAtUtc: String,
)

@Serializable
data class ProblemDetailsDto(
    val title: String? = null,
    val detail: String? = null,
    val status: Int? = null,
)
