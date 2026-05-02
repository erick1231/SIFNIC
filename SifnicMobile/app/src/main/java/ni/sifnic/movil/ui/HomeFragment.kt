package ni.sifnic.movil.ui

import android.content.Context
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import ni.sifnic.movil.SessionPrefs
import ni.sifnic.movil.databinding.FragmentHomeBinding

class HomeFragment : Fragment() {

    interface Listener {
        fun openCredits()
        fun openNewRequest()
    }

    private var _binding: FragmentHomeBinding? = null
    private val binding get() = _binding!!

    private var listener: Listener? = null

    override fun onAttach(context: Context) {
        super.onAttach(context)
        listener = context as? Listener
    }

    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, savedInstanceState: Bundle?): View {
        _binding = FragmentHomeBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        val prefs = SessionPrefs(requireContext())
        binding.textWelcome.text = "Hola, ${prefs.displayName ?: prefs.sessionToken?.take(8) ?: "usuario"}"

        binding.btnCredits.setOnClickListener { listener?.openCredits() }
        binding.btnNewRequest.setOnClickListener { listener?.openNewRequest() }
        binding.btnLogout.setOnClickListener {
            prefs.clear()
            (activity as? MainActivity)?.showLogin()
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
