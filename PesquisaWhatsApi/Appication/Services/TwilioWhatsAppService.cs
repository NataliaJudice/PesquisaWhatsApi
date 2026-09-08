using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Appication.Interface;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Security;
using Twilio.Types;

namespace PesquisaWhatsApi.Application.Services
{
    /// <summary>
    /// Camada de saída para o WhatsApp usando a API da Twilio.
    /// O webhook da Twilio responde em TwiML (sem chamada extra); este serviço
    /// cobre os envios ativos, como o teste de conexão do painel.
    /// </summary>
    public class TwilioWhatsAppService : IWhatsAppService
    {
        private readonly ILogger<TwilioWhatsAppService> _logger;

        public TwilioWhatsAppService(IOptions<WhatsAppOptions> options, ILogger<TwilioWhatsAppService> logger)
        {
            Configuracao = options.Value;
            _logger = logger;

            if (EstaConfigurado)
            {
                TwilioClient.Init(Configuracao.Twilio.AccountSid, Configuracao.Twilio.AuthToken);
            }
        }

        public WhatsAppOptions Configuracao { get; }

        public string Provedor => Provedores.Twilio;

        public bool EstaConfigurado => Configuracao.Twilio.EstaConfigurado;

        public string NumeroRemetente => Configuracao.Twilio.NumeroRemetente;

        public async Task<(bool Sucesso, string? Erro)> EnviarMensagemAsync(string telefone, string mensagem)
        {
            if (!EstaConfigurado)
            {
                return (false, "WhatsApp não configurado. Preencha WhatsApp:Twilio no appsettings/user-secrets.");
            }

            if (string.IsNullOrWhiteSpace(telefone) || string.IsNullOrWhiteSpace(mensagem))
            {
                return (false, "Informe o número de destino e o texto da mensagem.");
            }

            try
            {
                await MessageResource.CreateAsync(
                    from: new PhoneNumber(FormatarParaTwilio(Configuracao.Twilio.NumeroRemetente)),
                    to: new PhoneNumber(FormatarParaTwilio(telefone)),
                    body: mensagem);

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

            var token = Configuracao.Twilio.AuthToken;
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(dados.Assinatura)) return false;

            var validador = new RequestValidator(token);
            return validador.Validate(dados.Url, dados.Parametros, dados.Assinatura);
        }

        public string NormalizarTelefone(string telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone)) return string.Empty;

            var semPrefixo = telefone.Replace("whatsapp:", string.Empty, StringComparison.OrdinalIgnoreCase);
            return Regex.Replace(semPrefixo, @"[^\d]", string.Empty);
        }

        private static string FormatarParaTwilio(string telefone)
        {
            var limpo = telefone.Trim();

            if (limpo.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            {
                return limpo;
            }

            var digitos = Regex.Replace(limpo, @"[^\d]", string.Empty);
            return $"whatsapp:+{digitos}";
        }
    }
}
