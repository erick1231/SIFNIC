package ni.sifnic.movil.api

import com.google.gson.Gson
import com.google.gson.JsonArray
import com.google.gson.JsonObject
import ni.sifnic.movil.BuildConfig
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.File
import java.util.concurrent.TimeUnit

/**
 * Cliente HTTP contra el API ASP.NET [Sifnic.Api]. La base de datos `credito` solo se escribe por el servidor.
 */
object SifnicRepository {

    private val gson = Gson()
    private var tokenGetter: () -> String? = { null }

    fun bindSession(tokenGetter: () -> String?) {
        this.tokenGetter = tokenGetter
    }

    private fun http(): OkHttpClient {
        return OkHttpClient.Builder()
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(120, TimeUnit.SECONDS)
            .addInterceptor { chain ->
                val b = chain.request().newBuilder()
                    .header("Accept", "application/json")
                tokenGetter()?.takeIf { it.isNotBlank() }?.let { b.header("X-Session-Token", it) }
                chain.proceed(b.build())
            }
            .build()
    }

    private fun url(path: String): String {
        val base = BuildConfig.API_BASE_URL.trimEnd('/') + "/"
        val u = (base + path.trimStart('/')).toHttpUrlOrNull()
            ?: throw IllegalStateException("URL base inválida en BuildConfig.")
        return u.toString()
    }

    private fun jsonPost(path: String, jsonBody: String): JsonObject {
        val body = jsonBody.toRequestBody("application/json; charset=utf-8".toMediaType())
        val req = Request.Builder().url(url(path)).post(body).build()
        http().newCall(req).execute().use { resp ->
            val text = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                throw ApiException(resp.code, parseMessage(text))
            }
            return gson.fromJson(text, JsonObject::class.java)
        }
    }

    private fun parseMessage(text: String): String {
        return try {
            val o = gson.fromJson(text, JsonObject::class.java)
            o.get("message")?.asString ?: text.ifBlank { "Error HTTP" }
        } catch (_: Exception) {
            text.ifBlank { "Error HTTP" }
        }
    }

    private fun get(path: String): JsonObject {
        val req = Request.Builder().url(url(path)).get().build()
        http().newCall(req).execute().use { resp ->
            val text = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                throw ApiException(resp.code, parseMessage(text))
            }
            return gson.fromJson(text, JsonObject::class.java)
        }
    }

    fun login(username: String, password: String): JsonObject {
        val body = JsonObject().apply {
            addProperty("Username", username.trim())
            addProperty("Password", password)
        }
        return jsonPost("Seguridad/Login", gson.toJson(body))
    }

    fun carteraListar(search: String = ""): JsonArray {
        val qs = StringBuilder("?status=TODOS")
        if (search.isNotBlank()) {
            qs.append("&search=").append(java.net.URLEncoder.encode(search, "UTF-8"))
        }
        val res = get("Cartera/Listar$qs")
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "Error")
        val d = res.get("data") ?: return JsonArray()
        return if (d.isJsonArray) d.asJsonArray else JsonArray()
    }

    /** Para spinners y validación de montos. */
    fun productosCredito(): JsonArray {
        val res = get("SolicitudesCredito/ProductosCredito")
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "Error")
        return res.getAsJsonArray("data") ?: JsonArray()
    }

    fun crearCliente(payload: JsonObject): JsonObject {
        val res = jsonPost("Clientes/Crear", gson.toJson(payload))
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "Error cliente")
        return res.getAsJsonObject("data") ?: JsonObject()
    }

    fun subirArchivoMovil(idCliente: Long, tipoDocumento: String, file: File, mime: String) {
        val mt = (mime.ifBlank { "image/jpeg" }).toMediaType()
        val fileBody = file.asRequestBody(mt)
        val multipart = MultipartBody.Builder().setType(MultipartBody.FORM)
            .addFormDataPart("idCliente", idCliente.toString())
            .addFormDataPart("tipoDocumento", tipoDocumento)
            .addFormDataPart("archivo", file.name, fileBody)
            .build()

        val req = Request.Builder()
            .url(url("Clientes/SubirArchivoMovil"))
            .post(multipart)
            .header("Accept", "application/json")
            .apply {
                tokenGetter()?.takeIf { it.isNotBlank() }?.let { header("X-Session-Token", it) }
            }
            .build()

        http().newCall(req).execute().use { resp ->
            val text = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                throw ApiException(resp.code, parseMessage(text))
            }
            val jo = gson.fromJson(text, JsonObject::class.java)
            if (!jo.get("ok").asBoolean) {
                throw ApiException(400, jo.get("message")?.asString ?: "No se pudo subir el archivo")
            }
        }
    }

    fun crearSolicitud(payload: JsonObject): JsonObject {
        val res = jsonPost("SolicitudesCredito/Crear", gson.toJson(payload))
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "Error solicitud")
        return res.getAsJsonObject("data") ?: JsonObject()
    }

    fun cajaCatalogos(): JsonObject {
        val res = get("Caja/Catalogos")
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "Error caja")
        return res.getAsJsonObject("data") ?: JsonObject()
    }

    fun abrirSesionCaja(branch: String, observation: String = "Apertura app móvil") {
        val body = JsonObject().apply {
            addProperty("branch", branch)
            addProperty("openingNio", 0)
            addProperty("openingUsd", 0)
            addProperty("observation", observation)
            add("breakdown", JsonArray())
        }
        val res = jsonPost("Caja/AbrirSesion", gson.toJson(body))
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "No se abrió caja")
    }

    fun aplicarPago(
        creditId: Long,
        amount: Double,
        currency: String,
        method: String,
        manualReceipt: String?,
        observation: String?,
        exchangeRate: Double? = null,
    ): JsonObject {
        val body = JsonObject().apply {
            addProperty("creditId", creditId)
            addProperty("amount", amount)
            addProperty("currency", currency)
            addProperty("method", method)
            if (!manualReceipt.isNullOrBlank()) addProperty("manualReceipt", manualReceipt)
            if (!observation.isNullOrBlank()) addProperty("observation", observation)
            if (exchangeRate != null && exchangeRate > 0) addProperty("exchangeRate", exchangeRate)
        }
        val res = jsonPost("Caja/AplicarPago", gson.toJson(body))
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "No se aplicó el pago")
        return res.getAsJsonObject("data") ?: JsonObject()
    }

    fun creditoDetalle(id: Long): JsonObject {
        val res = get("Cartera/Obtener?id=$id")
        if (!res.get("ok").asBoolean) throw ApiException(400, res.get("message")?.asString ?: "Error préstamo")
        return res.getAsJsonObject("data") ?: JsonObject()
    }

    fun voucherUrl(printPath: String): String {
        val token = tokenGetter().orEmpty()
        val base = BuildConfig.API_BASE_URL.trimEnd('/')
        val path = if (printPath.startsWith("/")) printPath else "/$printPath"
        val sep = if (path.contains("?")) "&" else "?"
        return "$base$path${sep}sessionToken=${java.net.URLEncoder.encode(token, "UTF-8")}"
    }

    class ApiException(val code: Int, override val message: String) : Exception(message)
}
