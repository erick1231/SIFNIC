(() => {
  const model = window.RRHHModel;
  const view = window.RRHHView;

  const state = {
    activeSectionId: "empleados",
    activeFilter: "Todos",
    search: "",
    activeRecordId: null,
  };

  const getActiveSection = () => model.getSectionById(state.activeSectionId);

  const getFilteredRecords = () => {
    const section = getActiveSection();
    return model.filterRecords(section, state.search, state.activeFilter);
  };

  const syncActiveRecord = (records) => {
    const currentExists = records.some((record) => record.id === state.activeRecordId);
    if (!currentExists) {
      state.activeRecordId = records[0]?.id || null;
    }
  };

  const render = () => {
    const sections = model.getSections();
    const section = getActiveSection();
    const records = getFilteredRecords();

    syncActiveRecord(records);

    const selectedRecord =
      records.find((record) => record.id === state.activeRecordId) || null;

    view.renderSectionNav(sections, state.activeSectionId);
    view.renderDesk(section, records, state.activeRecordId, state.activeFilter);
    view.renderInspector(selectedRecord);

    bindViewEvents();
  };

  const bindViewEvents = () => {
    view.elements.sectionNav.querySelectorAll("[data-section-id]").forEach((button) => {
      button.addEventListener("click", () => {
        state.activeSectionId = button.dataset.sectionId;
        state.activeFilter = "Todos";
        state.search = "";
        view.elements.recordSearch.value = "";
        state.activeRecordId = null;
        render();
      });
    });

    view.elements.toolbarFilters.querySelectorAll("[data-filter-name]").forEach((button) => {
      button.addEventListener("click", () => {
        state.activeFilter = button.dataset.filterName;
        state.activeRecordId = null;
        render();
      });
    });

    view.elements.tableBody.querySelectorAll("[data-record-id]").forEach((row) => {
      row.addEventListener("click", () => {
        state.activeRecordId = row.dataset.recordId;
        render();
      });
    });
  };

  const bindStaticEvents = () => {
    view.elements.backToDashboard?.addEventListener("click", () => {
      window.location.href = "../../dashboard.html";
    });

    view.elements.closeSession?.addEventListener("click", () => {
      model.clearSession();
      window.location.href = "../../index.html";
    });

    view.elements.recordSearch?.addEventListener("input", (event) => {
      state.search = event.target.value;
      state.activeRecordId = null;
      render();
    });
  };

  const boot = () => {
    const session = model.getSession();

    if (!session) {
      window.location.href = "../../index.html";
      return;
    }

    view.setSession(session);
    bindStaticEvents();
    render();
  };

  boot();
})();
