(() => {
  const ensureDialog = () => {
    let backdrop = document.getElementById("unsavedGuardBackdrop");
    if (backdrop) return backdrop;

    backdrop = document.createElement("div");
    backdrop.id = "unsavedGuardBackdrop";
    backdrop.className = "unsaved-guard-backdrop";
    backdrop.hidden = true;
    backdrop.innerHTML = `
      <section class="unsaved-guard-dialog" role="dialog" aria-modal="true" aria-labelledby="unsavedGuardTitle">
        <div>
          <span class="eyebrow">Cambios sin guardar</span>
          <h2 id="unsavedGuardTitle">Tienes cambios sin guardar</h2>
          <p>¿Deseas salir sin guardar?</p>
        </div>
        <footer>
          <button class="ghost-button" type="button" data-unsaved-action="stay">Seguir editando</button>
          <button class="ghost-button" type="button" data-unsaved-action="discard">Salir sin guardar</button>
          <button class="primary-button" type="button" data-unsaved-action="save">Guardar y salir</button>
        </footer>
      </section>`;
    document.body.appendChild(backdrop);
    return backdrop;
  };

  const open = ({ onSave } = {}) =>
    new Promise((resolve) => {
      const backdrop = ensureDialog();
      const saveButton = backdrop.querySelector('[data-unsaved-action="save"]');
      saveButton.hidden = typeof onSave !== "function";
      backdrop.hidden = false;
      backdrop.classList.add("is-open");

      const finish = (result) => {
        backdrop.hidden = true;
        backdrop.classList.remove("is-open");
        backdrop.removeEventListener("click", onBackdrop);
        backdrop.querySelectorAll("[data-unsaved-action]").forEach((button) => {
          button.removeEventListener("click", onAction);
          button.disabled = false;
        });
        saveButton.textContent = "Guardar y salir";
        resolve(result);
      };

      const onAction = async (event) => {
        const action = event.currentTarget.dataset.unsavedAction;
        if (action === "stay") {
          finish("stay");
          return;
        }
        if (action === "discard") {
          finish("discard");
          return;
        }
        try {
          saveButton.disabled = true;
          saveButton.textContent = "Guardando...";
          const saved = await onSave();
          finish(saved === false ? "stay" : "save");
        } catch {
          finish("stay");
        }
      };

      const onBackdrop = (event) => {
        if (event.target === backdrop) finish("stay");
      };

      backdrop.querySelectorAll("[data-unsaved-action]").forEach((button) => {
        button.addEventListener("click", onAction);
      });
      backdrop.addEventListener("click", onBackdrop);
      backdrop.querySelector('[data-unsaved-action="stay"]')?.focus();
    });

  window.SifnicUnsavedGuard = { open };
})();
