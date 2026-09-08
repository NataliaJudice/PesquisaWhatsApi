// Construtor visual de fluxos: canvas com blocos arrastáveis, ligações e editor lateral.
(function () {
    "use strict";

    const LARGURA_NO = 256;
    const COLUNA = 340;
    const LINHA = 230;

    const estado = {
        fluxo: window.__fluxo,
        selecionadoId: null,
        zoom: 1,
        panX: 0,
        panY: 0,
        ligacao: null // { tipo: "sequencial" | "opcao", origemId, opcaoId }
    };

    const el = {
        canvas: document.getElementById("canvas"),
        mundo: document.getElementById("mundo"),
        svg: document.getElementById("conexoes"),
        nos: document.getElementById("nos"),
        vazio: document.getElementById("canvasVazio"),
        avisoLigacao: document.getElementById("avisoLigacao"),
        editorVazio: document.getElementById("editorVazio"),
        editorConteudo: document.getElementById("editorConteudo"),
        editorTipo: document.getElementById("editorTipo"),
        editorTipoIcone: document.getElementById("editorTipoIcone"),
        campoTitulo: document.getElementById("campoTitulo"),
        campoConteudo: document.getElementById("campoConteudo"),
        campoProximo: document.getElementById("campoProximo"),
        blocoSequencial: document.getElementById("blocoSequencial"),
        listaOpcoes: document.getElementById("listaOpcoes"),
        contadorOpcoes: document.getElementById("contadorOpcoes"),
        novaOpcaoChave: document.getElementById("novaOpcaoChave"),
        novaOpcaoDestino: document.getElementById("novaOpcaoDestino"),
        contadorPassos: document.getElementById("contadorPassos"),
        statusSalvo: document.getElementById("statusSalvo"),
        nomeFluxo: document.getElementById("nomeFluxo"),
        lblZoom: document.getElementById("lblZoom")
    };

    // ------------------------------------------------------------------
    // Comunicação com o servidor
    // ------------------------------------------------------------------

    function marcarSalvando(texto) {
        el.statusSalvo.textContent = texto || "salvando...";
        el.statusSalvo.className = "text-amber-400/80";
    }

    function marcarSalvo() {
        el.statusSalvo.textContent = "tudo salvo";
        el.statusSalvo.className = "text-gray-600";
    }

    async function chamar(acao, dados) {
        marcarSalvando();
        try {
            const resposta = await App.postForm("/Fluxo/" + acao, dados);

            if (!resposta.sucesso) {
                App.toast(resposta.erro || "Não foi possível salvar.", "erro");
                marcarSalvo();
                return null;
            }

            if (resposta.fluxo) {
                estado.fluxo = resposta.fluxo;
                render();
            }

            marcarSalvo();
            return resposta;
        } catch (erro) {
            console.error(erro);
            App.toast("Erro de comunicação com o servidor.", "erro");
            marcarSalvo();
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Renderização do canvas
    // ------------------------------------------------------------------

    function escapar(texto) {
        const div = document.createElement("div");
        div.textContent = texto || "";
        return div.innerHTML;
    }

    function noPorId(id) {
        return estado.fluxo.nos.find(n => n.id === id);
    }

    function resumo(texto, limite) {
        const limpo = (texto || "").trim();
        if (limpo.length <= limite) return limpo;
        return limpo.substring(0, limite) + "…";
    }

    function estiloDoTipo(no) {
        if (no.ehInicio) return { cor: "text-emerald-400", fundo: "bg-emerald-500/10", borda: "border-emerald-500/20", icone: "flag", rotulo: "Início" };
        if (no.opcoes.length) return { cor: "text-amber-400", fundo: "bg-amber-500/10", borda: "border-amber-500/20", icone: "list-tree", rotulo: "Menu" };
        if (no.proximoId) return { cor: "text-sky-400", fundo: "bg-sky-500/10", borda: "border-sky-500/20", icone: "message-square", rotulo: "Mensagem" };
        return { cor: "text-rose-400", fundo: "bg-rose-500/10", borda: "border-rose-500/20", icone: "flag-off", rotulo: "Fim" };
    }

    function htmlDoNo(no) {
        const tipo = estiloDoTipo(no);
        const titulo = no.titulo && no.titulo.trim() ? no.titulo : "Passo #" + no.id;

        let saidas = "";

        if (no.opcoes.length) {
            saidas = no.opcoes.map(opcao => `
                <div class="saida relative flex items-center justify-between gap-2 bg-[#0f1218] border border-white/5 rounded-lg px-2 py-1.5 text-[11px]"
                     data-opcao-id="${opcao.id}" data-destino="${opcao.destinoId}">
                    <span class="truncate text-gray-300">${escapar(opcao.palavraChave)}</span>
                    <button type="button" class="dot w-3 h-3 rounded-full bg-amber-400 hover:ring-2 hover:ring-amber-400/40 shrink-0 translate-x-3.5 transition"
                            data-ligar="opcao" data-opcao-id="${opcao.id}" title="Ligar esta opção a outro bloco"></button>
                </div>`).join("");
        } else if (no.proximoId) {
            saidas = `
                <div class="saida relative flex items-center justify-between gap-2 bg-[#0f1218] border border-white/5 rounded-lg px-2 py-1.5 text-[11px]" data-sequencial="1">
                    <span class="text-gray-500">Segue automaticamente</span>
                    <button type="button" class="dot w-3 h-3 rounded-full bg-sky-400 hover:ring-2 hover:ring-sky-400/40 shrink-0 translate-x-3.5 transition"
                            data-ligar="sequencial" title="Ligar a outro bloco"></button>
                </div>`;
        } else {
            saidas = `
                <div class="saida relative flex items-center justify-between gap-2 bg-rose-500/5 border border-rose-500/10 rounded-lg px-2 py-1.5 text-[11px]" data-sequencial="1">
                    <span class="text-rose-400/80">Conversa termina aqui</span>
                    <button type="button" class="dot w-3 h-3 rounded-full bg-rose-500/60 hover:ring-2 hover:ring-rose-400/40 shrink-0 translate-x-3.5 transition"
                            data-ligar="sequencial" title="Ligar a outro bloco"></button>
                </div>`;
        }

        return `
            <div id="no-${no.id}" class="no absolute w-64 bg-[#151922] border border-white/10 rounded-xl shadow-[0_18px_40px_-20px_rgba(0,0,0,0.9)] overflow-visible select-none"
                 style="left: ${no.posX}px; top: ${no.posY}px;" data-id="${no.id}">
                <div class="cabecalho cursor-grab active:cursor-grabbing flex items-center justify-between gap-2 px-3 py-2 border-b ${tipo.borda} ${tipo.fundo} rounded-t-xl">
                    <div class="flex items-center gap-2 min-w-0 ${tipo.cor}">
                        <i data-lucide="${tipo.icone}" class="w-3.5 h-3.5 shrink-0"></i>
                        <span class="text-[11px] font-semibold truncate">${escapar(titulo)}</span>
                    </div>
                    <span class="text-[10px] ${tipo.cor} opacity-70 shrink-0">${tipo.rotulo}</span>
                </div>
                <div class="p-2.5 space-y-2">
                    <div class="text-[11px] leading-relaxed text-gray-300 bg-[#0f1218] border border-white/5 rounded-lg p-2 whitespace-pre-wrap break-words">${escapar(resumo(no.conteudo, 140)) || '<span class="text-gray-600">(sem texto)</span>'}</div>
                    <div class="space-y-1.5">${saidas}</div>
                </div>
                <span class="entrada absolute left-0 top-8 -translate-x-1.5 w-3 h-3 rounded-full bg-white/20 border-2 border-[#151922]"></span>
            </div>`;
    }

    function render() {
        const html = estado.fluxo.nos.map(htmlDoNo).join("");
        el.nos.innerHTML = html;

        el.vazio.classList.toggle("hidden", estado.fluxo.nos.length > 0);
        el.contadorPassos.textContent = estado.fluxo.nos.length + (estado.fluxo.nos.length === 1 ? " passo" : " passos");

        if (estado.selecionadoId && !noPorId(estado.selecionadoId)) {
            estado.selecionadoId = null;
        }

        if (estado.selecionadoId) {
            const selecionado = document.getElementById("no-" + estado.selecionadoId);
            if (selecionado) selecionado.classList.add("node-ativo");
        }

        if (window.lucide) window.lucide.createIcons();

        aplicarTransformacao();
        // setTimeout em vez de requestAnimationFrame: a aba pode estar em segundo plano na abertura
        setTimeout(desenharConexoes, 0);
        sincronizarEditor();
    }

    function desenharConexoes() {
        // Mantém os <defs> (marcadores de seta) e limpa apenas os caminhos
        el.svg.querySelectorAll("path").forEach(p => {
            if (p.parentNode.tagName !== "marker") p.remove();
        });
        el.svg.querySelectorAll("text").forEach(t => t.remove());

        estado.fluxo.nos.forEach(no => {
            const elemento = document.getElementById("no-" + no.id);
            if (!elemento) return;

            if (no.opcoes.length) {
                elemento.querySelectorAll("div[data-opcao-id]").forEach(linha => {
                    const destinoId = parseInt(linha.getAttribute("data-destino"), 10);
                    ligar(elemento, linha, destinoId, "#fbbf24", "seta-ambar");
                });
            } else if (no.proximoId) {
                const linha = elemento.querySelector("[data-sequencial]");
                ligar(elemento, linha, no.proximoId, "#38bdf8", "seta-azul");
            }
        });
    }

    function ligar(noOrigem, linhaOrigem, destinoId, cor, marcador) {
        const destino = document.getElementById("no-" + destinoId);
        if (!destino || !linhaOrigem) return;

        const x1 = noOrigem.offsetLeft + LARGURA_NO + 6;
        const y1 = noOrigem.offsetTop + linhaOrigem.offsetTop + (linhaOrigem.offsetHeight / 2);
        const x2 = destino.offsetLeft - 8;
        const y2 = destino.offsetTop + 38;

        const curva = Math.max(60, Math.abs(x2 - x1) * 0.45);
        const caminho = document.createElementNS("http://www.w3.org/2000/svg", "path");
        caminho.setAttribute("d", `M ${x1} ${y1} C ${x1 + curva} ${y1}, ${x2 - curva} ${y2}, ${x2} ${y2}`);
        caminho.setAttribute("stroke", cor);
        caminho.setAttribute("stroke-width", "2");
        caminho.setAttribute("fill", "none");
        caminho.setAttribute("opacity", "0.75");
        caminho.setAttribute("marker-end", `url(#${marcador})`);
        el.svg.appendChild(caminho);
    }

    function aplicarTransformacao() {
        el.mundo.style.transform = `translate(${estado.panX}px, ${estado.panY}px) scale(${estado.zoom})`;
        el.lblZoom.textContent = Math.round(estado.zoom * 100) + "%";
    }

    // ------------------------------------------------------------------
    // Editor lateral
    // ------------------------------------------------------------------

    function preencherSelect(select, selecionado, incluirEncerrar, incluirNovo) {
        const opcoes = [];

        if (incluirEncerrar) opcoes.push('<option value="">— Encerrar conversa —</option>');
        if (incluirNovo) opcoes.push('<option value="novo">➕ Criar um bloco novo</option>');

        estado.fluxo.nos
            .filter(n => n.id !== estado.selecionadoId)
            .forEach(n => {
                const rotulo = "#" + n.id + " · " + (n.titulo && n.titulo.trim() ? n.titulo : resumo(n.conteudo, 28));
                opcoes.push(`<option value="${n.id}">${escapar(rotulo)}</option>`);
            });

        select.innerHTML = opcoes.join("");
        select.value = selecionado === null || selecionado === undefined ? "" : String(selecionado);
    }

    function sincronizarEditor() {
        const no = estado.selecionadoId ? noPorId(estado.selecionadoId) : null;

        el.editorVazio.classList.toggle("hidden", !!no);
        el.editorConteudo.classList.toggle("hidden", !no);
        if (!no) return;

        const tipo = estiloDoTipo(no);
        el.editorTipo.textContent = tipo.rotulo === "Início" ? "Bloco de início" : tipo.rotulo;
        el.editorTipoIcone.className = `w-6 h-6 rounded-md ${tipo.fundo} ${tipo.cor} flex items-center justify-center shrink-0`;
        el.editorTipoIcone.innerHTML = `<i data-lucide="${tipo.icone}" class="w-3.5 h-3.5"></i>`;

        if (document.activeElement !== el.campoTitulo) el.campoTitulo.value = no.titulo || "";
        if (document.activeElement !== el.campoConteudo) el.campoConteudo.value = no.conteudo || "";

        // Um bloco com opções não usa o caminho automático
        el.blocoSequencial.classList.toggle("hidden", no.opcoes.length > 0);
        preencherSelect(el.campoProximo, no.proximoId, true, false);

        el.contadorOpcoes.textContent = no.opcoes.length ? no.opcoes.length + " caminho(s)" : "";
        el.listaOpcoes.innerHTML = no.opcoes.map(opcao => `
            <div class="bg-[#161a22] border border-white/5 rounded-xl p-2.5 space-y-2" data-opcao="${opcao.id}">
                <div class="flex items-center gap-2">
                    <input type="text" value="${escapar(opcao.palavraChave)}" maxlength="40" data-campo="chave"
                           class="flex-1 bg-[#0f1218] border border-white/10 rounded-lg px-2.5 py-1.5 text-xs text-gray-200 focus:outline-none focus:border-emerald-500/50" />
                    <button type="button" data-acao="excluir-opcao" class="p-1.5 rounded-md text-gray-600 hover:text-rose-400 hover:bg-white/5 transition" title="Excluir opção">
                        <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
                    </button>
                </div>
                <select data-campo="destino" class="w-full bg-[#0f1218] border border-white/10 rounded-lg px-2.5 py-1.5 text-xs text-gray-200 focus:outline-none focus:border-emerald-500/50"></select>
            </div>`).join("");

        no.opcoes.forEach(opcao => {
            const container = el.listaOpcoes.querySelector(`[data-opcao="${opcao.id}"]`);
            if (container) preencherSelect(container.querySelector('[data-campo="destino"]'), opcao.destinoId, false, false);
        });

        preencherSelect(el.novaOpcaoDestino, "novo", false, true);

        if (window.lucide) window.lucide.createIcons();
    }

    function selecionar(id) {
        document.querySelectorAll(".no").forEach(n => n.classList.remove("node-ativo"));
        estado.selecionadoId = id;

        const elemento = document.getElementById("no-" + id);
        if (elemento) elemento.classList.add("node-ativo");

        sincronizarEditor();
    }

    function fecharEditor() {
        estado.selecionadoId = null;
        document.querySelectorAll(".no").forEach(n => n.classList.remove("node-ativo"));
        sincronizarEditor();
    }

    // ------------------------------------------------------------------
    // Modo ligação (clicar na bolinha e depois no destino)
    // ------------------------------------------------------------------

    function iniciarLigacao(tipo, origemId, opcaoId) {
        estado.ligacao = { tipo, origemId, opcaoId };
        el.avisoLigacao.classList.remove("hidden");
        document.querySelectorAll(".no").forEach(n => {
            if (parseInt(n.getAttribute("data-id"), 10) !== origemId) n.classList.add("node-alvo");
        });
    }

    function cancelarLigacao() {
        estado.ligacao = null;
        el.avisoLigacao.classList.add("hidden");
        document.querySelectorAll(".no").forEach(n => n.classList.remove("node-alvo"));
    }

    async function concluirLigacao(destinoId) {
        const ligacao = estado.ligacao;
        cancelarLigacao();
        if (!ligacao || ligacao.origemId === destinoId) return;

        if (ligacao.tipo === "sequencial") {
            await chamar("ConectarSequencial", { origemId: ligacao.origemId, destinoId });
            App.toast("Blocos ligados.", "sucesso");
        } else {
            const origem = noPorId(ligacao.origemId);
            const opcao = origem ? origem.opcoes.find(o => o.id === ligacao.opcaoId) : null;
            if (!opcao) return;

            await chamar("AtualizarOpcao", { opcaoId: opcao.id, palavraChave: opcao.palavraChave, destinoId });
            App.toast("Caminho da opção atualizado.", "sucesso");
        }
    }

    // ------------------------------------------------------------------
    // Arrastar blocos e mover o canvas
    // ------------------------------------------------------------------

    let arrasto = null;
    let pan = null;

    el.canvas.addEventListener("mousedown", function (evento) {
        const dot = evento.target.closest("[data-ligar]");
        if (dot) return; // tratado no click

        const no = evento.target.closest(".no");

        if (no) {
            const cabecalho = evento.target.closest(".cabecalho");
            const id = parseInt(no.getAttribute("data-id"), 10);

            if (estado.ligacao) return;
            if (!cabecalho) return; // só o cabeçalho arrasta

            evento.preventDefault();
            const dado = noPorId(id);
            arrasto = {
                id,
                elemento: no,
                inicioX: evento.clientX,
                inicioY: evento.clientY,
                origemX: dado.posX,
                origemY: dado.posY,
                moveu: false
            };
            return;
        }

        // Fundo: inicia o pan
        pan = { inicioX: evento.clientX, inicioY: evento.clientY, panX: estado.panX, panY: estado.panY };
        el.canvas.classList.replace("cursor-grab", "cursor-grabbing");
    });

    document.addEventListener("mousemove", function (evento) {
        if (arrasto) {
            const dx = (evento.clientX - arrasto.inicioX) / estado.zoom;
            const dy = (evento.clientY - arrasto.inicioY) / estado.zoom;

            if (Math.abs(dx) > 2 || Math.abs(dy) > 2) arrasto.moveu = true;

            const no = noPorId(arrasto.id);
            no.posX = Math.max(0, arrasto.origemX + dx);
            no.posY = Math.max(0, arrasto.origemY + dy);

            arrasto.elemento.style.left = no.posX + "px";
            arrasto.elemento.style.top = no.posY + "px";
            desenharConexoes();
            return;
        }

        if (pan) {
            estado.panX = pan.panX + (evento.clientX - pan.inicioX);
            estado.panY = pan.panY + (evento.clientY - pan.inicioY);
            aplicarTransformacao();
        }
    });

    document.addEventListener("mouseup", async function () {
        if (arrasto) {
            const { id, moveu } = arrasto;
            arrasto = null;

            if (moveu) {
                const no = noPorId(id);
                marcarSalvando("salvando posição...");
                try {
                    await App.postForm("/Fluxo/SalvarPosicao", { noId: id, posX: Math.round(no.posX), posY: Math.round(no.posY) });
                } catch (erro) {
                    console.error(erro);
                }
                marcarSalvo();
            }
        }

        if (pan) {
            pan = null;
            el.canvas.classList.replace("cursor-grabbing", "cursor-grab");
        }
    });

    // Clique: seleção de bloco, bolinhas de ligação e conclusão da ligação
    el.canvas.addEventListener("click", function (evento) {
        const dot = evento.target.closest("[data-ligar]");
        if (dot) {
            evento.stopPropagation();
            const no = dot.closest(".no");
            const origemId = parseInt(no.getAttribute("data-id"), 10);
            const tipo = dot.getAttribute("data-ligar");
            const opcaoId = dot.getAttribute("data-opcao-id");
            iniciarLigacao(tipo, origemId, opcaoId ? parseInt(opcaoId, 10) : null);
            return;
        }

        const no = evento.target.closest(".no");

        if (estado.ligacao) {
            if (no) concluirLigacao(parseInt(no.getAttribute("data-id"), 10));
            else cancelarLigacao();
            return;
        }

        if (no) selecionar(parseInt(no.getAttribute("data-id"), 10));
    });

    el.canvas.addEventListener("wheel", function (evento) {
        if (!evento.ctrlKey && !evento.metaKey) return;
        evento.preventDefault();
        aplicarZoom(evento.deltaY < 0 ? 0.1 : -0.1);
    }, { passive: false });

    function aplicarZoom(delta) {
        estado.zoom = Math.min(1.6, Math.max(0.4, Math.round((estado.zoom + delta) * 100) / 100));
        aplicarTransformacao();
    }

    function centralizarNoInicio() {
        const inicio = estado.fluxo.nos.find(n => n.ehInicio) || estado.fluxo.nos[0];
        if (!inicio) {
            estado.panX = 0;
            estado.panY = 0;
        } else {
            estado.panX = 120 - inicio.posX * estado.zoom;
            estado.panY = 120 - inicio.posY * estado.zoom;
        }
        aplicarTransformacao();
    }

    document.addEventListener("keydown", function (evento) {
        if (evento.key === "Escape") {
            if (estado.ligacao) cancelarLigacao();
            else fecharEditor();
        }
    });

    // ------------------------------------------------------------------
    // Ações da paleta e do editor
    // ------------------------------------------------------------------

    function posicaoLivre() {
        // Coloca o novo bloco à direita do último, dentro da área visível
        const ocupados = estado.fluxo.nos;
        if (!ocupados.length) return { x: 120, y: 140 };

        const maiorX = Math.max(...ocupados.map(n => n.posX));
        const naColuna = ocupados.filter(n => Math.abs(n.posX - maiorX) < 10);
        const maiorY = Math.max(...naColuna.map(n => n.posY));

        return naColuna.length > 2
            ? { x: maiorX + COLUNA, y: 140 }
            : { x: maiorX, y: maiorY + LINHA };
    }

    document.querySelectorAll("[data-novo]").forEach(botao => {
        botao.addEventListener("click", async function () {
            const tipo = botao.getAttribute("data-novo");
            const posicao = posicaoLivre();

            const conteudo = tipo === "menu"
                ? "Escolha uma opção:\n\n1 - Falar com atendente\n2 - Encerrar"
                : "Digite aqui a mensagem do bot.";
            const titulo = tipo === "menu" ? "Menu" : "Mensagem";

            const resposta = await chamar("CriarNo", {
                fluxoId: estado.fluxo.id,
                conteudo,
                titulo,
                posX: Math.round(posicao.x),
                posY: Math.round(posicao.y)
            });

            if (resposta && resposta.extra && resposta.extra.noId) {
                selecionar(resposta.extra.noId);
                App.toast("Bloco criado. Edite o texto ao lado.", "sucesso");
            }
        });
    });

    document.getElementById("btnFecharEditor").addEventListener("click", fecharEditor);

    async function salvarTexto() {
        if (!estado.selecionadoId) return;

        const no = noPorId(estado.selecionadoId);
        const conteudo = el.campoConteudo.value.trim();
        const titulo = el.campoTitulo.value.trim();

        if (!conteudo) {
            App.toast("A mensagem não pode ficar vazia.", "erro");
            el.campoConteudo.value = no.conteudo;
            return;
        }

        if (conteudo === no.conteudo && titulo === (no.titulo || "")) return;

        await chamar("AtualizarNo", { noId: no.id, conteudo, titulo });
    }

    el.campoConteudo.addEventListener("blur", salvarTexto);
    el.campoTitulo.addEventListener("blur", salvarTexto);
    document.getElementById("btnSalvarTexto").addEventListener("click", async function () {
        await salvarTexto();
        App.toast("Mensagem salva.", "sucesso");
    });

    el.campoProximo.addEventListener("change", async function () {
        if (!estado.selecionadoId) return;
        const valor = el.campoProximo.value;
        await chamar("ConectarSequencial", {
            origemId: estado.selecionadoId,
            destinoId: valor === "" ? "" : valor
        });
    });

    el.listaOpcoes.addEventListener("change", async function (evento) {
        const container = evento.target.closest("[data-opcao]");
        if (!container) return;

        const opcaoId = parseInt(container.getAttribute("data-opcao"), 10);
        const chave = container.querySelector('[data-campo="chave"]').value.trim();
        const destino = container.querySelector('[data-campo="destino"]').value;

        if (!chave) {
            App.toast("O gatilho não pode ficar vazio.", "erro");
            sincronizarEditor();
            return;
        }

        await chamar("AtualizarOpcao", { opcaoId, palavraChave: chave, destinoId: destino });
    });

    el.listaOpcoes.addEventListener("focusout", async function (evento) {
        if (!evento.target.matches('[data-campo="chave"]')) return;

        const container = evento.target.closest("[data-opcao]");
        const opcaoId = parseInt(container.getAttribute("data-opcao"), 10);
        const no = noPorId(estado.selecionadoId);
        const opcao = no ? no.opcoes.find(o => o.id === opcaoId) : null;
        if (!opcao) return;

        const chave = evento.target.value.trim();
        if (!chave || chave === opcao.palavraChave) {
            evento.target.value = opcao.palavraChave;
            return;
        }

        await chamar("AtualizarOpcao", { opcaoId, palavraChave: chave, destinoId: opcao.destinoId });
    });

    el.listaOpcoes.addEventListener("click", async function (evento) {
        const botao = evento.target.closest('[data-acao="excluir-opcao"]');
        if (!botao) return;

        const container = botao.closest("[data-opcao]");
        const opcaoId = parseInt(container.getAttribute("data-opcao"), 10);

        if (!window.confirm("Remover esta opção? O bloco de destino continua existindo.")) return;

        await chamar("ExcluirOpcao", { opcaoId });
        App.toast("Opção removida.", "sucesso");
    });

    document.getElementById("btnAdicionarOpcao").addEventListener("click", async function () {
        if (!estado.selecionadoId) return;

        const chave = el.novaOpcaoChave.value.trim();
        if (!chave) {
            App.toast("Escreva o que o cliente precisa digitar.", "erro");
            el.novaOpcaoChave.focus();
            return;
        }

        const destino = el.novaOpcaoDestino.value;
        const dados = {
            noId: estado.selecionadoId,
            palavraChave: chave,
            conteudoNovoNo: "Resposta para \"" + chave + "\""
        };
        if (destino !== "novo") dados.destinoId = destino;

        const resposta = await chamar("CriarOpcao", dados);
        if (resposta) {
            el.novaOpcaoChave.value = "";
            App.toast("Caminho criado.", "sucesso");
        }
    });

    document.getElementById("btnDefinirInicio").addEventListener("click", async function () {
        if (!estado.selecionadoId) return;
        await chamar("DefinirInicio", { noId: estado.selecionadoId });
        App.toast("Este bloco agora abre a conversa.", "sucesso");
    });

    document.getElementById("btnExcluirNo").addEventListener("click", async function () {
        if (!estado.selecionadoId) return;
        if (!window.confirm("Excluir este bloco? As ligações que chegam nele serão desfeitas.")) return;

        await chamar("ExcluirNo", { noId: estado.selecionadoId });
        fecharEditor();
        App.toast("Bloco excluído.", "sucesso");
    });

    // Renomear o fluxo direto no cabeçalho
    el.nomeFluxo.addEventListener("blur", async function () {
        const nome = el.nomeFluxo.value.trim();
        if (!nome || nome === estado.fluxo.nome) {
            el.nomeFluxo.value = estado.fluxo.nome;
            return;
        }

        marcarSalvando();
        try {
            await App.postForm("/Fluxo/Renomear", { fluxoId: estado.fluxo.id, nome });
            estado.fluxo.nome = nome;
            document.title = nome + " · Construtor";
            App.toast("Fluxo renomeado.", "sucesso");
        } catch (erro) {
            console.error(erro);
        }
        marcarSalvo();
    });

    el.nomeFluxo.addEventListener("keydown", e => { if (e.key === "Enter") el.nomeFluxo.blur(); });

    // ------------------------------------------------------------------
    // Organização automática (layout em camadas a partir do início)
    // ------------------------------------------------------------------

    document.getElementById("btnOrganizar").addEventListener("click", async function () {
        const nos = estado.fluxo.nos;
        if (!nos.length) return;

        const inicio = nos.find(n => n.ehInicio) || nos[0];
        const nivel = new Map([[inicio.id, 0]]);
        const fila = [inicio.id];

        while (fila.length) {
            const atualId = fila.shift();
            const atual = noPorId(atualId);
            if (!atual) continue;

            const destinos = atual.opcoes.map(o => o.destinoId);
            if (atual.proximoId) destinos.push(atual.proximoId);

            destinos.forEach(destinoId => {
                if (!nivel.has(destinoId)) {
                    nivel.set(destinoId, nivel.get(atualId) + 1);
                    fila.push(destinoId);
                }
            });
        }

        // Blocos soltos (sem ninguém apontando) vão para a última coluna
        const maiorNivel = Math.max(0, ...nivel.values());
        nos.forEach(n => { if (!nivel.has(n.id)) nivel.set(n.id, maiorNivel + 1); });

        const porColuna = new Map();
        const posicoes = [];

        nos.slice()
            .sort((a, b) => nivel.get(a.id) - nivel.get(b.id) || a.id - b.id)
            .forEach(n => {
                const coluna = nivel.get(n.id);
                const linha = porColuna.get(coluna) || 0;
                porColuna.set(coluna, linha + 1);

                n.posX = 120 + coluna * COLUNA;
                n.posY = 120 + linha * LINHA;
                posicoes.push({ noId: n.id, posX: n.posX, posY: n.posY });
            });

        render();
        centralizarNoInicio();

        marcarSalvando("organizando...");
        try {
            await App.postJson("/Fluxo/SalvarPosicoes", posicoes);
            App.toast("Mapa organizado.", "sucesso");
        } catch (erro) {
            console.error(erro);
            App.toast("Não consegui salvar as novas posições.", "erro");
        }
        marcarSalvo();
    });

    document.getElementById("btnZoomIn").addEventListener("click", () => aplicarZoom(0.1));
    document.getElementById("btnZoomOut").addEventListener("click", () => aplicarZoom(-0.1));
    document.getElementById("btnCentralizar").addEventListener("click", centralizarNoInicio);

    window.addEventListener("resize", desenharConexoes);

    // ------------------------------------------------------------------

    render();
    centralizarNoInicio();
})();
