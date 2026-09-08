namespace PesquisaWhatsApi.Models
{
    public class FluxoMensagem
    {
        public int Id { get; set; }
        public int FluxoId { get; set; }
        public Fluxos Fluxo { get; set; } = null!;

        public int MensagemId { get; set; }
        public Mensagem Mensagem { get; set; } = null!;

        // Nome curto exibido no cabeçalho do bloco dentro do construtor visual
        public string Titulo { get; set; } = string.Empty;

        // Marca o bloco por onde a conversa começa (WhatsApp e Simulador)
        public bool EhInicio { get; set; }

        // Posição do bloco no canvas do construtor (salva ao arrastar)
        public double PosX { get; set; }
        public double PosY { get; set; }

        // SE FOR SEQUENCIAL: Preenchido com o ID do próximo passo automático
        // SE FOR MENU DE OPÇÕES: Fica NULL (as opções em OpcoesRespostas guiarão o fluxo)
        public int? ProximaFluxoMensagemId { get; set; }
        public FluxoMensagem? ProximaFluxoMensagem { get; set; }

        // Opções de Menu vinculadas a este nó (Preenchido apenas se ProximaFluxoMensagemId for nulo)
        public ICollection<Resposta> OpcoesRespostas { get; set; } = new List<Resposta>();
    }
}
