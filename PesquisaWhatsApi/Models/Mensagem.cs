namespace PesquisaWhatsApi.Models
{
    public class Mensagem
    {
        public int Id { get; set; }
        public string Conteudo { get; set; } = string.Empty;

        // Relacionamentos
        public ICollection<FluxoMensagem> FluxoMensagens { get; set; } = new List<FluxoMensagem>();
    }
}
