using System.ComponentModel.DataAnnotations;

namespace PesquisaWhatsApi.Models
{
    public class Resposta
    {
        public int Id { get; set; }

        // Gatilho que o usuário digita (Ex: "1", "2", "suporte")
        public string PalavraChave { get; set; } = string.Empty;

        // Origem (A mensagem que exibiu as opções)
        public int FluxoMensagemAtualId { get; set; }
        public FluxoMensagem FluxoMensagemAtual { get; set; } = null!;

        // Destino (Para onde ir se o usuário digitar esta PalavraChave)
        public int ProximaFluxoMensagemId { get; set; }
        public FluxoMensagem ProximaFluxoMensagem { get; set; } = null!;
    }
}
