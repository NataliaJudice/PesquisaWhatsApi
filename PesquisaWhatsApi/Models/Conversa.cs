namespace PesquisaWhatsApi.Models
{
    public class Conversa
    {
        public int Id { get; set; }
        public string Protocolo { get; set; } = string.Empty;
        public string TelefoneUsuario { get; set; } = string.Empty;

        // Fluxo que está rodando nesta conversa
        public int? FluxoId { get; set; }
        public Fluxos? Fluxo { get; set; }

        // Guarda em qual nó (passo) do fluxo o usuário está estacionado no WhatsApp
        public int? FluxoMensagemAtualId { get; set; }
        public FluxoMensagem? FluxoMensagemAtual { get; set; }

        // "whatsapp" ou "simulador"
        public string Canal { get; set; } = CanaisConversa.Simulador;

        // Nome exibido no WhatsApp (ProfileName), quando disponível
        public string? NomeContato { get; set; }

        public bool Finalizada { get; set; }

        public DateTime DataInicio { get; set; } = DateTime.UtcNow;
        public DateTime DataUltimaInteracao { get; set; } = DateTime.UtcNow;

        public ICollection<HistoricoMensagem> Historico { get; set; } = new List<HistoricoMensagem>();
    }

    public static class CanaisConversa
    {
        public const string Simulador = "simulador";
        public const string WhatsApp = "whatsapp";
    }
}
