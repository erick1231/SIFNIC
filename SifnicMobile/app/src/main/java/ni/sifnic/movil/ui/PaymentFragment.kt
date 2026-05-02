package ni.sifnic.movil.ui

import android.annotation.SuppressLint
import android.app.AlertDialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.ArrayAdapter
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import com.google.gson.JsonObject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ni.sifnic.movil.api.SifnicRepository
import ni.sifnic.movil.databinding.FragmentPaymentBinding

class PaymentFragment : Fragment() {

    companion object {
        const val ARG_ID = "creditId"
    }

    private var _binding: FragmentPaymentBinding? = null
    private val binding get() = _binding!!

    private var creditId: Long = 0
    private var loanCurrency: String = "NIO"
    private var voucherUrl: String? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        creditId = arguments?.getLong(ARG_ID) ?: 0L
    }

    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, savedInstanceState: Bundle?): View {
        _binding = FragmentPaymentBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        val methods = listOf("EFECTIVO", "TRANSFERENCIA", "CHEQUE", "POS")
        binding.spinnerMethod.adapter =
            ArrayAdapter(requireContext(), android.R.layout.simple_spinner_dropdown_item, methods)

        binding.spinnerMethod.setOnItemSelectedListener(object : android.widget.AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: android.widget.AdapterView<*>?, v: View?, pos: Int, id: Long) {
                val m = methods[pos]
                binding.layoutRef.visibility = if (m == "EFECTIVO") View.GONE else View.VISIBLE
            }

            override fun onNothingSelected(parent: android.widget.AdapterView<*>?) {}
        })

        lifecycleScope.launch {
            loadLoanDetail()
        }

        binding.btnOpenCash.setOnClickListener {
            lifecycleScope.launch {
                try {
                    withContext(Dispatchers.IO) {
                        val cat = SifnicRepository.cajaCatalogos()
                        val branch = resolveBranch(cat)
                        SifnicRepository.abrirSesionCaja(branch)
                    }
                    binding.textPayMsg.text = "Caja abierta. Ya puedes aplicar el pago."
                } catch (e: Exception) {
                    binding.textPayMsg.text = e.message ?: "No se abrió caja"
                }
            }
        }

        binding.btnPay.setOnClickListener {
            val amtText = binding.editAmount.text?.toString().orEmpty()
            val amount = amtText.toDoubleOrNull() ?: 0.0
            if (amount <= 0) {
                binding.textPayMsg.text = "Indica un monto válido."
                return@setOnClickListener
            }
            val method = methods[binding.spinnerMethod.selectedItemPosition]
            val ref = binding.editRef.text?.toString()?.trim().orEmpty()
            if (method != "EFECTIVO" && ref.isBlank()) {
                binding.textPayMsg.text = "Indica referencia bancaria / POS / cheque."
                return@setOnClickListener
            }

            lifecycleScope.launch {
                try {
                    val data = withContext(Dispatchers.IO) {
                        SifnicRepository.aplicarPago(
                            creditId = creditId,
                            amount = amount,
                            currency = loanCurrency,
                            method = method,
                            manualReceipt = ref.ifBlank { null },
                            observation = "Pago app móvil",
                        )
                    }
                    val print = data.get("printUrl")?.asString
                    voucherUrl = print?.let { SifnicRepository.voucherUrl(it) }
                    binding.btnVoucher.isEnabled = !voucherUrl.isNullOrBlank()
                    binding.textPayMsg.text =
                        "Pago aplicado. Recibo: ${data.get("voucherNumber")?.asString ?: ""}"
                } catch (e: Exception) {
                    binding.textPayMsg.text = e.message ?: "Error al pagar"
                }
            }
        }

        binding.btnVoucher.setOnClickListener {
            val u = voucherUrl ?: return@setOnClickListener
            showVoucher(u)
        }
    }

    private suspend fun loadLoanDetail() {
        try {
            val data = withContext(Dispatchers.IO) { SifnicRepository.creditoDetalle(creditId) }
            val loan = data.getAsJsonObject("loan") ?: return
            loanCurrency = loan.get("currency")?.asString ?: "NIO"
            binding.textLoanTitle.text = loan.get("number")?.asString ?: "Crédito"
            val sb = StringBuilder()
            sb.appendLine(loan.get("clientName")?.asString ?: "")
            sb.appendLine("Capital: ${loan.get("capitalBalance")} $loanCurrency")
            sb.appendLine("Interés acum.: ${loan.get("interestBalance")} · Mora: ${loan.get("moraBalance")}")
            sb.appendLine("Estado: ${loan.get("status")?.asString ?: ""}")
            binding.textLoanDetail.text = sb.toString().trim()
        } catch (e: Exception) {
            binding.textLoanDetail.text = "No se pudo cargar: ${e.message}"
        }
    }

    private fun resolveBranch(cat: JsonObject): String {
        val assigned = cat.getAsJsonObject("assignedBranch")
        var branch = assigned?.get("value")?.asString?.trim().orEmpty()
        if (branch.isNotBlank()) return branch
        val branches = cat.getAsJsonArray("branches")
        if (branches != null && branches.size() > 0) {
            val first = branches[0].asJsonObject
            branch = first.get("value")?.asString?.trim().orEmpty()
            if (branch.isNotBlank()) return branch
        }
        return "MANAGUA"
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun showVoucher(url: String) {
        val wv = WebView(requireContext())
        wv.settings.javaScriptEnabled = true
        wv.webViewClient = WebViewClient()
        wv.loadUrl(url)
        AlertDialog.Builder(requireContext())
            .setTitle("Voucher")
            .setView(wv)
            .setPositiveButton("Cerrar", null)
            .show()
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
