package za.co.malume.driver.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import za.co.malume.driver.data.local.CachedManifestStopEntity
import za.co.malume.driver.data.local.CachedTripEntity
import za.co.malume.driver.location.LiveLocationSnapshot
import za.co.malume.driver.ui.DriverUiState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TripManifestScreen(
    trip: CachedTripEntity?,
    stops: List<CachedManifestStopEntity>,
    uiState: DriverUiState,
    pendingSyncCount: Int,
    locationSnapshot: LiveLocationSnapshot,
    onBack: () -> Unit,
    onStartTrip: (String) -> Unit,
    onArriveGate: (String) -> Unit,
    onCompleteTrip: (String) -> Unit,
    onToggleGps: (String) -> Unit,
    onTriggerSos: (String) -> Unit,
    onScanQr: (String, CachedManifestStopEntity) -> Unit,
    onEnterPin: (CachedManifestStopEntity) -> Unit,
    onSyncNow: () -> Unit,
) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(trip?.routeName ?: "Trip Manifest", style = MaterialTheme.typography.titleMedium)
                        Text(
                            text = "${trip?.status ?: ""} • ${trip?.vehicleRegistration ?: ""}",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                },
                navigationIcon = {
                    TextButton(onClick = onBack) {
                        Text("Back")
                    }
                },
                actions = {
                    if (trip != null) {
                        Button(
                            onClick = { onTriggerSos(trip.tripId) },
                            colors = ButtonDefaults.buttonColors(
                                containerColor = Color(0xFFC62828),
                                contentColor = Color.White,
                            ),
                        ) {
                            Text("SOS")
                        }
                    }
                },
            )
        },
    ) { paddingValues ->
        if (trip == null) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp),
            ) {
                Text("Loading cached manifest...")
            }
            return@Scaffold
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp),
        ) {
            if (uiState.isLoading) {
                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                Spacer(modifier = Modifier.height(8.dp))
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                when (trip.status) {
                    "Scheduled" -> Button(
                        onClick = { onStartTrip(trip.tripId) },
                        modifier = Modifier.weight(1f),
                    ) { Text("Start Trip") }

                    "EnRoute" -> Button(
                        onClick = { onArriveGate(trip.tripId) },
                        modifier = Modifier.weight(1f),
                    ) { Text("Arrived at Gate") }

                    "AtSchoolGate" -> Button(
                        onClick = { onCompleteTrip(trip.tripId) },
                        modifier = Modifier.weight(1f),
                    ) { Text("Complete Trip") }
                }

                OutlinedButton(onClick = { onToggleGps(trip.tripId) }) {
                    Text(if (locationSnapshot.isTracking) "GPS ON" else "GPS OFF")
                }
            }

            Text(
                text = "GPS: %.5f, %.5f (%.1f km/h)%s".format(
                    locationSnapshot.latitude,
                    locationSnapshot.longitude,
                    locationSnapshot.speedKph,
                    if (locationSnapshot.isSosActive) " [SOS 1 Hz]" else "",
                ),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 6.dp),
            )

            if (pendingSyncCount > 0) {
                Spacer(modifier = Modifier.height(8.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    Text(
                        text = "$pendingSyncCount offline handover(s) pending sync",
                        style = MaterialTheme.typography.bodySmall,
                    )
                    TextButton(onClick = onSyncNow) {
                        Text("Sync Now")
                    }
                }
            }

            uiState.errorMessage?.let { err ->
                Spacer(modifier = Modifier.height(8.dp))
                Card(
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text(err, color = MaterialTheme.colorScheme.onErrorContainer, modifier = Modifier.padding(12.dp))
                }
            }

            uiState.statusBanner?.let { banner ->
                Spacer(modifier = Modifier.height(8.dp))
                Card(modifier = Modifier.fillMaxWidth()) {
                    Text(banner, modifier = Modifier.padding(12.dp))
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            LazyColumn(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                items(stops, key = { it.manifestEntryId }) { stop ->
                    ElevatedCard(modifier = Modifier.fillMaxWidth()) {
                        Column(modifier = Modifier.padding(14.dp)) {
                            Text(
                                text = "#${stop.sequenceOrder} ${stop.learnerFullName} (${stop.grade})",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                            )
                            Text(
                                text = "${stop.homeAddressText} • Emergency: ${stop.emergencyContactPhone}",
                                style = MaterialTheme.typography.bodySmall,
                            )
                            Spacer(modifier = Modifier.height(6.dp))
                            Text(
                                text = "Status: ${stop.attendanceStatus} (${stop.handoverMethod})",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.primary,
                            )

                            val actionLabel = if (stop.attendanceStatus == "PickedUp") "Drop Off" else "Pick Up"
                            val canHandover = stop.attendanceStatus in listOf("Scheduled", "PickedUp")

                            if (canHandover) {
                                Spacer(modifier = Modifier.height(10.dp))
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                ) {
                                    Button(
                                        onClick = { onScanQr(trip.tripId, stop) },
                                        modifier = Modifier.weight(1f),
                                    ) {
                                        Text("Scan QR ($actionLabel)")
                                    }
                                    OutlinedButton(
                                        onClick = { onEnterPin(stop) },
                                        modifier = Modifier.weight(1f),
                                    ) {
                                        Text("Daily PIN")
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
