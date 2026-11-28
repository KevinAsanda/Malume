package za.co.malume.driver.crypto

import java.nio.charset.StandardCharsets
import java.security.KeyFactory
import java.security.MessageDigest
import java.security.Signature
import java.security.spec.X509EncodedKeySpec
import java.util.Base64
import java.util.Locale
import java.util.UUID
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

sealed interface QrVerificationResult {
    data object Valid : QrVerificationResult
    data class Invalid(val reason: String) : QrVerificationResult
}

object OfflineHandoverVerifier {

    fun verifyQrSignature(
        rawPayload: String,
        expectedLearnerId: String,
        expectedSchoolId: String,
        publicKeyBase64: String,
    ): QrVerificationResult {
        val parsed = QrBadgePayload.parse(rawPayload)
            ?: return QrVerificationResult.Invalid("Unrecognized Malume QR badge format.")

        val normalizedLearner = runCatching { UUID.fromString(expectedLearnerId).toString().lowercase() }.getOrNull()
            ?: return QrVerificationResult.Invalid("Invalid expected learner ID.")
        val normalizedSchool = runCatching { UUID.fromString(expectedSchoolId).toString().lowercase() }.getOrNull()
            ?: return QrVerificationResult.Invalid("Invalid expected school ID.")

        if (parsed.learnerId != normalizedLearner) {
            return QrVerificationResult.Invalid("QR badge belongs to a different learner.")
        }
        if (parsed.schoolId != normalizedSchool) {
            return QrVerificationResult.Invalid("QR badge school ID does not match.")
        }

        val publicKeyBytes = runCatching { Base64.getDecoder().decode(publicKeyBase64.trim()) }.getOrNull()
            ?: return QrVerificationResult.Invalid("Missing or corrupt QR verifier public key.")
        val signatureBytes = runCatching { Base64.getDecoder().decode(parsed.signatureBase64) }.getOrNull()
            ?: return QrVerificationResult.Invalid("Corrupt QR signature encoding.")

        val verified = runCatching {
            val keySpec = X509EncodedKeySpec(publicKeyBytes)
            val publicKey = KeyFactory.getInstance("EC").generatePublic(keySpec)
            val verifier = Signature.getInstance("SHA256withECDSA")
            verifier.initVerify(publicKey)
            val canonicalInput = "${QrBadgePayload.FORMAT_PREFIX}:$normalizedLearner:$normalizedSchool"
            verifier.update(canonicalInput.toByteArray(StandardCharsets.UTF_8))
            verifier.verify(signatureBytes)
        }.getOrDefault(false)

        return if (verified) {
            QrVerificationResult.Valid
        } else {
            QrVerificationResult.Invalid("ECDSA signature verification failed.")
        }
    }

    fun deriveDailyPin(
        learnerSecretSalt: ByteArray,
        learnerId: String,
        serviceDateYyyyMmDd: String,
        directionCode: Int,
    ): String {
        require(learnerSecretSalt.size >= 16) { "Learner QR secret salt must be at least 16 bytes." }

        val normalizedLearner = UUID.fromString(learnerId).toString().lowercase()
        val inputMessage = "$normalizedLearner:$serviceDateYyyyMmDd:$directionCode"

        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(learnerSecretSalt, "HmacSHA256"))
        val digest = mac.doFinal(inputMessage.toByteArray(StandardCharsets.UTF_8))

        val offset = digest[digest.size - 1].toInt() and 0x0F
        val binaryCode = ((digest[offset].toInt() and 0x7F) shl 24) or
            ((digest[offset + 1].toInt() and 0xFF) shl 16) or
            ((digest[offset + 2].toInt() and 0xFF) shl 8) or
            (digest[offset + 3].toInt() and 0xFF)

        val fourDigitPin = (binaryCode % 9000) + 1000
        return String.format(Locale.US, "%04d", fourDigitPin)
    }

    fun computePinVerifierHash(
        learnerId: String,
        serviceDateYyyyMmDd: String,
        directionCode: Int,
        candidatePin: String,
    ): String {
        val normalizedLearner = UUID.fromString(learnerId).toString().lowercase()
        val payload = "$normalizedLearner:$serviceDateYyyyMmDd:$directionCode:${candidatePin.trim()}"
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(payload.toByteArray(StandardCharsets.UTF_8))

        val sb = StringBuilder(digest.size * 2)
        for (byteValue in digest) {
            sb.append(String.format(Locale.US, "%02X", byteValue.toInt() and 0xFF))
        }
        return sb.toString()
    }

    fun verifyDailyPin(
        learnerId: String,
        serviceDateYyyyMmDd: String,
        directionCode: Int,
        candidatePin: String,
        expectedVerifierHash: String,
    ): Boolean {
        val trimmedPin = candidatePin.trim()
        if (trimmedPin.length != 4 || !trimmedPin.all { it.isDigit() }) {
            return false
        }

        val computedHash = computePinVerifierHash(
            learnerId = learnerId,
            serviceDateYyyyMmDd = serviceDateYyyyMmDd,
            directionCode = directionCode,
            candidatePin = trimmedPin,
        )

        return MessageDigest.isEqual(
            computedHash.toByteArray(StandardCharsets.UTF_8),
            expectedVerifierHash.trim().uppercase(Locale.US).toByteArray(StandardCharsets.UTF_8),
        )
    }
}
