package za.co.malume.driver.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import kotlinx.coroutines.flow.Flow

@Dao
interface TripManifestDao {

    @Query("SELECT * FROM cached_trips WHERE serviceDate = :serviceDate ORDER BY directionCode ASC, routeName ASC")
    fun observeTripsByDate(serviceDate: String): Flow<List<CachedTripEntity>>

    @Query("SELECT * FROM cached_trips WHERE tripId = :tripId LIMIT 1")
    fun observeTrip(tripId: String): Flow<CachedTripEntity?>

    @Query("SELECT * FROM cached_trips WHERE tripId = :tripId LIMIT 1")
    suspend fun getTrip(tripId: String): CachedTripEntity?

    @Query("SELECT * FROM cached_manifest_stops WHERE tripId = :tripId ORDER BY sequenceOrder ASC")
    fun observeStopsForTrip(tripId: String): Flow<List<CachedManifestStopEntity>>

    @Query("SELECT * FROM cached_manifest_stops WHERE tripId = :tripId ORDER BY sequenceOrder ASC")
    suspend fun getStopsForTrip(tripId: String): List<CachedManifestStopEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertTrip(trip: CachedTripEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertStops(stops: List<CachedManifestStopEntity>)

    @Query("DELETE FROM cached_manifest_stops WHERE tripId = :tripId")
    suspend fun deleteStopsForTrip(tripId: String)

    @Transaction
    suspend fun saveTripManifest(trip: CachedTripEntity, stops: List<CachedManifestStopEntity>) {
        upsertTrip(trip)
        deleteStopsForTrip(trip.tripId)
        upsertStops(stops)
    }

    @Query(
        """
        UPDATE cached_manifest_stops
        SET attendanceStatus = :attendanceStatus,
            handoverMethod = :handoverMethod,
            handoverTimestampUtc = :timestampUtc,
            wasSyncedOffline = :wasSyncedOffline
        WHERE tripId = :tripId AND learnerId = :learnerId
        """,
    )
    suspend fun updateStopAttendance(
        tripId: String,
        learnerId: String,
        attendanceStatus: String,
        handoverMethod: String,
        timestampUtc: String,
        wasSyncedOffline: Boolean,
    )

    @Query("UPDATE cached_trips SET status = :status WHERE tripId = :tripId")
    suspend fun updateTripStatus(tripId: String, status: String)
}

@Dao
interface OfflineOutboxDao {

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun enqueue(event: OfflineHandoverEventEntity)

    @Query("SELECT * FROM offline_handover_outbox WHERE syncState = 'PENDING' ORDER BY recordedAtUtc ASC")
    suspend fun getPendingOrdered(): List<OfflineHandoverEventEntity>

    @Query("SELECT COUNT(*) FROM offline_handover_outbox WHERE syncState = 'PENDING'")
    fun observePendingCount(): Flow<Int>

    @Query("UPDATE offline_handover_outbox SET syncState = :syncState, lastError = :lastError WHERE eventId = :eventId")
    suspend fun updateSyncState(eventId: String, syncState: String, lastError: String?)
}
