package ni.sifnic.movil.ui

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import com.google.gson.JsonArray
import ni.sifnic.movil.databinding.ItemCreditBinding

class CreditsAdapter(
    private val items: JsonArray,
    private val onClick: (Long) -> Unit,
) : RecyclerView.Adapter<CreditsAdapter.VH>() {

    class VH(val binding: ItemCreditBinding) : RecyclerView.ViewHolder(binding.root)

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): VH {
        val inf = LayoutInflater.from(parent.context)
        return VH(ItemCreditBinding.inflate(inf, parent, false))
    }

    override fun getItemCount(): Int = items.size()

    override fun onBindViewHolder(holder: VH, position: Int) {
        val o = items[position].asJsonObject
        val id = o.get("id").asLong
        holder.binding.textNumber.text = o.get("number")?.asString ?: "#"
        holder.binding.textClient.text = listOf(
            o.get("clientName")?.asString,
            o.get("clientIdentification")?.asString,
        ).filterNot { it.isNullOrBlank() }.joinToString(" · ")
        val cap = o.get("capitalBalance")?.takeUnless { it.isJsonNull }?.asJsonPrimitive?.toString() ?: "—"
        val cur = o.get("currency")?.asString ?: ""
        holder.binding.textBalance.text = "Saldo capital: $cap $cur · Estado: ${o.get("status")?.asString ?: ""}"

        holder.itemView.setOnClickListener { onClick(id) }
    }
}
