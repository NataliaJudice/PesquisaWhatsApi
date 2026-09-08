using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Models;

namespace PesquisaWhatsApi.Appication.Interface
{
    public interface IChatbotService
    {
        // ----- Construção do fluxo -----
        Task<Fluxos> CriarFluxoAsync(string nome);
        Task<FluxoMensagem> AdicionarMensagemSequencialAsync(int fluxoId, string conteudo, int? noAnteriorId = null);
        Task<FluxoMensagem> AdicionarMensagemMenuAsync(int fluxoId, string conteudo);
        Task<Resposta> VincularOpcaoAoMenuAsync(int noMenuId, string palavraChave, int noDestinoId);

        // ----- Edição no construtor visual -----
        Task<FluxoMensagem> CriarNoAsync(int fluxoId, string conteudo, double posX, double posY, string? titulo = null);
        Task<bool> AtualizarNoAsync(int noId, string conteudo, string? titulo = null);
        Task<bool> SalvarPosicaoAsync(int noId, double posX, double posY);
        Task<bool> DefinirNoInicialAsync(int noId);
        Task<bool> ConectarSequencialAsync(int origemId, int? destinoId);
        Task<Resposta?> CriarOpcaoAsync(int noMenuId, string palavraChave, int? destinoId, string? conteudoNovoNo);
        Task<bool> AtualizarOpcaoAsync(int opcaoId, string palavraChave, int destinoId);
        Task<bool> ExcluirOpcaoAsync(int opcaoId);
        Task<bool> ExcluirNoAsync(int noId);
        Task<bool> ExcluirFluxoAsync(int fluxoId);
        Task<bool> RenomearFluxoAsync(int fluxoId, string nome);
        Task<bool> AlternarStatusFluxoAsync(int fluxoId);
        Task<bool> DefinirFluxoPadraoAsync(int fluxoId);
        Task<FluxoMensagem?> ObterNoInicialAsync(int fluxoId);

        // ----- Motor de execução (WhatsApp / Simulador) -----
        Task<Conversa> ObterOuCriarConversaAsync(string telefone, int fluxoIdInicial);
        Task<FluxoMensagem?> ProcessarEntradaWhatsAppAsync(string telefone, string textoDigitado);

        /// <summary>Zera a conversa do número e devolve as mensagens de abertura do fluxo.</summary>
        Task<RespostaBot> IniciarConversaAsync(string telefone, int fluxoId, string canal, string? nomeContato = null);

        /// <summary>Processa uma mensagem recebida, avançando o fluxo e devolvendo as respostas.</summary>
        Task<RespostaBot> ProcessarMensagemAsync(string telefone, string texto, string canal, string? nomeContato = null);
    }
}
