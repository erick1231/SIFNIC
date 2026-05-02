package ni.sifnic.movil.ui

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.fragment.app.commit
import ni.sifnic.movil.R
import ni.sifnic.movil.SessionPrefs
import ni.sifnic.movil.api.SifnicRepository

class MainActivity : AppCompatActivity(), HomeFragment.Listener, CreditsFragment.Listener {

    lateinit var prefs: SessionPrefs

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        prefs = SessionPrefs(this)
        SifnicRepository.bindSession { prefs.sessionToken }

        if (savedInstanceState == null) {
            if (prefs.sessionToken.isNullOrBlank()) {
                showLogin()
            } else {
                showHome()
            }
        }
    }

    fun showLogin() {
        supportFragmentManager.commit {
            replace(R.id.container, LoginFragment())
        }
    }

    fun showHome() {
        supportFragmentManager.commit {
            replace(R.id.container, HomeFragment())
        }
    }

    override fun openCredits() {
        supportFragmentManager.commit {
            replace(R.id.container, CreditsFragment())
            addToBackStack(null)
        }
    }

    override fun openNewRequest() {
        supportFragmentManager.commit {
            replace(R.id.container, NewRequestFragment())
            addToBackStack(null)
        }
    }

    override fun openPayment(creditId: Long) {
        val f = PaymentFragment().apply {
            arguments = Bundle().apply { putLong(PaymentFragment.ARG_ID, creditId) }
        }
        supportFragmentManager.commit {
            replace(R.id.container, f)
            addToBackStack(null)
        }
    }
}
