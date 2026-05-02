package ni.sifnic.movil.ui

import android.content.Context
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import com.google.gson.JsonArray
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ni.sifnic.movil.api.SifnicRepository
import ni.sifnic.movil.databinding.FragmentCreditsBinding

class CreditsFragment : Fragment() {

    interface Listener {
        fun openPayment(creditId: Long)
    }

    private var _binding: FragmentCreditsBinding? = null
    private val binding get() = _binding!!

    private var listener: Listener? = null

    override fun onAttach(context: Context) {
        super.onAttach(context)
        listener = context as? Listener
    }

    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, savedInstanceState: Bundle?): View {
        _binding = FragmentCreditsBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        binding.recycler.layoutManager = LinearLayoutManager(requireContext())
        loadList(JsonArray())

        lifecycleScope.launch {
            try {
                val arr = withContext(Dispatchers.IO) { SifnicRepository.carteraListar() }
                loadList(arr)
            } catch (e: Exception) {
                loadList(JsonArray())
                binding.textHint.text = "Error: ${e.message}"
            }
        }
    }

    private fun loadList(arr: JsonArray) {
        binding.recycler.adapter = CreditsAdapter(arr) { id ->
            listener?.openPayment(id)
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
