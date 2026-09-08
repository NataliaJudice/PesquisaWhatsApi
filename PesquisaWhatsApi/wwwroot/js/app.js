// Utilitários compartilhados: chamadas ao servidor com antiforgery + toasts.
(function () {
    "use strict";

    function tokenAntiforgery() {
        const campo = document.querySelector('input[name="__RequestVerificationToken"]');
        return campo ? campo.value : "";
    }

    /** POST em formato de formulário (o padrão esperado pelos controllers MVC). */
    async function postForm(url, dados) {
        const corpo = new URLSearchParams();
        Object.entries(dados || {}).forEach(([chave, valor]) => {
            if (valor !== undefined && valor !== null) corpo.append(chave, valor);
        });

        const resposta = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "RequestVerificationToken": tokenAntiforgery()
            },
            body: corpo.toString()
        });

        if (!resposta.ok) throw new Error("Falha na requisição (" + resposta.status + ")");
        return resposta.json();
    }

    /** POST com corpo JSON (usado no salvamento em lote de posições). */
    async function postJson(url, dados) {
        const resposta = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": tokenAntiforgery()
            },
            body: JSON.stringify(dados)
        });

        if (!resposta.ok) throw new Error("Falha na requisição (" + resposta.status + ")");
        return resposta.json();
    }

    const ICONES = {
        sucesso: { icone: "check-circle-2", cor: "text-emerald-400", borda: "border-emerald-500/30" },
        erro: { icone: "alert-circle", cor: "text-rose-400", borda: "border-rose-500/30" },
        info: { icone: "info", cor: "text-sky-400", borda: "border-sky-500/30" }
    };

    function toast(texto, tipo) {
        const area = document.getElementById("toasts");
        if (!area) return;

        const estilo = ICONES[tipo] || ICONES.info;
        const item = document.createElement("div");
        item.className =
            "flex items-start gap-3 max-w-sm bg-[#161a22] border " + estilo.borda +
            " rounded-xl shadow-2xl px-4 py-3 text-sm text-gray-200 opacity-0 translate-y-2 transition duration-200";
        item.innerHTML =
            '<i data-lucide="' + estilo.icone + '" class="w-4 h-4 mt-0.5 shrink-0 ' + estilo.cor + '"></i>' +
            '<span class="leading-snug"></span>';
        item.querySelector("span").textContent = texto;

        area.appendChild(item);
        if (window.lucide) window.lucide.createIcons();

        requestAnimationFrame(() => item.classList.remove("opacity-0", "translate-y-2"));

        setTimeout(() => {
            item.classList.add("opacity-0", "translate-y-2");
            setTimeout(() => item.remove(), 220);
        }, 3600);
    }

    /** Confirmação simples antes de ações destrutivas. */
    function confirmarSubmit(evento, mensagem) {
        if (!window.confirm(mensagem)) {
            evento.preventDefault();
            return false;
        }
        return true;
    }

    window.App = { postForm, postJson, toast, confirmarSubmit };

    document.addEventListener("DOMContentLoaded", function () {
        if (window.lucide) window.lucide.createIcons();

        if (window.__toastInicial) {
            toast(window.__toastInicial.texto, window.__toastInicial.tipo);
        }
    });
})();
