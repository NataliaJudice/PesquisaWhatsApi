namespace PesquisaWhatsApi.Appication.DTOs
{
    /// <summary>
    /// Resultado de um passo do motor de conversa. Como um passo pode encadear
    /// várias mensagens sequenciais, o retorno é sempre uma lista.
    /// </summary>
    public class RespostaBot
    {
        public bool Sucesso { get; set; } = true;

        /// <summary>Mensagens que o bot deve enviar, na ordem.</summary>
        public List<string> Mensagens { get; set; } = new();

        /// <summary>Opções aceitas no passo em que a conversa parou (quick replies).</summary>
        public List<string> Opcoes { get; set; } = new();

        /// <summary>Nó em que a conversa ficou estacionada.</summary>
        public int? NoAtualId { get; set; }

        public string? Protocolo { get; set; }

        /// <summary>True quando o fluxo chegou ao fim (nó sem saída).</summary>
        public bool Finalizado { get; set; }

        /// <summary>True quando a entrada do usuário não bateu com nenhuma opção.</summary>
        public bool EntradaInvalida { get; set; }

        public static RespostaBot Erro(string mensagem) =>
            new() { Sucesso = false, Mensagens = { mensagem } };
    }
}
