namespace PesquisaWhatsApi.Models.ViewModels
{
    public class PainelWhatsAppViewModel
    {
        public bool Configurado { get; set; }

        /// <summary>"Meta" ou "Twilio".</summary>
        public string Provedor { get; set; } = string.Empty;
        public bool UsaMeta { get; set; }

        public string NumeroRemetente { get; set; } = string.Empty;

        /// <summary>Identificador da conta já mascarado (Account SID ou Phone Number ID).</summary>
        public string ContaMascarada { get; set; } = string.Empty;

        /// <summary>Token de verificação que a Meta pede ao cadastrar o webhook.</summary>
        public string VerifyToken { get; set; } = string.Empty;

        public bool ValidacaoAssinatura { get; set; }

        public string UrlWebhook { get; set; } = string.Empty;
        public string UrlStatus { get; set; } = string.Empty;

        /// <summary>True quando a URL montada ainda é local — a Twilio não alcança esse endereço.</summary>
        public bool EhLocalhost { get; set; }

        /// <summary>Porta em que a aplicação está respondendo agora (usada no comando do túnel).</summary>
        public string PortaLocal { get; set; } = string.Empty;

        public Fluxos? FluxoPadrao { get; set; }
        public List<Fluxos> Fluxos { get; set; } = new();

        public int TotalConversas { get; set; }
        public int ConversasAtivas { get; set; }
        public int MensagensTrocadas { get; set; }

        public List<ConversaResumoViewModel> Conversas { get; set; } = new();
    }

    public class ConversaResumoViewModel
    {
        public int Id { get; set; }
        public string Protocolo { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string? NomeContato { get; set; }
        public string Canal { get; set; } = string.Empty;
        public string FluxoNome { get; set; } = string.Empty;
        public string PassoAtual { get; set; } = string.Empty;
        public bool Finalizada { get; set; }
        public int TotalMensagens { get; set; }
        public DateTime DataUltimaInteracao { get; set; }
    }
}
