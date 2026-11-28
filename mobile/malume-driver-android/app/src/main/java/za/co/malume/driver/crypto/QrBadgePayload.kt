package za.co.malume.driver.crypto

import java.util.UUID

data class QrBadgePayload(
    val learnerId: String,
    val schoolId: String,
    val signatureBase64: String,
) {
    fun format(): String = "$FORMAT_PREFIX:$learnerId:$schoolId:$signatureBase64"

    companion object {
        const val FORMAT_PREFIX = "MLM1"

        fun parse(rawPayload: String): QrBadgePayload? {
            val trimmed = rawPayload.trim()
            if (trimmed.isEmpty()) return null

            val parts = trimmed.split(':', limit = 4)
            if (parts.size != 4 || parts[0] != FORMAT_PREFIX) return null

            val learnerUuid = runCatching { UUID.fromString(parts[1]).toString().lowercase() }.getOrNull()
                ?: return null
            val schoolUuid = runCatching { UUID.fromString(parts[2]).toString().lowercase() }.getOrNull()
                ?: return null
            val signature = parts[3].trim()
            if (signature.isEmpty()) return null

            return QrBadgePayload(
                learnerId = learnerUuid,
                schoolId = schoolUuid,
                signatureBase64 = signature,
            )
        }
    }
}
