using PesquisaWhatsApi.Appication.DTOs;

namespace PesquisaWhatsApi.Appication.Interface
{
    public interface IWhatsAppService
    {
        WhatsAppOptions Configuracao { get; }

        /// <summary>"Meta" ou "Twilio".</summary>
        string Provedor { get; }

        bool EstaConfigurado { get; }

        /// <summary>Identificação do remetente exibida no painel.</summary>
        string NumeroRemetente { get; }

        /// <summary>Envia uma mensagem ativa (fora do webhook) para um número.</summary>
        Task<(bool Sucesso, string? Erro)> EnviarMensagemAsync(string telefone, string mensagem);

        /// <summary>Confere a assinatura da requisição recebida no webhook.</summary>
        bool AssinaturaValida(AssinaturaWebhook dados);

        /// <summary>Deixa apenas os dígitos do número (remove prefixos como "whatsapp:").</summary>
        string NormalizarTelefone(string telefone);
    }
}
