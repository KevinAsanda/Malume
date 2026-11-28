package za.co.malume.driver.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import net.zetetic.database.sqlcipher.SupportOpenHelperFactory
import za.co.malume.driver.crypto.DatabasePassphraseManager

@Database(
    entities = [
        CachedTripEntity::class,
        CachedManifestStopEntity::class,
        OfflineHandoverEventEntity::class,
    ],
    version = 1,
    exportSchema = false,
)
abstract class MalumeDatabase : RoomDatabase() {
    abstract fun tripManifestDao(): TripManifestDao
    abstract fun offlineOutboxDao(): OfflineOutboxDao

    companion object {
        @Volatile
        private var instance: MalumeDatabase? = null

        fun getInstance(context: Context): MalumeDatabase {
            return instance ?: synchronized(this) {
                instance ?: buildEncryptedDatabase(context.applicationContext).also { instance = it }
            }
        }

        private fun buildEncryptedDatabase(appContext: Context): MalumeDatabase {
            System.loadLibrary("sqlcipher")
            val passphrase = DatabasePassphraseManager(appContext).getOrCreatePassphrase()
            val factory = SupportOpenHelperFactory(passphrase)

            return Room.databaseBuilder(
                appContext,
                MalumeDatabase::class.java,
                "malume_driver_encrypted.db",
            )
                .openHelperFactory(factory)
                .fallbackToDestructiveMigration(dropAllTables = true)
                .build()
        }
    }
}
