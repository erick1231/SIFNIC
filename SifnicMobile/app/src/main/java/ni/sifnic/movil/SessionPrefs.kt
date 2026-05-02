package ni.sifnic.movil

import android.content.Context

class SessionPrefs(context: Context) {
    private val p = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    var sessionToken: String?
        get() = p.getString(KEY_TOKEN, null)
        set(value) {
            p.edit().apply {
                if (value.isNullOrBlank()) remove(KEY_TOKEN) else putString(KEY_TOKEN, value)
            }.apply()
        }

    var displayName: String?
        get() = p.getString(KEY_NAME, null)
        set(value) {
            p.edit().putString(KEY_NAME, value).apply()
        }

    fun clear() {
        p.edit().clear().apply()
    }

    companion object {
        private const val PREFS = "sifnic_sess"
        private const val KEY_TOKEN = "token"
        private const val KEY_NAME = "display_name"
    }
}
