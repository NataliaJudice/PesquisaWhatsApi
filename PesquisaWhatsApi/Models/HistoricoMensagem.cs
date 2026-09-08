namespace PesquisaWhatsApi.Models
{
    /// <summary>
    /// Registro de tudo que entrou e saiu de uma conversa (usado no painel do WhatsApp).
    /// </summary>
    public class HistoricoMensagem
    {
        public int Id { get; set; }

        public int ConversaId { get; set; }
        public Conversa Conversa { get; set; } = null!;

        // "entrada" (usuário -> bot) ou "saida" (bot -> usuário)
        public string Direcao { get; set; } = DirecoesMensagem.Entrada;

        public string Conteudo { get; set; } = string.Empty;

        public DateTime DataEnvio { get; set; } = DateTime.UtcNow;
    }

    public static class DirecoesMensagem
    {
        public const string Entrada = "entrada";
        public const string Saida = "saida";
    }
}
