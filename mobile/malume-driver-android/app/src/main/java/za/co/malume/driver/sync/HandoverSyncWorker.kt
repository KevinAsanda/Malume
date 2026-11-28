package za.co.malume.driver.sync

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import za.co.malume.driver.MalumeDriverApp
import java.util.concurrent.TimeUnit

class HandoverSyncWorker(
    appContext: Context,
    params: WorkerParameters,
) : CoroutineWorker(appContext, params) {

    override suspend fun doWork(): Result {
        val app = applicationContext as? MalumeDriverApp ?: return Result.failure()
        val session = app.container.sessionStore.currentSession
        if (!session.isAuthenticated) {
            return Result.success()
        }

        val outcome = app.container.outboxSyncEngine.flushPending()
        return if (outcome.retryNeeded) Result.retry() else Result.success()
    }

    companion object {
        private const val UNIQUE_WORK_NAME = "malume_handover_outbox_sync"

        fun enqueue(context: Context) {
            val constraints = Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .build()

            val request = OneTimeWorkRequestBuilder<HandoverSyncWorker>()
                .setConstraints(constraints)
                .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 10, TimeUnit.SECONDS)
                .build()

            WorkManager.getInstance(context.applicationContext)
                .enqueueUniqueWork(UNIQUE_WORK_NAME, ExistingWorkPolicy.REPLACE, request)
        }
    }
}
