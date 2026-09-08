using Microsoft.AspNetCore.Mvc;
using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Models;
using Twilio.TwiML;

namespace PesquisaWhatsApi.Controllers
{
    /// <summary>
    /// Endpoint público chamado pela Twilio toda vez que alguém manda mensagem
    /// para o número de WhatsApp da conta. Configure esta URL em
    /// Twilio Console > WhatsApp > Sandbox/Sender > "When a message comes in".
    /// </summary>
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<WhatsAppWebhookController> _logger;

        public WhatsAppWebhookController(
            IChatbotService chatbotService,
            IWhatsAppService whatsAppService,
            ILogger<WhatsAppWebhookController> logger)
        {
            _chatbotService = chatbotService;
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        /// <summary>Health check — abra no navegador para conferir se a URL está acessível.</summary>
        [HttpGet("webhook")]
        public IActionResult Status()
        {
            return Ok(new
            {
                status = "online",
                provedor = _whatsAppService.Provedor,
                configurado = _whatsAppService.EstaConfigurado,
                mensagem = "Webhook pronto. Aponte esta URL no console da Twilio (método POST)."
            });
        }

        [HttpPost("webhook")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Receber()
        {
            var form = Request.Form.ToDictionary(f => f.Key, f => f.Value.ToString());

            var assinaturaOk = _whatsAppService.AssinaturaValida(new AssinaturaWebhook
            {
                Url = MontarUrlRequisicao(),
                Parametros = form,
                Assinatura = Request.Headers["X-Twilio-Signature"]
            });

            if (!assinaturaOk)
            {
                _logger.LogWarning("Webhook do WhatsApp recusado: assinatura inválida.");

                // StatusCode em vez de Forbid(): a aplicação não tem esquema de autenticação registrado
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            form.TryGetValue("From", out var de);
            form.TryGetValue("Body", out var corpo);
            form.TryGetValue("ProfileName", out var nomeContato);

            var telefone = _whatsAppService.NormalizarTelefone(de ?? string.Empty);

            if (string.IsNullOrEmpty(telefone))
            {
                _logger.LogWarning("Webhook do WhatsApp sem remetente identificável.");
                return TwiML(new MessagingResponse());
            }

            _logger.LogInformation("WhatsApp recebido de {Telefone}: {Texto}", telefone, corpo);

            var resposta = await _chatbotService.ProcessarMensagemAsync(
                telefone,
                corpo ?? string.Empty,
                CanaisConversa.WhatsApp,
                nomeContato);

            var twiml = new MessagingResponse();
            foreach (var mensagem in resposta.Mensagens)
            {
                twiml.Message(mensagem);
            }

            return TwiML(twiml);
        }

        /// <summary>
        /// Callback opcional de status de entrega (enviado, entregue, lido, falhou).
        /// Configure em "Status callback URL" se quiser acompanhar no log.
        /// </summary>
        [HttpPost("status")]
        public IActionResult StatusEntrega()
        {
            var form = Request.HasFormContentType
                ? Request.Form.ToDictionary(f => f.Key, f => f.Value.ToString())
                : new Dictionary<string, string>();

            form.TryGetValue("MessageStatus", out var status);
            form.TryGetValue("To", out var para);
            _logger.LogInformation("Status de entrega {Status} para {Para}", status, para);

            return Ok();
        }

        private string MontarUrlRequisicao()
        {
            var urlConfigurada = _whatsAppService.Configuracao.UrlPublicaWebhook;

            // Atrás de um túnel (ngrok) o host visto pela aplicação não é o que a Twilio assinou
            if (!string.IsNullOrWhiteSpace(urlConfigurada))
            {
                return urlConfigurada.TrimEnd('/');
            }

            return $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        }

        private ContentResult TwiML(MessagingResponse resposta) =>
            Content(resposta.ToString(), "application/xml", System.Text.Encoding.UTF8);
    }
}
