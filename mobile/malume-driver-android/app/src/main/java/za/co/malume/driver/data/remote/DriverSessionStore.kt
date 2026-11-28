package za.co.malume.driver.data.remote

import android.content.Context
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import za.co.malume.driver.data.remote.dto.AuthResponseDto

data class DriverSession(
    val auth: AuthResponseDto? = null,
    val apiBaseUrl: String = DEFAULT_API_BASE_URL,
) {
    val isAuthenticated: Boolean get() = auth != null

    companion object {
        const val DEFAULT_API_BASE_URL = "http://10.0.2.2:5144"
    }
}

class DriverSessionStore(context: Context) {

    private val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
    private val json = Json { ignoreUnknownKeys = true }

    private val _sessionFlow = MutableStateFlow(loadInitialSession())
    val sessionFlow: StateFlow<DriverSession> = _sessionFlow.asStateFlow()

    val currentSession: DriverSession get() = _sessionFlow.value

    fun saveLogin(auth: AuthResponseDto, apiBaseUrl: String) {
        val normalizedUrl = normalizeUrl(apiBaseUrl)
        prefs.edit()
            .putString(KEY_AUTH_JSON, json.encodeToString(auth))
            .putString(KEY_API_BASE_URL, normalizedUrl)
            .apply()
        _sessionFlow.value = DriverSession(auth = auth, apiBaseUrl = normalizedUrl)
    }

    fun updateApiBaseUrl(apiBaseUrl: String) {
        val normalizedUrl = normalizeUrl(apiBaseUrl)
        prefs.edit().putString(KEY_API_BASE_URL, normalizedUrl).apply()
        _sessionFlow.value = _sessionFlow.value.copy(apiBaseUrl = normalizedUrl)
    }

    fun clearAuth() {
        prefs.edit().remove(KEY_AUTH_JSON).apply()
        _sessionFlow.value = _sessionFlow.value.copy(auth = null)
    }

    private fun loadInitialSession(): DriverSession {
        val baseUrl = prefs.getString(KEY_API_BASE_URL, DriverSession.DEFAULT_API_BASE_URL)
            ?: DriverSession.DEFAULT_API_BASE_URL
        val rawAuth = prefs.getString(KEY_AUTH_JSON, null)
        val parsedAuth = rawAuth?.let {
            runCatching { json.decodeFromString<AuthResponseDto>(it) }.getOrNull()
        }
        return DriverSession(auth = parsedAuth, apiBaseUrl = normalizeUrl(baseUrl))
    }

    private fun normalizeUrl(url: String): String {
        val trimmed = url.trim().trimEnd('/')
        return trimmed.ifEmpty { DriverSession.DEFAULT_API_BASE_URL }
    }

    companion object {
        private const val PREFS_NAME = "malume_driver_session"
        private const val KEY_AUTH_JSON = "auth_json"
        private const val KEY_API_BASE_URL = "api_base_url"
    }
}
