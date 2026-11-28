package za.co.malume.driver

import android.app.Application
import android.content.Context
import za.co.malume.driver.data.local.MalumeDatabase
import za.co.malume.driver.data.remote.DriverSessionStore
import za.co.malume.driver.data.remote.MalumeApiService
import za.co.malume.driver.data.remote.SignalRTelemetryClient
import za.co.malume.driver.sync.ConnectivitySyncMonitor
import za.co.malume.driver.sync.OutboxSyncEngine
import za.co.malume.driver.sync.RoomOutboxStore

class DriverAppContainer(appContext: Context) {
    val database: MalumeDatabase by lazy { MalumeDatabase.getInstance(appContext) }
    val sessionStore: DriverSessionStore by lazy { DriverSessionStore(appContext) }
    val apiService: MalumeApiService by lazy { MalumeApiService(sessionStore) }
    val signalRTelemetryClient: SignalRTelemetryClient by lazy { SignalRTelemetryClient(sessionStore) }
    val outboxSyncEngine: OutboxSyncEngine by lazy {
        OutboxSyncEngine(RoomOutboxStore(database.offlineOutboxDao()), apiService)
    }
    val connectivityMonitor: ConnectivitySyncMonitor by lazy { ConnectivitySyncMonitor(appContext) }
}

class MalumeDriverApp : Application() {
    lateinit var container: DriverAppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = DriverAppContainer(applicationContext)
        container.connectivityMonitor.register()
    }
}
