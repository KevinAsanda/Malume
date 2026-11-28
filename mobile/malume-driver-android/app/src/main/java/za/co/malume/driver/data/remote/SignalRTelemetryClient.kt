package za.co.malume.driver.data.remote

import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import com.microsoft.signalr.HubConnectionState
import io.reactivex.rxjava3.core.Single
import za.co.malume.driver.data.remote.dto.DriverTelemetryPingDto

class SignalRTelemetryClient(
    private val sessionStore: DriverSessionStore,
) {
    private var connection: HubConnection? = null
    private var joinedTripId: String? = null

    @Synchronized
    fun ensureConnectedAndJoined(tripId: String): Boolean {
        val session = sessionStore.currentSession
        val token = session.auth?.accessToken ?: return false
        val hubUrl = "${session.apiBaseUrl}/hubs/trips"

        val current = connection
        if (current == null) {
            val built = HubConnectionBuilder.create(hubUrl)
                .withAccessTokenProvider(Single.defer {
                    Single.just(sessionStore.currentSession.auth?.accessToken.orEmpty())
                })
                .build()
            connection = built
        }

        val hub = connection ?: return false
        if (hub.connectionState != HubConnectionState.CONNECTED) {
            val started = runCatching { hub.start().blockingAwait() }.isSuccess
            if (!started) {
                return false
            }
            joinedTripId = null
        }

        if (joinedTripId != tripId) {
            val joined = runCatching { hub.invoke("JoinTripGroup", tripId).blockingAwait() }.isSuccess
            if (joined) {
                joinedTripId = tripId
            }
            return joined
        }

        return true
    }

    @Synchronized
    fun streamLocation(ping: DriverTelemetryPingDto): Boolean {
        if (!ensureConnectedAndJoined(ping.tripId)) {
            return false
        }
        val hub = connection ?: return false
        return runCatching {
            hub.invoke("StreamLocation", ping).blockingAwait()
        }.isSuccess
    }

    @Synchronized
    fun disconnect() {
        joinedTripId = null
        val hub = connection ?: return
        runCatching { hub.stop().blockingAwait() }
        connection = null
    }
}
