package za.co.malume.driver.location

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.Looper
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import za.co.malume.driver.MalumeDriverApp
import za.co.malume.driver.R
import za.co.malume.driver.data.remote.dto.DriverTelemetryPingDto
import java.time.Instant

data class LiveLocationSnapshot(
    val latitude: Double = -26.2041,
    val longitude: Double = 28.0473,
    val speedKph: Double = 0.0,
    val isTracking: Boolean = false,
    val isSosActive: Boolean = false,
)

class TripTelemetryService : Service() {

    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private lateinit var fusedLocationClient: FusedLocationProviderClient

    private var activeTripId: String? = null
    private var sosMode: Boolean = false
    private var locationCallback: LocationCallback? = null

    override fun onCreate() {
        super.onCreate()
        fusedLocationClient = LocationServices.getFusedLocationProviderClient(this)
        ensureNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopLocationUpdates()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
                return START_NOT_STICKY
            }

            ACTION_START_OR_UPDATE, ACTION_ENABLE_SOS -> {
                val tripId = intent.getStringExtra(EXTRA_TRIP_ID) ?: activeTripId
                if (tripId.isNullOrBlank()) {
                    stopSelf()
                    return START_NOT_STICKY
                }
                activeTripId = tripId
                sosMode = intent.action == ACTION_ENABLE_SOS ||
                    intent.getBooleanExtra(EXTRA_SOS_MODE, sosMode)

                val notification = buildNotification(sosMode)
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                    startForeground(
                        NOTIFICATION_ID,
                        notification,
                        ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION,
                    )
                } else {
                    startForeground(NOTIFICATION_ID, notification)
                }

                startLocationUpdates()
            }
        }
        return START_STICKY
    }

    private fun startLocationUpdates() {
        val hasFine = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED
        val hasCoarse = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED
        if (!hasFine && !hasCoarse) {
            return
        }

        locationCallback?.let { fusedLocationClient.removeLocationUpdates(it) }

        val intervalMs = if (sosMode) 1_000L else 5_000L
        val request = LocationRequest.Builder(Priority.PRIORITY_HIGH_ACCURACY, intervalMs)
            .setMinUpdateIntervalMillis(intervalMs)
            .build()

        val callback = object : LocationCallback() {
            override fun onLocationResult(result: LocationResult) {
                val location = result.lastLocation ?: return
                val tripId = activeTripId ?: return
                val speedKph = (location.speed.toDouble() * 3.6).coerceAtLeast(0.0)
                val snapshot = LiveLocationSnapshot(
                    latitude = location.latitude,
                    longitude = location.longitude,
                    speedKph = speedKph,
                    isTracking = true,
                    isSosActive = sosMode,
                )
                _latestLocation.value = snapshot

                val ping = DriverTelemetryPingDto(
                    tripId = tripId,
                    latitude = location.latitude,
                    longitude = location.longitude,
                    speedKph = speedKph,
                    bearingDegrees = location.bearing.toDouble(),
                    isSosActive = sosMode,
                    recordedAtUtc = Instant.now().toString(),
                )
                serviceScope.launch {
                    val app = applicationContext as? MalumeDriverApp ?: return@launch
                    app.container.signalRTelemetryClient.streamLocation(ping)
                }
            }
        }
        locationCallback = callback
        fusedLocationClient.requestLocationUpdates(request, callback, Looper.getMainLooper())
        _latestLocation.value = _latestLocation.value.copy(isTracking = true, isSosActive = sosMode)
    }

    private fun stopLocationUpdates() {
        locationCallback?.let { fusedLocationClient.removeLocationUpdates(it) }
        locationCallback = null
        _latestLocation.value = _latestLocation.value.copy(isTracking = false, isSosActive = false)
        serviceScope.launch {
            val app = applicationContext as? MalumeDriverApp ?: return@launch
            app.container.signalRTelemetryClient.disconnect()
        }
    }

    private fun ensureNotificationChannel() {
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val channel = NotificationChannel(
            CHANNEL_ID,
            "Malume Active Trip GPS",
            NotificationManager.IMPORTANCE_LOW,
        )
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(sosActive: Boolean): Notification {
        val text = if (sosActive) {
            "SOS ACTIVE: Streaming 1 Hz emergency GPS telemetry"
        } else {
            "Streaming active scholar route GPS telemetry"
        }
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setSmallIcon(R.mipmap.ic_launcher)
            .setOngoing(true)
            .build()
    }

    override fun onDestroy() {
        stopLocationUpdates()
        serviceScope.cancel()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    companion object {
        private const val CHANNEL_ID = "malume_trip_telemetry"
        private const val NOTIFICATION_ID = 3001
        private const val ACTION_START_OR_UPDATE = "za.co.malume.driver.START_TELEMETRY"
        private const val ACTION_ENABLE_SOS = "za.co.malume.driver.ENABLE_SOS"
        private const val ACTION_STOP = "za.co.malume.driver.STOP_TELEMETRY"
        private const val EXTRA_TRIP_ID = "extra_trip_id"
        private const val EXTRA_SOS_MODE = "extra_sos_mode"

        private val _latestLocation = MutableStateFlow(LiveLocationSnapshot())
        val latestLocation: StateFlow<LiveLocationSnapshot> = _latestLocation.asStateFlow()

        fun startTracking(context: Context, tripId: String, sosMode: Boolean = false) {
            val intent = Intent(context, TripTelemetryService::class.java).apply {
                action = if (sosMode) ACTION_ENABLE_SOS else ACTION_START_OR_UPDATE
                putExtra(EXTRA_TRIP_ID, tripId)
                putExtra(EXTRA_SOS_MODE, sosMode)
            }
            ContextCompat.startForegroundService(context, intent)
        }

        fun stopTracking(context: Context) {
            val intent = Intent(context, TripTelemetryService::class.java).apply {
                action = ACTION_STOP
            }
            context.startService(intent)
        }
    }
}
