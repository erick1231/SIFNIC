package ni.sifnic.movil.ui

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import com.google.gson.JsonObject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ni.sifnic.movil.SessionPrefs
import ni.sifnic.movil.api.SifnicRepository
import ni.sifnic.movil.databinding.FragmentLoginBinding

class LoginFragment : Fragment() {

    private var _binding: FragmentLoginBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, savedInstanceState: Bundle?): View {
        _binding = FragmentLoginBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        val prefs = SessionPrefs(requireContext())
        binding.btnLogin.setOnClickListener {
            val u = binding.editUser.text?.toString().orEmpty().trim()
            val p = binding.editPass.text?.toString().orEmpty()
            if (u.isBlank() || p.isBlank()) {
                binding.textError.visibility = View.VISIBLE
                binding.textError.text = "Completa usuario y contraseña."
                return@setOnClickListener
            }
            binding.textError.visibility = View.GONE
            binding.btnLogin.isEnabled = false
            lifecycleScope.launch {
                try {
                    val json = withContext(Dispatchers.IO) { SifnicRepository.login(u, p) }
                    if (!json.get("ok").asBoolean) {
                        throw Exception(json.get("message")?.asString ?: "No se pudo iniciar sesión")
                    }
                    val data = json.getAsJsonObject("data")
                        ?: throw Exception("Respuesta sin datos")
                    if (data.get("requirePasswordChange")?.asBoolean == true) {
                        throw Exception("Debes cambiar la contraseña desde la web antes de usar la app.")
                    }
                    val token = data.get("sessionToken")?.asString.orEmpty()
                    if (token.isBlank()) throw Exception("Sesión sin token")
                    prefs.sessionToken = token
                    prefs.displayName = data.get("displayName")?.asString ?: u
                    withContext(Dispatchers.Main) {
                        (activity as MainActivity).showHome()
                    }
                } catch (e: Exception) {
                    withContext(Dispatchers.Main) {
                        binding.textError.visibility = View.VISIBLE
                        binding.textError.text = e.message ?: "Error"
                        binding.btnLogin.isEnabled = true
                    }
                }
            }
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
