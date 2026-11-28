package za.co.malume.driver.sync

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import za.co.malume.driver.data.local.OfflineHandoverEventEntity
import za.co.malume.driver.data.local.OutboxSyncState
import za.co.malume.driver.data.remote.HandoverSyncApi
import za.co.malume.driver.data.remote.dto.BatchSyncHandoversRequestDto
import za.co.malume.driver.data.remote.dto.BatchSyncHandoversResponseDto
import za.co.malume.driver.data.remote.dto.HandoverSyncItemResultDto
import java.io.IOException

class OutboxSyncEngineTest {

    private class InMemoryOutboxStore(initial: List<OfflineHandoverEventEntity>) : OutboxStore {
        val items = initial.associateBy { it.eventId }.toMutableMap()

        override suspend fun getPendingOrdered(): List<OfflineHandoverEventEntity> =
            items.values.filter { it.syncState == OutboxSyncState.PENDING }.sortedBy { it.recordedAtUtc }

        override suspend fun updateSyncState(eventId: String, syncState: String, lastError: String?) {
            val current = items[eventId] ?: return
            items[eventId] = current.copy(syncState = syncState, lastError = lastError)
        }
    }

    @Test
    fun flushPending_marksAppliedDuplicateSyncedAndRejectedFailed() = runTest {
        val store = InMemoryOutboxStore(
            listOf(
                sampleEvent("ev-1", "trip-1"),
                sampleEvent("ev-2", "trip-1"),
                sampleEvent("ev-3", "trip-1"),
            ),
        )
        val fakeApi = object : HandoverSyncApi {
            override suspend fun batchSyncHandovers(
                tripId: String,
                request: BatchSyncHandoversRequestDto,
            ): BatchSyncHandoversResponseDto {
                assertEquals(listOf("ev-1", "ev-2", "ev-3"), request.events.map { it.eventId })
                return BatchSyncHandoversResponseDto(
                    tripId = tripId,
                    appliedCount = 1,
                    duplicateCount = 1,
                    rejectedCount = 1,
                    results = listOf(
                        HandoverSyncItemResultDto("ev-1", "learner-1", "Applied", null),
                        HandoverSyncItemResultDto("ev-2", "learner-1", "Duplicate", null),
                        HandoverSyncItemResultDto("ev-3", "learner-1", "Rejected", "Learner was marked absent."),
                    ),
                )
            }
        }

        val result = OutboxSyncEngine(store, fakeApi).flushPending()

        assertFalse(result.retryNeeded)
        assertEquals(2, result.syncedCount)
        assertEquals(1, result.rejectedCount)
        assertEquals(OutboxSyncState.SYNCED, store.items["ev-1"]?.syncState)
        assertEquals(OutboxSyncState.SYNCED, store.items["ev-2"]?.syncState)
        assertEquals(OutboxSyncState.FAILED, store.items["ev-3"]?.syncState)
        assertEquals("Learner was marked absent.", store.items["ev-3"]?.lastError)
    }

    @Test
    fun flushPending_onNetworkIoException_keepsPendingAndRequestsRetry() = runTest {
        val store = InMemoryOutboxStore(listOf(sampleEvent("ev-1", "trip-1")))
        val offlineApi = object : HandoverSyncApi {
            override suspend fun batchSyncHandovers(
                tripId: String,
                request: BatchSyncHandoversRequestDto,
            ): BatchSyncHandoversResponseDto {
                throw IOException("Load shedding network drop")
            }
        }

        val result = OutboxSyncEngine(store, offlineApi).flushPending()

        assertTrue(result.retryNeeded)
        assertEquals(0, result.syncedCount)
        assertEquals(OutboxSyncState.PENDING, store.items["ev-1"]?.syncState)
    }

    private fun sampleEvent(eventId: String, tripId: String) = OfflineHandoverEventEntity(
        eventId = eventId,
        tripId = tripId,
        learnerId = "learner-1",
        action = "PickedUp",
        method = "QrScan",
        recordedAtUtc = "2026-11-28T06:41:00Z",
        latitude = -26.241,
        longitude = 27.901,
        qrPayload = "MLM1:...",
        fallbackPin = null,
        syncState = OutboxSyncState.PENDING,
        lastError = null,
    )
}
