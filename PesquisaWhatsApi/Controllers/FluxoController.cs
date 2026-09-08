using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Data;
using PesquisaWhatsApi.Models.ViewModels;

namespace PesquisaWhatsApi.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class FluxoController : Controller
    {
        private readonly IChatbotService _chatbotService;
        private readonly ApplicationDbContext _context;

        public FluxoController(IChatbotService chatbotService, ApplicationDbContext context)
        {
            _chatbotService = chatbotService;
            _context = context;
        }

        // 1. Lista todos os fluxos criados
        public async Task<IActionResult> Index()
        {
            var fluxos = await _context.Fluxos
                .OrderByDescending(f => f.EhPadrao)
                .ThenByDescending(f => f.Id)
                .ToListAsync();

            // Contadores usados nos cartões da listagem
            ViewBag.Passos = await _context.FluxoMensagens
                .GroupBy(fm => fm.FluxoId)
                .Select(g => new { FluxoId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.FluxoId, x => x.Total);

            ViewBag.Conversas = await _context.Conversas
                .Where(c => c.FluxoId != null)
                .GroupBy(c => c.FluxoId!.Value)
                .Select(g => new { FluxoId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.FluxoId, x => x.Total);

            return View(fluxos);
        }

        // 2. Criar novo fluxo (Post)
        [HttpPost]
        public async Task<IActionResult> Criar(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return RedirectToAction(nameof(Index));

            var fluxo = await _chatbotService.CriarFluxoAsync(nome.Trim());

            // Todo fluxo novo já nasce com a mensagem de boas-vindas para não abrir um canvas vazio
            await _chatbotService.CriarNoAsync(
                fluxo.Id,
                "Olá! 👋 Seja bem-vindo(a). Digite *1* para começar.",
                120,
                160,
                "Boas-vindas");

            return RedirectToAction(nameof(Detalhes), new { id = fluxo.Id });
        }

        // 3. Tela de Detalhes/Montagem do Fluxo (construtor visual)
        public async Task<IActionResult> Detalhes(int id)
        {
            var fluxo = await _context.Fluxos
                .Include(f => f.FluxoMensagens)
                    .ThenInclude(fm => fm.Mensagem)
                .Include(f => f.FluxoMensagens)
                    .ThenInclude(fm => fm.OpcoesRespostas)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fluxo == null) return NotFound();

            return View(MontarViewModel(fluxo));
        }

        // ---------------------------------------------------------------
        // Endpoints JSON usados pelo construtor visual (wwwroot/js/builder.js)
        // ---------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> CriarNo(int fluxoId, string conteudo, double posX, double posY, string? titulo)
        {
            if (string.IsNullOrWhiteSpace(conteudo)) conteudo = "Nova mensagem";

            var no = await _chatbotService.CriarNoAsync(fluxoId, conteudo, posX, posY, titulo);
            return await EstadoAtualizado(fluxoId, new { noId = no.Id });
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarNo(int noId, string conteudo, string? titulo)
        {
            if (string.IsNullOrWhiteSpace(conteudo))
            {
                return Json(new { sucesso = false, erro = "A mensagem não pode ficar vazia." });
            }

            var ok = await _chatbotService.AtualizarNoAsync(noId, conteudo, titulo);
            return ok ? await EstadoAtualizadoPorNo(noId) : NaoEncontrado();
        }

        [HttpPost]
        public async Task<IActionResult> SalvarPosicao(int noId, double posX, double posY)
        {
            var ok = await _chatbotService.SalvarPosicaoAsync(noId, posX, posY);
            return Json(new { sucesso = ok });
        }

        [HttpPost]
        public async Task<IActionResult> DefinirInicio(int noId)
        {
            var ok = await _chatbotService.DefinirNoInicialAsync(noId);
            return ok ? await EstadoAtualizadoPorNo(noId) : NaoEncontrado();
        }

        [HttpPost]
        public async Task<IActionResult> ConectarSequencial(int origemId, int? destinoId)
        {
            var ok = await _chatbotService.ConectarSequencialAsync(origemId, destinoId);
            if (!ok)
            {
                return Json(new { sucesso = false, erro = "Não foi possível ligar esses dois blocos." });
            }

            return await EstadoAtualizadoPorNo(origemId);
        }

        [HttpPost]
        public async Task<IActionResult> CriarOpcao(int noId, string palavraChave, int? destinoId, string? conteudoNovoNo)
        {
            var opcao = await _chatbotService.CriarOpcaoAsync(noId, palavraChave, destinoId, conteudoNovoNo);
            if (opcao == null)
            {
                return Json(new { sucesso = false, erro = "Gatilho inválido ou já existente neste bloco." });
            }

            return await EstadoAtualizadoPorNo(noId);
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarOpcao(int opcaoId, string palavraChave, int destinoId)
        {
            var noId = await _context.Respostas
                .Where(r => r.Id == opcaoId)
                .Select(r => (int?)r.FluxoMensagemAtualId)
                .FirstOrDefaultAsync();

            var ok = await _chatbotService.AtualizarOpcaoAsync(opcaoId, palavraChave, destinoId);
            if (!ok || noId == null)
            {
                return Json(new { sucesso = false, erro = "Não foi possível salvar a opção." });
            }

            return await EstadoAtualizadoPorNo(noId.Value);
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirOpcao(int opcaoId)
        {
            var noId = await _context.Respostas
                .Where(r => r.Id == opcaoId)
                .Select(r => (int?)r.FluxoMensagemAtualId)
                .FirstOrDefaultAsync();

            var ok = await _chatbotService.ExcluirOpcaoAsync(opcaoId);
            if (!ok || noId == null) return NaoEncontrado();

            return await EstadoAtualizadoPorNo(noId.Value);
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirNo(int noId)
        {
            var fluxoId = await _context.FluxoMensagens
                .Where(fm => fm.Id == noId)
                .Select(fm => (int?)fm.FluxoId)
                .FirstOrDefaultAsync();

            if (fluxoId == null) return NaoEncontrado();

            await _chatbotService.ExcluirNoAsync(noId);
            return await EstadoAtualizado(fluxoId.Value, null);
        }

        /// <summary>Recebe a organização automática do canvas em lote.</summary>
        [HttpPost]
        public async Task<IActionResult> SalvarPosicoes([FromBody] List<PosicaoNo> posicoes)
        {
            if (posicoes == null || !posicoes.Any()) return Json(new { sucesso = true });

            foreach (var posicao in posicoes)
            {
                await _chatbotService.SalvarPosicaoAsync(posicao.NoId, posicao.PosX, posicao.PosY);
            }

            return Json(new { sucesso = true });
        }

        [HttpPost]
        public async Task<IActionResult> Renomear(int fluxoId, string nome)
        {
            var ok = await _chatbotService.RenomearFluxoAsync(fluxoId, nome);
            return Json(new { sucesso = ok, nome = nome?.Trim() });
        }

        [HttpPost]
        public async Task<IActionResult> AlternarStatus(int fluxoId)
        {
            await _chatbotService.AlternarStatusFluxoAsync(fluxoId);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DefinirPadrao(int fluxoId, string? retorno)
        {
            await _chatbotService.DefinirFluxoPadraoAsync(fluxoId);

            if (retorno == "whatsapp") return RedirectToAction("Index", "WhatsApp");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Excluir(int fluxoId)
        {
            await _chatbotService.ExcluirFluxoAsync(fluxoId);
            return RedirectToAction(nameof(Index));
        }

        // ---- Telas antigas mantidas para acesso direto ----

        // 6. Tela para Vincular uma Opção a um Menu
        public async Task<IActionResult> ConfigurarOpcao(int noMenuId)
        {
            var noMenu = await _context.FluxoMensagens
                .Include(fm => fm.Mensagem)
                .FirstOrDefaultAsync(fm => fm.Id == noMenuId);

            if (noMenu == null) return NotFound();

            var possiveisDestinos = await _context.FluxoMensagens
                .Where(fm => fm.FluxoId == noMenu.FluxoId && fm.Id != noMenuId)
                .Select(fm => new
                {
                    fm.Id,
                    Texto = "#" + fm.Id + " · " + fm.Mensagem.Conteudo.Substring(0, Math.Min(fm.Mensagem.Conteudo.Length, 40))
                })
                .ToListAsync();

            ViewBag.Destinos = new SelectList(possiveisDestinos, "Id", "Texto");
            return View(noMenu);
        }

        // 7. Salvar Opção do Menu (Post) — cria o gatilho e o bloco de destino
        [HttpPost]
        public async Task<IActionResult> SalvarOpcao(int noMenuId, string palavraChave, string conteudoResposta, int? destinoId)
        {
            var noMenu = await _context.FluxoMensagens.FindAsync(noMenuId);
            if (noMenu == null) return NotFound();

            await _chatbotService.CriarOpcaoAsync(noMenuId, palavraChave, destinoId, conteudoResposta);
            return RedirectToAction(nameof(Detalhes), new { id = noMenu.FluxoId });
        }

        // 4. Adicionar Mensagem Sequencial (Post)
        [HttpPost]
        public async Task<IActionResult> AdicionarSequencial(int fluxoId, string conteudo, int? noAnteriorId)
        {
            await _chatbotService.AdicionarMensagemSequencialAsync(fluxoId, conteudo, noAnteriorId);
            return RedirectToAction(nameof(Detalhes), new { id = fluxoId });
        }

        // 5. Adicionar Mensagem do tipo Menu (Post)
        [HttpPost]
        public async Task<IActionResult> AdicionarMenu(int fluxoId, string conteudo)
        {
            await _chatbotService.AdicionarMensagemMenuAsync(fluxoId, conteudo);
            return RedirectToAction(nameof(Detalhes), new { id = fluxoId });
        }

        public record PosicaoNo(int NoId, double PosX, double PosY);

        // ---------------------------------------------------------------

        private IActionResult NaoEncontrado() =>
            Json(new { sucesso = false, erro = "Registro não encontrado." });

        private async Task<IActionResult> EstadoAtualizadoPorNo(int noId)
        {
            var fluxoId = await _context.FluxoMensagens
                .Where(fm => fm.Id == noId)
                .Select(fm => (int?)fm.FluxoId)
                .FirstOrDefaultAsync();

            if (fluxoId == null) return NaoEncontrado();

            return await EstadoAtualizado(fluxoId.Value, new { noId });
        }

        /// <summary>Devolve o fluxo inteiro em JSON para o canvas se redesenhar.</summary>
        private async Task<IActionResult> EstadoAtualizado(int fluxoId, object? extra)
        {
            var fluxo = await _context.Fluxos
                .Include(f => f.FluxoMensagens)
                    .ThenInclude(fm => fm.Mensagem)
                .Include(f => f.FluxoMensagens)
                    .ThenInclude(fm => fm.OpcoesRespostas)
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fluxoId);

            if (fluxo == null) return NaoEncontrado();

            return Json(new
            {
                sucesso = true,
                fluxo = MontarViewModel(fluxo),
                extra
            });
        }

        private static FluxoBuilderViewModel MontarViewModel(Models.Fluxos fluxo) => new()
        {
            Id = fluxo.Id,
            Nome = fluxo.Nome,
            Status = fluxo.Status,
            EhPadrao = fluxo.EhPadrao,
            Nos = fluxo.FluxoMensagens
                .OrderBy(fm => fm.Id)
                .Select(fm => new NoBuilderViewModel
                {
                    Id = fm.Id,
                    Titulo = fm.Titulo,
                    Conteudo = fm.Mensagem?.Conteudo ?? string.Empty,
                    EhInicio = fm.EhInicio,
                    PosX = fm.PosX,
                    PosY = fm.PosY,
                    ProximoId = fm.ProximaFluxoMensagemId,
                    Opcoes = fm.OpcoesRespostas
                        .OrderBy(o => o.Id)
                        .Select(o => new OpcaoBuilderViewModel
                        {
                            Id = o.Id,
                            PalavraChave = o.PalavraChave,
                            DestinoId = o.ProximaFluxoMensagemId
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
