namespace PesquisaWhatsApi.Models
{
    public class Fluxos
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool Status { get; set; } = true;
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        // Fluxo disparado quando um número novo manda mensagem no WhatsApp
        public bool EhPadrao { get; set; }

        // Relacionamentos
        public ICollection<FluxoMensagem> FluxoMensagens { get; set; } = new List<FluxoMensagem>();
    }
}
