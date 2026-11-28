package za.co.malume.driver.data.local

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(tableName = "cached_trips")
data class CachedTripEntity(
    @PrimaryKey val tripId: String,
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
    val cachedAtUtc: String,
)

@Entity(
    tableName = "cached_manifest_stops",
    indices = [Index(value = ["tripId"])],
)
data class CachedManifestStopEntity(
    @PrimaryKey val manifestEntryId: String,
    val tripId: String,
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
    val handoverTimestampUtc: String?,
    val estimatedArrivalUtc: String?,
    val qrPublicKeyVerifier: String,
    val dailyPinVerifierHash: String,
    val wasSyncedOffline: Boolean,
)

@Entity(
    tableName = "offline_handover_outbox",
    indices = [Index(value = ["tripId", "syncState"])],
)
data class OfflineHandoverEventEntity(
    @PrimaryKey val eventId: String,
    val tripId: String,
    val learnerId: String,
    val action: String,
    val method: String,
    val recordedAtUtc: String,
    val latitude: Double,
    val longitude: Double,
    val qrPayload: String?,
    val fallbackPin: String?,
    val syncState: String,
    val lastError: String?,
)

object OutboxSyncState {
    const val PENDING = "PENDING"
    const val SYNCED = "SYNCED"
    const val FAILED = "FAILED"
}
