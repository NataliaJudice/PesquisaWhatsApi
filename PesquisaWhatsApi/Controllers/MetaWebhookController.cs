using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Models;

namespace PesquisaWhatsApi.Controllers
{
    /// <summary>
    /// Webhook da WhatsApp Cloud API (Meta).
    /// Cadastre esta URL em developers.facebook.com › seu app › WhatsApp › Configuração,
    /// no campo "URL de callback", junto com o token de verificação.
    /// </summary>
    [ApiController]
    [Route("api/whatsapp/meta")]
    public class MetaWebhookController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<MetaWebhookController> _logger;

        public MetaWebhookController(
            IChatbotService chatbotService,
            IWhatsAppService whatsAppService,
            ILogger<MetaWebhookController> logger)
        {
            _chatbotService = chatbotService;
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        /// <summary>
        /// Handshake de verificação: a Meta chama uma vez, ao salvar o webhook,
        /// e espera receber de volta o valor de hub.challenge.
        /// </summary>
        [HttpGet]
        public IActionResult Verificar(
            [FromQuery(Name = "hub.mode")] string? modo,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? desafio)
        {
            // Sem parâmetros: é alguém abrindo no navegador para conferir se a URL responde
            if (string.IsNullOrEmpty(modo) && string.IsNullOrEmpty(token))
            {
                return Ok(new
                {
                    status = "online",
                    provedor = _whatsAppService.Provedor,
                    configurado = _whatsAppService.EstaConfigurado,
                    mensagem = "Webhook pronto. Cadastre esta URL no painel da Meta."
                });
            }

            var esperado = _whatsAppService.Configuracao.Meta.VerifyToken;

            if (modo == "subscribe" && !string.IsNullOrWhiteSpace(esperado) && token == esperado)
            {
                _logger.LogInformation("Webhook da Meta verificado com sucesso.");
                return Content(desafio ?? string.Empty, "text/plain");
            }

            _logger.LogWarning("Verificação do webhook da Meta recusada (token não confere).");
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        [HttpPost]
        public async Task<IActionResult> Receber()
        {
            // O corpo bruto é necessário para conferir a assinatura HMAC
            string corpoBruto;
            using (var leitor = new StreamReader(Request.Body))
            {
                corpoBruto = await leitor.ReadToEndAsync();
            }

            var assinaturaOk = _whatsAppService.AssinaturaValida(new AssinaturaWebhook
            {
                CorpoBruto = corpoBruto,
                Assinatura = Request.Headers["X-Hub-Signature-256"]
            });

            if (!assinaturaOk)
            {
                _logger.LogWarning("Webhook da Meta recusado: assinatura inválida.");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var recebidas = ExtrairMensagens(corpoBruto);

            // Eventos de status (entregue, lido...) chegam sem mensagem: apenas confirmamos
            foreach (var recebida in recebidas)
            {
                _logger.LogInformation("WhatsApp recebido de {Telefone}: {Texto}", recebida.Telefone, recebida.Texto);

                var resposta = await _chatbotService.ProcessarMensagemAsync(
                    recebida.Telefone,
                    recebida.Texto,
                    CanaisConversa.WhatsApp,
                    recebida.Nome);

                foreach (var mensagem in resposta.Mensagens)
                {
                    var (enviou, erro) = await _whatsAppService.EnviarMensagemAsync(recebida.Telefone, mensagem);
                    if (!enviou)
                    {
                        _logger.LogError("Não consegui responder {Telefone}: {Erro}", recebida.Telefone, erro);
                        break;
                    }
                }
            }

            // A Meta reenvia o evento se não receber 200 rapidamente
            return Ok();
        }

        private record MensagemRecebida(string Telefone, string Texto, string? Nome);

        /// <summary>Percorre o payload da Meta e extrai as mensagens de texto/botão recebidas.</summary>
        private List<MensagemRecebida> ExtrairMensagens(string corpoBruto)
        {
            var resultado = new List<MensagemRecebida>();
            if (string.IsNullOrWhiteSpace(corpoBruto)) return resultado;

            try
            {
                using var documento = JsonDocument.Parse(corpoBruto);

                if (!documento.RootElement.TryGetProperty("entry", out var entradas)) return resultado;

                foreach (var entrada in entradas.EnumerateArray())
                {
                    if (!entrada.TryGetProperty("changes", out var mudancas)) continue;

                    foreach (var mudanca in mudancas.EnumerateArray())
                    {
                        if (!mudanca.TryGetProperty("value", out var valor)) continue;
                        if (!valor.TryGetProperty("messages", out var mensagens)) continue;

                        var nome = ExtrairNomeContato(valor);

                        foreach (var mensagem in mensagens.EnumerateArray())
                        {
                            var telefone = mensagem.TryGetProperty("from", out var de) ? de.GetString() : null;
                            if (string.IsNullOrWhiteSpace(telefone)) continue;

                            var texto = ExtrairTexto(mensagem);
                            if (texto == null)
                            {
                                // Áudio, imagem, figurinha...: trata como entrada não reconhecida
                                texto = string.Empty;
                            }

                            resultado.Add(new MensagemRecebida(
                                _whatsAppService.NormalizarTelefone(telefone!),
                                texto,
                                nome));
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Payload da Meta em formato inesperado.");
            }

            return resultado;
        }

        private static string? ExtrairNomeContato(JsonElement valor)
        {
            if (!valor.TryGetProperty("contacts", out var contatos)) return null;

            foreach (var contato in contatos.EnumerateArray())
            {
                if (contato.TryGetProperty("profile", out var perfil) &&
                    perfil.TryGetProperty("name", out var nome))
                {
                    return nome.GetString();
                }
            }

            return null;
        }

        /// <summary>Texto digitado, ou o rótulo do botão/lista quando a resposta veio por clique.</summary>
        private static string? ExtrairTexto(JsonElement mensagem)
        {
            var tipo = mensagem.TryGetProperty("type", out var t) ? t.GetString() : null;

            switch (tipo)
            {
                case "text":
                    return mensagem.TryGetProperty("text", out var texto) &&
                           texto.TryGetProperty("body", out var corpo)
                        ? corpo.GetString()
                        : null;

                case "button":
                    return mensagem.TryGetProperty("button", out var botao) &&
                           botao.TryGetProperty("text", out var rotulo)
                        ? rotulo.GetString()
                        : null;

                case "interactive":
                    if (!mensagem.TryGetProperty("interactive", out var interativo)) return null;

                    if (interativo.TryGetProperty("button_reply", out var respostaBotao))
                    {
                        return respostaBotao.TryGetProperty("title", out var tituloBotao)
                            ? tituloBotao.GetString()
                            : null;
                    }

                    if (interativo.TryGetProperty("list_reply", out var respostaLista))
                    {
                        return respostaLista.TryGetProperty("title", out var tituloLista)
                            ? tituloLista.GetString()
                            : null;
                    }

                    return null;

                default:
                    return null;
            }
        }
    }
}
