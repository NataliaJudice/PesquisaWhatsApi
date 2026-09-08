using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Data;
using PesquisaWhatsApi.Models;

namespace PesquisaWhatsApi.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class SimuladorController : Controller
    {
        private readonly IChatbotService _chatbotService;
        private readonly ApplicationDbContext _context;

        public SimuladorController(IChatbotService chatbotService, ApplicationDbContext context)
        {
            _chatbotService = chatbotService;
            _context = context;
        }

        // 1. Tela Inicial do Simulador
        public async Task<IActionResult> Index(int? fluxoId)
        {
            // Carrega os fluxos disponíveis para o usuário escolher qual quer testar
            ViewBag.Fluxos = await _context.Fluxos.Where(f => f.Status).OrderBy(f => f.Nome).ToListAsync();
            ViewBag.FluxoSelecionado = fluxoId
                ?? (await _context.Fluxos.Where(f => f.EhPadrao && f.Status).Select(f => (int?)f.Id).FirstOrDefaultAsync());

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> IniciarSimulacao(string telefone, int fluxoId)
        {
            if (string.IsNullOrWhiteSpace(telefone))
            {
                return Json(new { sucesso = false, mensagens = new[] { "Informe um número de telefone." } });
            }

            var resposta = await _chatbotService.IniciarConversaAsync(
                telefone.Trim(), fluxoId, CanaisConversa.Simulador);

            return Json(new
            {
                sucesso = resposta.Sucesso,
                mensagens = resposta.Mensagens,
                opcoes = resposta.Opcoes,
                noId = resposta.NoAtualId,
                protocolo = resposta.Protocolo,
                finalizado = resposta.Finalizado
            });
        }

        [HttpPost]
        public async Task<IActionResult> EnviarMensagem(string telefone, string texto)
        {
            var resposta = await _chatbotService.ProcessarMensagemAsync(
                (telefone ?? string.Empty).Trim(), texto ?? string.Empty, CanaisConversa.Simulador);

            return Json(new
            {
                sucesso = resposta.Sucesso,
                mensagens = resposta.Mensagens,
                opcoes = resposta.Opcoes,
                noId = resposta.NoAtualId,
                protocolo = resposta.Protocolo,
                finalizado = resposta.Finalizado,
                entradaInvalida = resposta.EntradaInvalida
            });
        }
    }
}
