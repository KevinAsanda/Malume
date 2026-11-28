package za.co.malume.driver.data.remote

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import za.co.malume.driver.data.remote.dto.AuthResponseDto
import za.co.malume.driver.data.remote.dto.BatchSyncHandoversRequestDto
import za.co.malume.driver.data.remote.dto.BatchSyncHandoversResponseDto
import za.co.malume.driver.data.remote.dto.DriverProfileDto
import za.co.malume.driver.data.remote.dto.DriverTripSummaryDto
import za.co.malume.driver.data.remote.dto.LoginRequestDto
import za.co.malume.driver.data.remote.dto.ProblemDetailsDto
import za.co.malume.driver.data.remote.dto.SosAlertDto
import za.co.malume.driver.data.remote.dto.TriggerSosRequestDto
import za.co.malume.driver.data.remote.dto.TripManifestDto
import java.io.IOException
import java.util.concurrent.TimeUnit

class ApiException(
    val statusCode: Int,
    override val message: String,
) : IOException(message)

interface HandoverSyncApi {
    suspend fun batchSyncHandovers(
        tripId: String,
        request: BatchSyncHandoversRequestDto,
    ): BatchSyncHandoversResponseDto
}

class MalumeApiService(
    private val sessionStore: DriverSessionStore,
) : HandoverSyncApi {

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .writeTimeout(20, TimeUnit.SECONDS)
        .build()

    private val json = Json {
        ignoreUnknownKeys = true
        encodeDefaults = true
    }

    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    suspend fun login(apiBaseUrl: String, email: String, password: String): AuthResponseDto {
        val base = apiBaseUrl.trim().trimEnd('/')
        val body = json.encodeToString(LoginRequestDto(email.trim(), password))
            .toRequestBody(jsonMediaType)
        val request = Request.Builder()
            .url("$base/api/v1/auth/login")
            .post(body)
            .build()
        return execute(request, AuthResponseDto.serializer(), attachAuth = false)
    }

    suspend fun getMyDriverProfile(): DriverProfileDto {
        val request = authorizedRequestBuilder("/api/v1/drivers/me").get().build()
        return execute(request, DriverProfileDto.serializer())
    }

    suspend fun getDriverSchedule(serviceDateYyyyMmDd: String? = null): List<DriverTripSummaryDto> {
        val path = if (serviceDateYyyyMmDd.isNullOrBlank()) {
            "/api/v1/trips/driver/schedule"
        } else {
            "/api/v1/trips/driver/schedule?serviceDate=$serviceDateYyyyMmDd"
        }
        val request = authorizedRequestBuilder(path).get().build()
        return execute(
            request,
            kotlinx.serialization.builtins.ListSerializer(DriverTripSummaryDto.serializer()),
        )
    }

    suspend fun getTripManifest(tripId: String): TripManifestDto {
        val request = authorizedRequestBuilder("/api/v1/trips/$tripId/manifest").get().build()
        return execute(request, TripManifestDto.serializer())
    }

    suspend fun startTrip(tripId: String): DriverTripSummaryDto {
        val emptyBody = ByteArray(0).toRequestBody(jsonMediaType)
        val request = authorizedRequestBuilder("/api/v1/trips/$tripId/start").post(emptyBody).build()
        return execute(request, DriverTripSummaryDto.serializer())
    }

    suspend fun arriveAtSchoolGate(tripId: String): DriverTripSummaryDto {
        val emptyBody = ByteArray(0).toRequestBody(jsonMediaType)
        val request = authorizedRequestBuilder("/api/v1/trips/$tripId/arrive-gate").post(emptyBody).build()
        return execute(request, DriverTripSummaryDto.serializer())
    }

    suspend fun completeTrip(tripId: String): DriverTripSummaryDto {
        val emptyBody = ByteArray(0).toRequestBody(jsonMediaType)
        val request = authorizedRequestBuilder("/api/v1/trips/$tripId/complete").post(emptyBody).build()
        return execute(request, DriverTripSummaryDto.serializer())
    }

    override suspend fun batchSyncHandovers(
        tripId: String,
        request: BatchSyncHandoversRequestDto,
    ): BatchSyncHandoversResponseDto {
        val body = json.encodeToString(request).toRequestBody(jsonMediaType)
        val httpRequest = authorizedRequestBuilder("/api/v1/trips/$tripId/handovers/batch-sync")
            .post(body)
            .build()
        return execute(httpRequest, BatchSyncHandoversResponseDto.serializer())
    }

    suspend fun triggerSos(
        tripId: String,
        latitude: Double,
        longitude: Double,
        notes: String? = null,
    ): SosAlertDto {
        val body = json.encodeToString(TriggerSosRequestDto(latitude, longitude, notes))
            .toRequestBody(jsonMediaType)
        val request = authorizedRequestBuilder("/api/v1/trips/$tripId/sos")
            .post(body)
            .build()
        return execute(request, SosAlertDto.serializer())
    }

    private fun authorizedRequestBuilder(path: String): Request.Builder {
        val session = sessionStore.currentSession
        val token = session.auth?.accessToken
            ?: throw ApiException(401, "Driver session is not authenticated.")
        return Request.Builder()
            .url("${session.apiBaseUrl}$path")
            .header("Authorization", "Bearer $token")
            .header("Accept", "application/json")
    }

    private suspend fun <T> execute(
        request: Request,
        deserializer: kotlinx.serialization.KSerializer<T>,
        attachAuth: Boolean = true,
    ): T = withContext(Dispatchers.IO) {
        client.newCall(request).execute().use { response ->
            val rawBody = response.body?.string().orEmpty()
            if (!response.isSuccessful) {
                if (attachAuth && response.code == 401) {
                    sessionStore.clearAuth()
                }
                val parsedError = runCatching { json.decodeFromString<ProblemDetailsDto>(rawBody) }.getOrNull()
                val message = parsedError?.detail
                    ?: parsedError?.title
                    ?: "HTTP ${response.code} ${response.message}"
                throw ApiException(response.code, message)
            }
            json.decodeFromString(deserializer, rawBody)
        }
    }
}
