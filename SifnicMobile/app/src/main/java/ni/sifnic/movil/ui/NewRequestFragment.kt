package ni.sifnic.movil.ui

import android.net.Uri
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.AdapterView
import android.widget.ArrayAdapter
import androidx.activity.result.contract.ActivityResultContracts
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import com.google.gson.JsonArray
import com.google.gson.JsonObject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ni.sifnic.movil.api.SifnicRepository
import ni.sifnic.movil.databinding.FragmentNewRequestBinding
import java.io.File

class NewRequestFragment : Fragment() {

    private var _binding: FragmentNewRequestBinding? = null
    private val binding get() = _binding!!

    private var clientId: Long = 0
    private var fileFront: File? = null
    private var fileBack: File? = null
    private val products: MutableList<JsonObject> = mutableListOf()

    private val pickFront = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let {
            fileFront = copyUri(it, "cedula_frente.jpg")
            binding.textReqMsg.text = "Frente listo (${fileFront?.length() ?: 0} bytes)."
        }
    }

    private val pickBack = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let {
            fileBack = copyUri(it, "cedula_reverso.jpg")
            binding.textReqMsg.text = "Reverso listo (${fileBack?.length() ?: 0} bytes)."
        }
    }

    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, savedInstanceState: Bundle?): View {
        _binding = FragmentNewRequestBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        binding.btnPickFront.setOnClickListener { pickFront.launch("image/*") }
        binding.btnPickBack.setOnClickListener { pickBack.launch("image/*") }

        lifecycleScope.launch {
            try {
                val arr = withContext(Dispatchers.IO) { SifnicRepository.productosCredito() }
                products.clear()
                val labels = mutableListOf<String>()
                for (i in 0 until arr.size()) {
                    val p = arr[i].asJsonObject
                    products.add(p)
                    val code = p.get("code")?.asString ?: ""
                    val name = p.get("name")?.asString ?: ""
                    labels.add("$code — $name")
                }
                binding.spinnerProduct.adapter =
                    ArrayAdapter(requireContext(), android.R.layout.simple_spinner_dropdown_item, labels)
            } catch (e: Exception) {
                binding.textReqMsg.text = "No se cargaron productos: ${e.message}"
            }
        }

        binding.btnCreateClient.setOnClickListener {
            lifecycleScope.launch {
                try {
                    val payload = buildClientPayload()
                    val data = withContext(Dispatchers.IO) { SifnicRepository.crearCliente(payload) }
                    clientId = data.get("id")?.takeUnless { it.isJsonNull }?.asLong ?: 0L
                    if (clientId <= 0) throw IllegalStateException("Respuesta sin id de cliente")

                    withContext(Dispatchers.IO) {
                        fileFront?.let {
                            SifnicRepository.subirArchivoMovil(clientId, "CEDULA_FRENTE", it, "image/jpeg")
                        }
                        fileBack?.let {
                            SifnicRepository.subirArchivoMovil(clientId, "CEDULA_REVERSO", it, "image/jpeg")
                        }
                    }
                    binding.textReqMsg.text =
                        "Cliente #$clientId creado y archivos enviados (si seleccionó fotos)."
                } catch (e: Exception) {
                    binding.textReqMsg.text = e.message ?: "Error cliente"
                }
            }
        }

        binding.btnSubmitRequest.setOnClickListener {
            if (clientId <= 0) {
                binding.textReqMsg.text = "Primero crea el cliente."
                return@setOnClickListener
            }
            val pos = binding.spinnerProduct.selectedItemPosition
            if (pos < 0 || pos >= products.size) {
                binding.textReqMsg.text = "Selecciona un tipo de crédito."
                return@setOnClickListener
            }
            val product = products[pos]
            val amount = binding.editAmountReq.text?.toString()?.toDoubleOrNull() ?: 0.0
            val term = binding.editTerm.text?.toString()?.toIntOrNull() ?: 0
            if (amount <= 0 || term <= 0) {
                binding.textReqMsg.text = "Monto y plazo deben ser válidos."
                return@setOnClickListener
            }

            lifecycleScope.launch {
                try {
                    val declaredIncome = 8000.0
                    val declaredExpenses = 2000.0

                    val checklist = JsonObject().apply {
                        addProperty("identification", true)
                        addProperty("fileCompleted", fileFront != null && fileBack != null)
                        addProperty("homeBusinessVisit", false)
                        addProperty("paymentCapacity", true)
                        addProperty("conamiReview", false)
                        addProperty("listCheck", false)
                        addProperty("guaranteeReview", false)
                    }

                    val body = JsonObject().apply {
                        addProperty("clientId", clientId)
                        addProperty("amount", amount)
                        addProperty("termMonths", term)
                        addProperty("annualRate", product.get("annualRate")?.asDouble ?: 0.0)
                        addProperty("commissionRate", product.get("commissionRate")?.asDouble ?: 0.0)
                        addProperty("slidingRate", product.get("slidingRate")?.asDouble ?: 0.0)
                        addProperty("moraRate", product.get("moraRate")?.asDouble ?: 0.0)
                        addProperty("currency", product.get("currency")?.asString ?: "NIO")
                        addProperty("destination", binding.editDestination.text?.toString()?.trim() ?: "GENERAL")
                        addProperty("product", product.get("code")?.asString ?: "")
                        addProperty("frequency", product.get("frequency")?.asString ?: "MENSUAL")
                        addProperty("installmentType", product.get("installmentType")?.asString ?: "NIVELADA")
                        addProperty("declaredIncome", declaredIncome)
                        addProperty("declaredExpenses", declaredExpenses)
                        addProperty("incomeSource", "NEGOCIO")
                        addProperty("financedActivity", "COMERCIO")
                        addProperty("guaranteeType", "NINGUNA")
                        addProperty("guaranteeDescription", "")
                        addProperty("guaranteeValue", 0)
                        addProperty("riskLevel", "MEDIO")
                        addProperty("status", "TRAMITE")
                        addProperty("prospectionStage", "PROSPECTO")
                        addProperty("requiresCommittee", false)
                        add("checklist", checklist)
                    }

                    val data = withContext(Dispatchers.IO) { SifnicRepository.crearSolicitud(body) }
                    val req = data.getAsJsonObject("request")
                    val num = req?.get("number")?.asString ?: ""
                    binding.textReqMsg.text = "Solicitud registrada: $num (estado trámite)."
                } catch (e: Exception) {
                    binding.textReqMsg.text = e.message ?: "Error solicitud"
                }
            }
        }
    }

    private fun buildClientPayload(): JsonObject {
        val cedula = binding.editCedula.text?.toString()?.trim().orEmpty().uppercase()
        val names = binding.editNames.text?.toString()?.trim().orEmpty()
        val lastNames = binding.editLastNames.text?.toString()?.trim().orEmpty()
        val phone = binding.editPhone.text?.toString()?.trim().orEmpty()
        val address = binding.editAddress.text?.toString()?.trim().orEmpty()
        val birth = binding.editBirth.text?.toString()?.trim().orEmpty()

        return JsonObject().apply {
            addProperty("identificationType", "CEDULA")
            addProperty("cedula", cedula)
            addProperty("names", names)
            addProperty("lastNames", lastNames)
            addProperty("clientType", "INDIVIDUAL")
            addProperty("status", "PROSPECTO")
            addProperty("branch", "MANAGUA")
            addProperty("relationship", "NUEVO")
            addProperty("phone", phone)
            addProperty("mobile", phone)
            addProperty("address", address)
            addProperty("monthlyIncome", 8000)
            addProperty("monthlyExpenses", 2000)
            addProperty("riskLevel", "MEDIO")
            addProperty("fileStatus", "INCOMPLETO")
            if (birth.isNotBlank()) addProperty("birthDate", birth)
        }
    }

    private fun copyUri(uri: Uri, fileName: String): File {
        val out = File(requireContext().cacheDir, fileName)
        requireContext().contentResolver.openInputStream(uri)?.use { input ->
            out.outputStream().use { output -> input.copyTo(output) }
        } ?: throw IllegalStateException("No se pudo leer la imagen.")
        return out
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
