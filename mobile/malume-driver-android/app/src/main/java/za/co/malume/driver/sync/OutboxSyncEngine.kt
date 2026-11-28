package za.co.malume.driver.sync

import za.co.malume.driver.data.local.OfflineHandoverEventEntity
import za.co.malume.driver.data.local.OfflineOutboxDao
import za.co.malume.driver.data.local.OutboxSyncState
import za.co.malume.driver.data.remote.HandoverSyncApi
import za.co.malume.driver.data.remote.dto.BatchSyncHandoversRequestDto
import za.co.malume.driver.data.remote.dto.HandoverEventItemRequestDto
import java.io.IOException

interface OutboxStore {
    suspend fun getPendingOrdered(): List<OfflineHandoverEventEntity>
    suspend fun updateSyncState(eventId: String, syncState: String, lastError: String?)
}

class RoomOutboxStore(private val dao: OfflineOutboxDao) : OutboxStore {
    override suspend fun getPendingOrdered(): List<OfflineHandoverEventEntity> = dao.getPendingOrdered()

    override suspend fun updateSyncState(eventId: String, syncState: String, lastError: String?) =
        dao.updateSyncState(eventId, syncState, lastError)
}

data class SyncFlushResult(
    val syncedCount: Int,
    val rejectedCount: Int,
    val retryNeeded: Boolean,
)

class OutboxSyncEngine(
    private val outboxStore: OutboxStore,
    private val handoverSyncApi: HandoverSyncApi,
) {
    suspend fun flushPending(): SyncFlushResult {
        val pending = outboxStore.getPendingOrdered()
        if (pending.isEmpty()) {
            return SyncFlushResult(syncedCount = 0, rejectedCount = 0, retryNeeded = false)
        }

        var synced = 0
        var rejected = 0
        var retryNeeded = false

        val groupedByTrip = pending.groupBy { it.tripId }
        for ((tripId, events) in groupedByTrip) {
            val requestDto = BatchSyncHandoversRequestDto(
                events = events.map { ev ->
                    HandoverEventItemRequestDto(
                        eventId = ev.eventId,
                        learnerId = ev.learnerId,
                        action = ev.action,
                        method = ev.method,
                        recordedAtUtc = ev.recordedAtUtc,
                        latitude = ev.latitude,
                        longitude = ev.longitude,
                        wasSyncedOffline = true,
                        qrPayload = ev.qrPayload,
                        fallbackPin = ev.fallbackPin,
                    )
                },
            )

            try {
                val response = handoverSyncApi.batchSyncHandovers(tripId, requestDto)
                for (result in response.results) {
                    when (result.outcome) {
                        "Applied", "Duplicate" -> {
                            outboxStore.updateSyncState(result.eventId, OutboxSyncState.SYNCED, null)
                            synced++
                        }

                        else -> {
                            outboxStore.updateSyncState(
                                result.eventId,
                                OutboxSyncState.FAILED,
                                result.errorMessage ?: "Rejected by server.",
                            )
                            rejected++
                        }
                    }
                }
            } catch (_: IOException) {
                retryNeeded = true
            }
        }

        return SyncFlushResult(syncedCount = synced, rejectedCount = rejected, retryNeeded = retryNeeded)
    }
}
