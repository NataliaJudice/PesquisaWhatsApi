namespace PesquisaWhatsApi.Appication.DTOs
{
    /// <summary>
    /// Configuração do canal de WhatsApp (seção "WhatsApp" do appsettings / user-secrets).
    /// Suporta dois provedores: a API oficial da Meta (Cloud API) e a Twilio.
    /// </summary>
    public class WhatsAppOptions
    {
        public const string Secao = "WhatsApp";

        /// <summary>"Meta" (padrão) ou "Twilio".</summary>
        public string Provedor { get; set; } = Provedores.Meta;

        public MetaOptions Meta { get; set; } = new();

        public TwilioOptions Twilio { get; set; } = new();

        /// <summary>Confere a assinatura das requisições recebidas no webhook.</summary>
        public bool ValidarAssinatura { get; set; } = true;

        /// <summary>
        /// URL pública do webhook (ex.: o endereço do túnel). Só é necessária se a
        /// detecção automática pelos cabeçalhos X-Forwarded-* não funcionar.
        /// </summary>
        public string UrlPublicaWebhook { get; set; } = string.Empty;

        public bool UsaMeta => Provedor.Equals(Provedores.Meta, StringComparison.OrdinalIgnoreCase);
    }

    public static class Provedores
    {
        public const string Meta = "Meta";
        public const string Twilio = "Twilio";
    }

    /// <summary>Credenciais da WhatsApp Cloud API (developers.facebook.com).</summary>
    public class MetaOptions
    {
        /// <summary>ID do número de telefone (WhatsApp › Configuração da API).</summary>
        public string PhoneNumberId { get; set; } = string.Empty;

        /// <summary>Token de acesso do app. O temporário dura 24h; o permanente vem de um usuário do sistema.</summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Palavra que você inventa e repete no painel da Meta ao cadastrar o webhook.</summary>
        public string VerifyToken { get; set; } = string.Empty;

        /// <summary>Chave secreta do app — usada para conferir a assinatura X-Hub-Signature-256.</summary>
        public string AppSecret { get; set; } = string.Empty;

        public string VersaoApi { get; set; } = "v21.0";

        /// <summary>Número exibido no painel (apenas informativo).</summary>
        public string NumeroExibicao { get; set; } = string.Empty;

        public bool EstaConfigurado =>
            !string.IsNullOrWhiteSpace(PhoneNumberId) && !string.IsNullOrWhiteSpace(AccessToken);
    }

    /// <summary>Credenciais da Twilio (mantidas para quem preferir esse provedor).</summary>
    public class TwilioOptions
    {
        public string AccountSid { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;

        /// <summary>Número remetente no formato "whatsapp:+14155238886".</summary>
        public string NumeroRemetente { get; set; } = string.Empty;

        public bool EstaConfigurado =>
            !string.IsNullOrWhiteSpace(AccountSid) &&
            !string.IsNullOrWhiteSpace(AuthToken) &&
            !string.IsNullOrWhiteSpace(NumeroRemetente);
    }

    /// <summary>Dados brutos de uma chamada de webhook, usados na conferência da assinatura.</summary>
    public class AssinaturaWebhook
    {
        public string Url { get; set; } = string.Empty;
        public IDictionary<string, string> Parametros { get; set; } = new Dictionary<string, string>();
        public string CorpoBruto { get; set; } = string.Empty;
        public string? Assinatura { get; set; }
    }
}
