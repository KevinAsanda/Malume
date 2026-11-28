package za.co.malume.driver.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import za.co.malume.driver.MalumeDriverApp
import za.co.malume.driver.crypto.OfflineHandoverVerifier
import za.co.malume.driver.crypto.QrVerificationResult
import za.co.malume.driver.data.local.CachedManifestStopEntity
import za.co.malume.driver.data.local.CachedTripEntity
import za.co.malume.driver.data.local.OfflineHandoverEventEntity
import za.co.malume.driver.data.local.OutboxSyncState
import za.co.malume.driver.data.remote.dto.DriverProfileDto
import za.co.malume.driver.data.remote.dto.DriverTripSummaryDto
import za.co.malume.driver.data.remote.dto.TripManifestDto
import za.co.malume.driver.location.LiveLocationSnapshot
import za.co.malume.driver.location.TripTelemetryService
import za.co.malume.driver.sync.HandoverSyncWorker
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.UUID

sealed interface DriverScreen {
    data object Login : DriverScreen
    data object Schedule : DriverScreen
    data class TripManifest(val tripId: String) : DriverScreen
    data class QrScanner(val tripId: String, val stop: CachedManifestStopEntity) : DriverScreen
}

data class DriverUiState(
    val isLoading: Boolean = false,
    val serviceDate: String = currentSastDate(),
    val driverProfile: DriverProfileDto? = null,
    val remoteTrips: List<DriverTripSummaryDto> = emptyList(),
    val statusBanner: String? = null,
    val errorMessage: String? = null,
    val pinDialogStop: CachedManifestStopEntity? = null,
) {
    companion object {
        private val SAST_ZONE = ZoneId.of("Africa/Johannesburg")
        fun currentSastDate(): String = LocalDate.now(SAST_ZONE).format(DateTimeFormatter.ISO_LOCAL_DATE)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class DriverViewModel(application: Application) : AndroidViewModel(application) {

    private val app = application as MalumeDriverApp
    private val container = app.container
    private val tripDao = container.database.tripManifestDao()
    private val outboxDao = container.database.offlineOutboxDao()

    val sessionFlow = container.sessionStore.sessionFlow
    val isOnlineFlow = container.connectivityMonitor.isOnlineFlow
    val pendingSyncCountFlow = outboxDao.observePendingCount()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), 0)
    val locationSnapshotFlow: StateFlow<LiveLocationSnapshot> = TripTelemetryService.latestLocation

    private val _uiState = MutableStateFlow(DriverUiState())
    val uiState: StateFlow<DriverUiState> = _uiState.asStateFlow()

    private val _selectedTripId = MutableStateFlow<String?>(null)

    private val _currentScreen = MutableStateFlow<DriverScreen>(
        if (container.sessionStore.currentSession.isAuthenticated) {
            DriverScreen.Schedule
        } else {
            DriverScreen.Login
        },
    )
    val currentScreen: StateFlow<DriverScreen> = _currentScreen.asStateFlow()

    val cachedTripsFlow: StateFlow<List<CachedTripEntity>> = _uiState
        .flatMapLatest { state -> tripDao.observeTripsByDate(state.serviceDate) }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    val selectedTripFlow: StateFlow<CachedTripEntity?> = _selectedTripId
        .flatMapLatest { tripId ->
            if (tripId == null) flowOf(null) else tripDao.observeTrip(tripId)
        }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), null)

    val selectedStopsFlow: StateFlow<List<CachedManifestStopEntity>> = _selectedTripId
        .flatMapLatest { tripId ->
            if (tripId == null) flowOf(emptyList()) else tripDao.observeStopsForTrip(tripId)
        }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    init {
        if (container.sessionStore.currentSession.isAuthenticated) {
            refreshSchedule()
        }
    }

    fun login(apiBaseUrl: String, email: String, password: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null, statusBanner = null)
            runCatching {
                val auth = container.apiService.login(apiBaseUrl, email, password)
                container.sessionStore.saveLogin(auth, apiBaseUrl)
            }.onSuccess {
                _currentScreen.value = DriverScreen.Schedule
                registerFcmTokenBestEffort()
                refreshSchedule()
            }.onFailure { err ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    errorMessage = err.message ?: "Unable to sign in.",
                )
            }
        }
    }

    fun logout() {
        TripTelemetryService.stopTracking(app)
        container.sessionStore.clearAuth()
        _selectedTripId.value = null
        _currentScreen.value = DriverScreen.Login
        _uiState.value = DriverUiState()
    }

    fun clearMessages() {
        _uiState.value = _uiState.value.copy(errorMessage = null, statusBanner = null)
    }

    private fun registerFcmTokenBestEffort() {
        runCatching {
            FirebaseMessaging.getInstance().token.addOnSuccessListener { token ->
                if (!token.isNullOrBlank()) {
                    viewModelScope.launch {
                        runCatching { container.apiService.registerDeviceToken(token) }
                    }
                }
            }
        }
    }

    fun refreshSchedule() {
        viewModelScope.launch {
            val serviceDate = DriverUiState.currentSastDate()
            _uiState.value = _uiState.value.copy(
                isLoading = true,
                serviceDate = serviceDate,
                errorMessage = null,
            )

            runCatching {
                val profile = container.apiService.getMyDriverProfile()
                val schedule = container.apiService.getDriverSchedule(serviceDate)
                for (tripSummary in schedule) {
                    val manifest = container.apiService.getTripManifest(tripSummary.tripId)
                    persistManifest(manifest)
                }
                profile to schedule
            }.onSuccess { (profile, schedule) ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    driverProfile = profile,
                    remoteTrips = schedule,
                    statusBanner = if (schedule.isEmpty()) {
                        "No trips scheduled for $serviceDate."
                    } else {
                        "Cached ${schedule.size} encrypted trip manifest(s) offline."
                    },
                )
            }.onFailure { err ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    errorMessage = "Offline mode active (showing cached manifests): ${err.message}",
                )
            }
        }
    }

    fun openTrip(tripId: String) {
        _selectedTripId.value = tripId
        _currentScreen.value = DriverScreen.TripManifest(tripId)
        viewModelScope.launch {
            runCatching {
                val manifest = container.apiService.getTripManifest(tripId)
                persistManifest(manifest)
            }
        }
    }

    fun navigateBackToSchedule() {
        _currentScreen.value = DriverScreen.Schedule
    }

    fun openQrScanner(tripId: String, stop: CachedManifestStopEntity) {
        _currentScreen.value = DriverScreen.QrScanner(tripId, stop)
    }

    fun closeQrScanner(tripId: String) {
        _currentScreen.value = DriverScreen.TripManifest(tripId)
    }

    fun openPinDialog(stop: CachedManifestStopEntity) {
        _uiState.value = _uiState.value.copy(pinDialogStop = stop, errorMessage = null)
    }

    fun dismissPinDialog() {
        _uiState.value = _uiState.value.copy(pinDialogStop = null)
    }

    fun startTrip(tripId: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
            runCatching {
                val summary = container.apiService.startTrip(tripId)
                tripDao.updateTripStatus(tripId, summary.status)
                TripTelemetryService.startTracking(app, tripId, sosMode = false)
                summary
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    statusBanner = "Trip started. Live GPS telemetry active.",
                )
            }.onFailure { err ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    errorMessage = err.message ?: "Unable to start trip.",
                )
            }
        }
    }

    fun arriveAtGate(tripId: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
            runCatching {
                val summary = container.apiService.arriveAtSchoolGate(tripId)
                tripDao.updateTripStatus(tripId, summary.status)
                summary
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    statusBanner = "Arrived at school gate.",
                )
            }.onFailure { err ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    errorMessage = err.message ?: "Unable to update gate arrival.",
                )
            }
        }
    }

    fun completeTrip(tripId: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
            runCatching {
                val summary = container.apiService.completeTrip(tripId)
                tripDao.updateTripStatus(tripId, summary.status)
                TripTelemetryService.stopTracking(app)
                summary
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    statusBanner = "Trip completed cleanly.",
                )
            }.onFailure { err ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    errorMessage = err.message ?: "Unable to complete trip.",
                )
            }
        }
    }

    fun toggleGpsTracking(tripId: String) {
        if (locationSnapshotFlow.value.isTracking) {
            TripTelemetryService.stopTracking(app)
            _uiState.value = _uiState.value.copy(statusBanner = "GPS tracking paused.")
        } else {
            TripTelemetryService.startTracking(app, tripId, sosMode = false)
            _uiState.value = _uiState.value.copy(statusBanner = "GPS telemetry started.")
        }
    }

    fun triggerSos(tripId: String) {
        viewModelScope.launch {
            TripTelemetryService.startTracking(app, tripId, sosMode = true)
            val snap = locationSnapshotFlow.value
            runCatching {
                container.apiService.triggerSos(
                    tripId = tripId,
                    latitude = snap.latitude,
                    longitude = snap.longitude,
                    notes = "Driver SOS triggered from Android Companion App",
                )
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    statusBanner = "SOS ALERT SENT! Streaming 1 Hz emergency coordinates.",
                    errorMessage = null,
                )
            }.onFailure { err ->
                _uiState.value = _uiState.value.copy(
                    statusBanner = "GPS switched to 1 Hz SOS mode (API alert queued/failed offline: ${err.message})",
                )
            }
        }
    }

    fun verifyQrAndRecordOffline(
        tripId: String,
        stop: CachedManifestStopEntity,
        rawQrPayload: String,
    ) {
        when (
            val verification = OfflineHandoverVerifier.verifyQrSignature(
                rawPayload = rawQrPayload,
                expectedLearnerId = stop.learnerId,
                expectedSchoolId = stop.schoolId,
                publicKeyBase64 = stop.qrPublicKeyVerifier,
            )
        ) {
            is QrVerificationResult.Invalid -> {
                _uiState.value = _uiState.value.copy(errorMessage = verification.reason)
            }

            QrVerificationResult.Valid -> {
                recordVerifiedHandover(
                    tripId = tripId,
                    stop = stop,
                    method = "QrScan",
                    qrPayload = rawQrPayload.trim(),
                    fallbackPin = null,
                )
                _currentScreen.value = DriverScreen.TripManifest(tripId)
            }
        }
    }

    fun verifyPinAndRecordOffline(
        tripId: String,
        stop: CachedManifestStopEntity,
        candidatePin: String,
    ) {
        viewModelScope.launch {
            val trip = tripDao.getTrip(tripId)
            if (trip == null) {
                _uiState.value = _uiState.value.copy(errorMessage = "Cached trip metadata missing.")
                return@launch
            }

            val dateNoDashes = trip.serviceDate.replace("-", "")
            val valid = OfflineHandoverVerifier.verifyDailyPin(
                learnerId = stop.learnerId,
                serviceDateYyyyMmDd = dateNoDashes,
                directionCode = trip.directionCode,
                candidatePin = candidatePin,
                expectedVerifierHash = stop.dailyPinVerifierHash,
            )

            if (!valid) {
                _uiState.value = _uiState.value.copy(
                    errorMessage = "Incorrect 4-digit Daily PIN for ${stop.learnerFullName}.",
                )
                return@launch
            }

            _uiState.value = _uiState.value.copy(pinDialogStop = null)
            recordVerifiedHandover(
                tripId = tripId,
                stop = stop,
                method = "FallbackPin",
                qrPayload = null,
                fallbackPin = candidatePin.trim(),
            )
        }
    }

    fun syncOutboxNow() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
            val result = container.outboxSyncEngine.flushPending()
            HandoverSyncWorker.enqueue(app)
            _uiState.value = _uiState.value.copy(
                isLoading = false,
                statusBanner = if (result.retryNeeded) {
                    "Synced ${result.syncedCount}. Remaining queued for connectivity restoration."
                } else {
                    "Outbox sync complete: ${result.syncedCount} synced, ${result.rejectedCount} rejected."
                },
            )
        }
    }

    private fun recordVerifiedHandover(
        tripId: String,
        stop: CachedManifestStopEntity,
        method: String,
        qrPayload: String?,
        fallbackPin: String?,
    ) {
        viewModelScope.launch {
            val nextAction = if (stop.attendanceStatus == "PickedUp") "DroppedOff" else "PickedUp"
            val nowUtc = Instant.now().toString()
            val loc = locationSnapshotFlow.value

            val event = OfflineHandoverEventEntity(
                eventId = UUID.randomUUID().toString(),
                tripId = tripId,
                learnerId = stop.learnerId,
                action = nextAction,
                method = method,
                recordedAtUtc = nowUtc,
                latitude = loc.latitude,
                longitude = loc.longitude,
                qrPayload = qrPayload,
                fallbackPin = fallbackPin,
                syncState = OutboxSyncState.PENDING,
                lastError = null,
            )

            outboxDao.enqueue(event)
            tripDao.updateStopAttendance(
                tripId = tripId,
                learnerId = stop.learnerId,
                attendanceStatus = nextAction,
                handoverMethod = method,
                timestampUtc = nowUtc,
                wasSyncedOffline = true,
            )
            HandoverSyncWorker.enqueue(app)

            _uiState.value = _uiState.value.copy(
                errorMessage = null,
                statusBanner = "Verified ${stop.learnerFullName} ($nextAction via $method). Queued for outbox sync.",
            )
        }
    }

    private suspend fun persistManifest(manifest: TripManifestDto) {
        val existingStops = tripDao.getStopsForTrip(manifest.tripId).associateBy { it.learnerId }
        val pendingCount = outboxDao.getPendingOrdered().count { it.tripId == manifest.tripId }

        val tripEntity = CachedTripEntity(
            tripId = manifest.tripId,
            routeId = manifest.routeId,
            routeName = manifest.routeName,
            direction = manifest.direction,
            directionCode = manifest.directionCode,
            serviceDate = manifest.serviceDate,
            status = manifest.status,
            schoolId = manifest.schoolId,
            schoolName = manifest.schoolName,
            schoolGateLatitude = manifest.schoolGateLatitude,
            schoolGateLongitude = manifest.schoolGateLongitude,
            schoolGateGeofenceRadiusMeters = manifest.schoolGateGeofenceRadiusMeters,
            vehicleRegistration = manifest.vehicleRegistration,
            driverComplianceStatus = manifest.driverComplianceStatus,
            cachedAtUtc = Instant.now().toString(),
        )

        val stopEntities = manifest.stops.map { stop ->
            val localStop = existingStops[stop.learnerId]
            val keepLocalOfflineState = pendingCount > 0 &&
                localStop != null &&
                localStop.attendanceStatus in listOf("PickedUp", "DroppedOff") &&
                stop.attendanceStatus == "Scheduled"

            CachedManifestStopEntity(
                manifestEntryId = stop.manifestEntryId,
                tripId = manifest.tripId,
                learnerId = stop.learnerId,
                learnerFullName = stop.learnerFullName,
                grade = stop.grade,
                schoolId = stop.schoolId,
                homeAddressText = stop.homeAddressText,
                emergencyContactPhone = stop.emergencyContactPhone,
                sequenceOrder = stop.sequenceOrder,
                stopLatitude = stop.stopLatitude,
                stopLongitude = stop.stopLongitude,
                bookingType = stop.bookingType,
                attendanceStatus = if (keepLocalOfflineState) localStop!!.attendanceStatus else stop.attendanceStatus,
                handoverMethod = if (keepLocalOfflineState) localStop!!.handoverMethod else stop.handoverMethod,
                handoverTimestampUtc = if (keepLocalOfflineState) localStop!!.handoverTimestampUtc else stop.handoverTimestampUtc,
                estimatedArrivalUtc = stop.estimatedArrivalUtc,
                qrPublicKeyVerifier = stop.qrPublicKeyVerifier,
                dailyPinVerifierHash = stop.dailyPinVerifierHash,
                wasSyncedOffline = if (keepLocalOfflineState) true else stop.wasSyncedOffline,
            )
        }

        tripDao.saveTripManifest(tripEntity, stopEntities)
    }
}
