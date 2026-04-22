(function () {
    function byId(id) { return document.getElementById(id); }

    function createChatbotController() {
        const root = byId("onboarding-chatbot-root");
        if (!root) return null;

        const openBtn = byId("onboarding-chatbot-open");
        const panel = byId("onboarding-chatbot-panel");
        const closeBtn = byId("onboarding-chatbot-close");
        const refreshBtn = byId("onboarding-chatbot-refresh");
        const form = byId("onboarding-chatbot-form");
        const input = byId("onboarding-chatbot-input");
        const messagesEl = byId("onboarding-chatbot-messages");
        const checklistEl = byId("onboarding-chatbot-checklist");
        const nextBox = byId("onboarding-chatbot-next");
        const nextLabel = byId("onboarding-chatbot-next-label");
        const nextGo = byId("onboarding-chatbot-next-go");
        const quickButtons = document.querySelectorAll("[data-chatbot-quick]");

        const state = {
            nextPath: null,
            checklist: [
                { label: "Proyecto creado", done: false },
                { label: "Elemento creado", done: false },
                { label: "CR creada", done: false },
                { label: "Version creada", done: false },
                { label: "Trazabilidad Git vinculada", done: false }
            ]
        };

        function addMessage(role, content) {
            const wrap = document.createElement("div");
            wrap.style.marginBottom = "8px";
            const bubble = document.createElement("div");
            bubble.style.padding = "8px 10px";
            bubble.style.borderRadius = "8px";
            bubble.style.fontSize = "12px";
            bubble.style.lineHeight = "1.35";
            bubble.style.background = role === "assistant" ? "#f1f5f9" : "#4338ca";
            bubble.style.color = role === "assistant" ? "#0f172a" : "#fff";
            bubble.textContent = content;
            wrap.appendChild(bubble);
            messagesEl.appendChild(wrap);
            messagesEl.scrollTop = messagesEl.scrollHeight;
        }

        function getContextualWelcome() {
            const path = (window.location.pathname || "").toLowerCase();
            if (path.includes("/changerequest")) return "Estas en Cambios. Tip: vincula commit/PR antes de baselinar.";
            if (path.includes("/version")) return "Estas en Versiones. Puedes crear y comparar versiones.";
            if (path.includes("/project")) return "Estas en Proyectos. Empieza creando uno nuevo.";
            if (path.includes("/dashboard")) return "Estas en Dashboard. Aqui ves estado general.";
            return "Flujo sugerido: Proyecto -> Elemento -> CR -> Version -> Trazabilidad Git.";
        }

        function buildAnswer(rawText) {
            const text = (rawText || "").toLowerCase();
            if (text.includes("empez") || text.includes("inicio") || text.includes("nuevo")) return "Empieza con: 1) Proyecto, 2) Elemento, 3) CR, 4) Version, 5) commit/PR.";
            if (text.includes("proyecto")) return "En Proyectos crea uno nuevo y luego asocia elementos.";
            if (text.includes("elemento")) return "En Elementos crea uno dentro del proyecto correcto.";
            if (text.includes("cr") || text.includes("cambio")) return "En Cambios crea CR y luego revisa detalle para trazabilidad y aprobaciones.";
            if (text.includes("version")) return "En Versiones crea una version desde el elemento y luego compara versiones.";
            if (text.includes("traza") || text.includes("github") || text.includes("commit") || text.includes("pr")) return "En Detalle de CR vincula owner/repo y commit o PR valido.";
            if (text.includes("baseline") || text.includes("baselin")) return "Para baselinar necesitas evidencia Git verificable.";
            return "Puedo ayudarte con: empezar, proyecto, elemento, CR, versiones, trazabilidad Git y baseline.";
        }

        function computeNextAction(items) {
            if (!items[0].done) return { label: "Crea tu primer proyecto.", path: "/Project/Create" };
            if (!items[1].done) return { label: "Crea un elemento dentro del proyecto.", path: "/Element/Create" };
            if (!items[2].done) return { label: "Crea una solicitud de cambio (CR).", path: "/ChangeRequest/Create" };
            if (!items[3].done) return { label: "Crea una version para tu elemento.", path: "/Element" };
            if (!items[4].done) return { label: "Agrega trazabilidad Git en Detalle de CR.", path: "/ChangeRequest" };
            return { label: "Excelente, flujo base completado. Revisa Dashboard.", path: "/Dashboard" };
        }

        function renderChecklist() {
            checklistEl.innerHTML = "";
            state.checklist.forEach(function (item) {
                const row = document.createElement("div");
                row.style.display = "flex";
                row.style.justifyContent = "space-between";
                row.style.alignItems = "center";
                row.style.marginBottom = "4px";
                row.style.fontSize = "12px";
                const left = document.createElement("span");
                left.textContent = item.label;
                const right = document.createElement("span");
                right.textContent = item.done ? "OK" : "Pendiente";
                right.style.padding = "2px 6px";
                right.style.borderRadius = "999px";
                right.style.fontWeight = "700";
                right.style.background = item.done ? "#dcfce7" : "#fef3c7";
                right.style.color = item.done ? "#166534" : "#92400e";
                row.appendChild(left);
                row.appendChild(right);
                checklistEl.appendChild(row);
            });
        }

        async function refreshProgress() {
            try {
                const response = await fetch("/api/onboarding/progress", { credentials: "same-origin" });
                if (response.ok) {
                    const data = await response.json();
                    state.checklist = [
                        { label: "Proyecto creado", done: (data.projectCount || 0) > 0 },
                        { label: "Elemento creado", done: (data.elementCount || 0) > 0 },
                        { label: "CR creada", done: (data.changeRequestCount || 0) > 0 },
                        { label: "Version creada", done: (data.versionCount || 0) > 0 },
                        { label: "Trazabilidad Git vinculada", done: (data.gitTraceCount || 0) > 0 }
                    ];
                }
            } catch (_e) {
                // ignore and keep UI available
            }
            renderChecklist();
            const next = computeNextAction(state.checklist);
            state.nextPath = next.path;
            nextLabel.textContent = next.label;
            nextBox.style.display = "block";
        }

        function openPanel() {
            panel.style.display = "block";
            openBtn.style.display = "none";
            if (!messagesEl.dataset.initialized) {
                addMessage("assistant", "Hola! Soy tu guia dentro del sistema.");
                addMessage("assistant", getContextualWelcome());
                messagesEl.dataset.initialized = "1";
                refreshProgress();
            }
        }

        function closePanel() {
            panel.style.display = "none";
            openBtn.style.display = "inline-block";
        }

        openBtn.addEventListener("click", openPanel);
        closeBtn.addEventListener("click", closePanel);
        refreshBtn.addEventListener("click", refreshProgress);
        nextGo.addEventListener("click", function () {
            if (state.nextPath) window.location.href = state.nextPath;
        });
        form.addEventListener("submit", function (e) {
            e.preventDefault();
            const q = (input.value || "").trim();
            if (!q) return;
            addMessage("user", q);
            addMessage("assistant", buildAnswer(q));
            input.value = "";
        });
        quickButtons.forEach(function (btn) {
            btn.addEventListener("click", function () {
                const q = btn.getAttribute("data-chatbot-quick") || "";
                addMessage("user", q);
                addMessage("assistant", buildAnswer(q));
            });
        });

        return { openPanel, closePanel };
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", createChatbotController);
    } else {
        createChatbotController();
    }
})();
