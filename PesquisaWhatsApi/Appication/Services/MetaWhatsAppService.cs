using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Appication.Interface;

namespace PesquisaWhatsApi.Application.Services
{
    /// <summary>
    /// Camada de saída para a WhatsApp Cloud API (API oficial da Meta).
    /// Diferente da Twilio, aqui a resposta não vai no corpo do webhook:
    /// o webhook responde 200 e as mensagens saem por chamadas ao Graph API.
    /// </summary>
    public class MetaWhatsAppService : IWhatsAppService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MetaWhatsAppService> _logger;

        public MetaWhatsAppService(
            IOptions<WhatsAppOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<MetaWhatsAppService> logger)
        {
            Configuracao = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public WhatsAppOptions Configuracao { get; }

        public string Provedor => Provedores.Meta;

        public bool EstaConfigurado => Configuracao.Meta.EstaConfigurado;

        public string NumeroRemetente => string.IsNullOrWhiteSpace(Configuracao.Meta.NumeroExibicao)
            ? Configuracao.Meta.PhoneNumberId
            : Configuracao.Meta.NumeroExibicao;

        public async Task<(bool Sucesso, string? Erro)> EnviarMensagemAsync(string telefone, string mensagem)
        {
            if (!EstaConfigurado)
            {
                return (false, "WhatsApp não configurado. Preencha WhatsApp:Meta no appsettings/user-secrets.");
            }

            if (string.IsNullOrWhiteSpace(telefone) || string.IsNullOrWhiteSpace(mensagem))
            {
                return (false, "Informe o número de destino e o texto da mensagem.");
            }

            var meta = Configuracao.Meta;
            var url = $"https://graph.facebook.com/{meta.VersaoApi}/{meta.PhoneNumberId}/messages";

            var corpo = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = NormalizarTelefone(telefone),
                type = "text",
                text = new { preview_url = false, body = mensagem }
            };

            try
            {
                var cliente = _httpClientFactory.CreateClient();
                cliente.Timeout = TimeSpan.FromSeconds(20);

                using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json")
                };
                requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", meta.AccessToken);

                var resposta = await cliente.SendAsync(requisicao);
                var conteudo = await resposta.Content.ReadAsStringAsync();

                if (!resposta.IsSuccessStatusCode)
                {
                    _logger.LogError("Meta recusou o envio para {Telefone}: {Status} {Corpo}", telefone, resposta.StatusCode, conteudo);
                    return (false, ExtrairErro(conteudo) ?? $"HTTP {(int)resposta.StatusCode}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar mensagem de WhatsApp para {Telefone}", telefone);
                return (false, ex.Message);
            }
        }

        public bool AssinaturaValida(AssinaturaWebhook dados)
        {
            if (!Configuracao.ValidarAssinatura) return true;

            var segredo = Configuracao.Meta.AppSecret;
            if (string.IsNullOrWhiteSpace(segredo))
            {
                _logger.LogWarning("Validação de assinatura ligada, mas WhatsApp:Meta:AppSecret está vazio.");
                return false;
            }

            var assinatura = dados.Assinatura;
            if (string.IsNullOrWhiteSpace(assinatura)) return false;

            // A Meta envia "sha256=<hex>"
            const string prefixo = "sha256=";
            if (assinatura.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                assinatura = assinatura[prefixo.Length..];
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(segredo));
            var calculado = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(dados.CorpoBruto))).ToLowerInvariant();

            // Comparação em tempo constante evita vazar informação por timing
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(calculado),
                Encoding.UTF8.GetBytes(assinatura.ToLowerInvariant()));
        }

        public string NormalizarTelefone(string telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone)) return string.Empty;

            var semPrefixo = telefone.Replace("whatsapp:", string.Empty, StringComparison.OrdinalIgnoreCase);
            return Regex.Replace(semPrefixo, @"[^\d]", string.Empty);
        }

        private static string? ExtrairErro(string conteudo)
        {
            try
            {
                using var documento = JsonDocument.Parse(conteudo);
                if (documento.RootElement.TryGetProperty("error", out var erro) &&
                    erro.TryGetProperty("message", out var mensagem))
                {
                    return mensagem.GetString();
                }
            }
            catch (JsonException)
            {
                // Resposta sem JSON válido: devolve nulo e o chamador usa o status HTTP
            }

            return null;
        }
    }
}
