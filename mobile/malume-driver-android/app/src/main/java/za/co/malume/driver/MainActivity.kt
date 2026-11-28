package za.co.malume.driver

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import za.co.malume.driver.ui.DriverScreen
import za.co.malume.driver.ui.DriverViewModel
import za.co.malume.driver.ui.components.FallbackPinDialog
import za.co.malume.driver.ui.screens.LoginScreen
import za.co.malume.driver.ui.screens.QrScannerScreen
import za.co.malume.driver.ui.screens.ScheduleScreen
import za.co.malume.driver.ui.screens.TripManifestScreen
import za.co.malume.driver.ui.theme.MalumeDriverTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            MalumeDriverTheme {
                MalumeDriverRoot()
            }
        }
    }
}

@Composable
private fun MalumeDriverRoot(viewModel: DriverViewModel = viewModel()) {
    val session by viewModel.sessionFlow.collectAsState()
    val uiState by viewModel.uiState.collectAsState()
    val screen by viewModel.currentScreen.collectAsState()
    val cachedTrips by viewModel.cachedTripsFlow.collectAsState()
    val selectedTrip by viewModel.selectedTripFlow.collectAsState()
    val selectedStops by viewModel.selectedStopsFlow.collectAsState()
    val pendingSyncCount by viewModel.pendingSyncCountFlow.collectAsState()
    val isOnline by viewModel.isOnlineFlow.collectAsState()
    val locationSnapshot by viewModel.locationSnapshotFlow.collectAsState()

    RequestLocationPermissionsOnLogin(isAuthenticated = session.isAuthenticated)

    when (val current = screen) {
        DriverScreen.Login -> LoginScreen(
            initialApiBaseUrl = session.apiBaseUrl,
            isLoading = uiState.isLoading,
            errorMessage = uiState.errorMessage,
            onLogin = viewModel::login,
        )

        DriverScreen.Schedule -> ScheduleScreen(
            driverName = session.auth?.fullName ?: "Malume Driver",
            uiState = uiState,
            cachedTrips = cachedTrips,
            pendingSyncCount = pendingSyncCount,
            isOnline = isOnline,
            onRefresh = viewModel::refreshSchedule,
            onSyncNow = viewModel::syncOutboxNow,
            onOpenTrip = viewModel::openTrip,
            onLogout = viewModel::logout,
        )

        is DriverScreen.TripManifest -> TripManifestScreen(
            trip = selectedTrip,
            stops = selectedStops,
            uiState = uiState,
            pendingSyncCount = pendingSyncCount,
            locationSnapshot = locationSnapshot,
            onBack = viewModel::navigateBackToSchedule,
            onStartTrip = viewModel::startTrip,
            onArriveGate = viewModel::arriveAtGate,
            onCompleteTrip = viewModel::completeTrip,
            onToggleGps = viewModel::toggleGpsTracking,
            onTriggerSos = viewModel::triggerSos,
            onScanQr = viewModel::openQrScanner,
            onEnterPin = viewModel::openPinDialog,
            onSyncNow = viewModel::syncOutboxNow,
        )

        is DriverScreen.QrScanner -> QrScannerScreen(
            stop = current.stop,
            errorMessage = uiState.errorMessage,
            onQrScanned = { rawPayload ->
                viewModel.verifyQrAndRecordOffline(current.tripId, current.stop, rawPayload)
            },
            onCancel = {
                viewModel.clearMessages()
                viewModel.closeQrScanner(current.tripId)
            },
        )
    }

    uiState.pinDialogStop?.let { stop ->
        val tripId = (screen as? DriverScreen.TripManifest)?.tripId
            ?: selectedTrip?.tripId
        if (tripId != null) {
            FallbackPinDialog(
                stop = stop,
                errorMessage = uiState.errorMessage,
                onVerifyPin = { pin -> viewModel.verifyPinAndRecordOffline(tripId, stop, pin) },
                onDismiss = {
                    viewModel.clearMessages()
                    viewModel.dismissPinDialog()
                },
            )
        }
    }
}

@Composable
private fun RequestLocationPermissionsOnLogin(isAuthenticated: Boolean) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val launcher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions(),
        onResult = {},
    )

    LaunchedEffect(isAuthenticated) {
        if (!isAuthenticated) return@LaunchedEffect
        val needed = buildList {
            if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                add(Manifest.permission.ACCESS_FINE_LOCATION)
                add(Manifest.permission.ACCESS_COARSE_LOCATION)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
        if (needed.isNotEmpty()) {
            launcher.launch(needed.toTypedArray())
        }
    }
}
