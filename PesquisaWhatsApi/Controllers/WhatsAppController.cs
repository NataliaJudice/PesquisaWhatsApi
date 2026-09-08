using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Data;
using PesquisaWhatsApi.Models;
using PesquisaWhatsApi.Models.ViewModels;

namespace PesquisaWhatsApi.Controllers
{
    /// <summary>Painel de conexão do chatbot com o WhatsApp.</summary>
    [AutoValidateAntiforgeryToken]
    public class WhatsAppController : Controller
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly ApplicationDbContext _context;

        public WhatsAppController(IWhatsAppService whatsAppService, ApplicationDbContext context)
        {
            _whatsAppService = whatsAppService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var config = _whatsAppService.Configuracao;
            var rotaWebhook = config.UsaMeta ? "/api/whatsapp/meta" : "/api/whatsapp/webhook";

            var baseUrl = string.IsNullOrWhiteSpace(config.UrlPublicaWebhook)
                ? $"{Request.Scheme}://{Request.Host}{rotaWebhook}"
                : config.UrlPublicaWebhook.TrimEnd('/');

            var conversas = await _context.Conversas
                .Include(c => c.Fluxo)
                .Include(c => c.FluxoMensagemAtual)
                    .ThenInclude(fm => fm!.Mensagem)
                .OrderByDescending(c => c.DataUltimaInteracao)
                .Take(25)
                .Select(c => new ConversaResumoViewModel
                {
                    Id = c.Id,
                    Protocolo = c.Protocolo,
                    Telefone = c.TelefoneUsuario,
                    NomeContato = c.NomeContato,
                    Canal = c.Canal,
                    FluxoNome = c.Fluxo != null ? c.Fluxo.Nome : "—",
                    PassoAtual = c.FluxoMensagemAtual != null && c.FluxoMensagemAtual.Mensagem != null
                        ? c.FluxoMensagemAtual.Mensagem.Conteudo
                        : "—",
                    Finalizada = c.Finalizada,
                    TotalMensagens = c.Historico.Count,
                    DataUltimaInteracao = c.DataUltimaInteracao
                })
                .ToListAsync();

            var vm = new PainelWhatsAppViewModel
            {
                Configurado = _whatsAppService.EstaConfigurado,
                Provedor = _whatsAppService.Provedor,
                UsaMeta = config.UsaMeta,
                NumeroRemetente = _whatsAppService.NumeroRemetente,
                ContaMascarada = Mascarar(config.UsaMeta ? config.Meta.PhoneNumberId : config.Twilio.AccountSid),
                VerifyToken = config.Meta.VerifyToken,
                ValidacaoAssinatura = config.ValidarAssinatura,
                UrlWebhook = baseUrl,
                UrlStatus = baseUrl.Replace("/webhook", "/status"),
                EhLocalhost = baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                              || baseUrl.Contains("127.0.0.1"),
                PortaLocal = Request.Host.Port?.ToString() ?? "5277",
                Fluxos = await _context.Fluxos.OrderBy(f => f.Nome).ToListAsync(),
                FluxoPadrao = await _context.Fluxos.FirstOrDefaultAsync(f => f.EhPadrao),
                TotalConversas = await _context.Conversas.CountAsync(c => c.Canal == CanaisConversa.WhatsApp),
                ConversasAtivas = await _context.Conversas.CountAsync(c => c.Canal == CanaisConversa.WhatsApp && !c.Finalizada),
                MensagensTrocadas = await _context.HistoricoMensagens.CountAsync(),
                Conversas = conversas
            };

            return View(vm);
        }

        /// <summary>Envio de teste — confirma que as credenciais e o número remetente funcionam.</summary>
        [HttpPost]
        public async Task<IActionResult> EnviarTeste(string telefone, string mensagem)
        {
            var (sucesso, erro) = await _whatsAppService.EnviarMensagemAsync(
                telefone,
                string.IsNullOrWhiteSpace(mensagem) ? "Teste de conexão do chatbot ✅" : mensagem);

            TempData[sucesso ? "Sucesso" : "Erro"] = sucesso
                ? $"Mensagem enviada para {telefone}."
                : $"Falha no envio: {erro}";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>Histórico completo de uma conversa (usado no painel lateral).</summary>
        [HttpGet]
        public async Task<IActionResult> Historico(int conversaId)
        {
            var conversa = await _context.Conversas
                .Include(c => c.Historico)
                .FirstOrDefaultAsync(c => c.Id == conversaId);

            if (conversa == null) return NotFound();

            return Json(new
            {
                sucesso = true,
                protocolo = conversa.Protocolo,
                telefone = conversa.TelefoneUsuario,
                contato = conversa.NomeContato,
                mensagens = conversa.Historico
                    .OrderBy(h => h.Id)
                    .Select(h => new
                    {
                        direcao = h.Direcao,
                        conteudo = h.Conteudo,
                        data = h.DataEnvio.ToLocalTime().ToString("dd/MM HH:mm")
                    })
            });
        }

        [HttpPost]
        public async Task<IActionResult> EncerrarConversa(int conversaId)
        {
            var conversa = await _context.Conversas.FindAsync(conversaId);
            if (conversa != null)
            {
                conversa.Finalizada = true;
                conversa.FluxoMensagemAtualId = null;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"Conversa {conversa.Protocolo} encerrada.";
            }

            return RedirectToAction(nameof(Index));
        }

        private static string Mascarar(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "—";
            if (valor.Length <= 8) return new string('•', valor.Length);
            return valor[..4] + new string('•', valor.Length - 8) + valor[^4..];
        }
    }
}
