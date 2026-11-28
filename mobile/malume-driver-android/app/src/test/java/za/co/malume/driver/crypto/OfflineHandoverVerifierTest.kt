package za.co.malume.driver.crypto

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.charset.StandardCharsets
import java.security.KeyPairGenerator
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import java.util.Base64

class OfflineHandoverVerifierTest {

    @Test
    fun verifyQrSignature_validEcdsaBadge_returnsValid() {
        val kpg = KeyPairGenerator.getInstance("EC")
        kpg.initialize(ECGenParameterSpec("secp256r1"))
        val keyPair = kpg.generateKeyPair()

        val learnerId = "11111111-2222-3333-4444-555555555555"
        val schoolId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
        val canonical = "MLM1:$learnerId:$schoolId"

        val signer = Signature.getInstance("SHA256withECDSA")
        signer.initSign(keyPair.private)
        signer.update(canonical.toByteArray(StandardCharsets.UTF_8))
        val sigBase64 = Base64.getEncoder().encodeToString(signer.sign())
        val pubBase64 = Base64.getEncoder().encodeToString(keyPair.public.encoded)
        val rawPayload = "$canonical:$sigBase64"

        val result = OfflineHandoverVerifier.verifyQrSignature(
            rawPayload = rawPayload,
            expectedLearnerId = learnerId,
            expectedSchoolId = schoolId,
            publicKeyBase64 = pubBase64,
        )

        assertTrue(result is QrVerificationResult.Valid)
    }

    @Test
    fun verifyQrSignature_tamperedOrMismatchedLearner_returnsInvalid() {
        val kpg = KeyPairGenerator.getInstance("EC")
        kpg.initialize(ECGenParameterSpec("secp256r1"))
        val keyPair = kpg.generateKeyPair()

        val learnerId = "11111111-2222-3333-4444-555555555555"
        val schoolId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
        val canonical = "MLM1:$learnerId:$schoolId"

        val signer = Signature.getInstance("SHA256withECDSA")
        signer.initSign(keyPair.private)
        signer.update(canonical.toByteArray(StandardCharsets.UTF_8))
        val sigBase64 = Base64.getEncoder().encodeToString(signer.sign())
        val pubBase64 = Base64.getEncoder().encodeToString(keyPair.public.encoded)
        val rawPayload = "$canonical:$sigBase64"

        val wrongLearner = OfflineHandoverVerifier.verifyQrSignature(
            rawPayload = rawPayload,
            expectedLearnerId = "99999999-2222-3333-4444-555555555555",
            expectedSchoolId = schoolId,
            publicKeyBase64 = pubBase64,
        )
        assertTrue(wrongLearner is QrVerificationResult.Invalid)

        val tampered = rawPayload.dropLast(4) + "AAAA"
        val tamperedResult = OfflineHandoverVerifier.verifyQrSignature(
            rawPayload = tampered,
            expectedLearnerId = learnerId,
            expectedSchoolId = schoolId,
            publicKeyBase64 = pubBase64,
        )
        assertTrue(tamperedResult is QrVerificationResult.Invalid)
    }

    @Test
    fun dailyRotatingPin_deriveAndVerifyOffline_matchesAndRejectsWrongPin() {
        val salt = ByteArray(32) { idx -> (idx + 7).toByte() }
        val learnerId = "11111111-2222-3333-4444-555555555555"
        val serviceDate = "20261128"
        val directionCode = 1

        val pin = OfflineHandoverVerifier.deriveDailyPin(salt, learnerId, serviceDate, directionCode)
        assertEquals(4, pin.length)
        assertTrue(pin.toInt() in 1000..9999)

        val verifierHash = OfflineHandoverVerifier.computePinVerifierHash(
            learnerId = learnerId,
            serviceDateYyyyMmDd = serviceDate,
            directionCode = directionCode,
            candidatePin = pin,
        )
        assertEquals(64, verifierHash.length)

        assertTrue(
            OfflineHandoverVerifier.verifyDailyPin(
                learnerId = learnerId,
                serviceDateYyyyMmDd = serviceDate,
                directionCode = directionCode,
                candidatePin = pin,
                expectedVerifierHash = verifierHash,
            ),
        )

        val wrongPin = if (pin == "9999") "1000" else (pin.toInt() + 1).toString()
        assertFalse(
            OfflineHandoverVerifier.verifyDailyPin(
                learnerId = learnerId,
                serviceDateYyyyMmDd = serviceDate,
                directionCode = directionCode,
                candidatePin = wrongPin,
                expectedVerifierHash = verifierHash,
            ),
        )
    }
}
