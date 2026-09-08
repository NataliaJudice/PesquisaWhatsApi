namespace PesquisaWhatsApi.Models.ViewModels
{
    /// <summary>Dados que o construtor visual (canvas) consome via JSON.</summary>
    public class FluxoBuilderViewModel
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool Status { get; set; }
        public bool EhPadrao { get; set; }
        public List<NoBuilderViewModel> Nos { get; set; } = new();
    }

    public class NoBuilderViewModel
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public bool EhInicio { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public int? ProximoId { get; set; }
        public List<OpcaoBuilderViewModel> Opcoes { get; set; } = new();

        /// <summary>"menu" quando tem opções, "mensagem" quando é sequencial, "fim" quando não sai de lugar nenhum.</summary>
        public string Tipo => Opcoes.Any() ? "menu" : (ProximoId.HasValue ? "mensagem" : "fim");
    }

    public class OpcaoBuilderViewModel
    {
        public int Id { get; set; }
        public string PalavraChave { get; set; } = string.Empty;
        public int DestinoId { get; set; }
    }
}
