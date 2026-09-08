using Microsoft.EntityFrameworkCore;
using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Data;
using PesquisaWhatsApi.Models;

namespace PesquisaWhatsApi.Application.Services
{
    public class ChatbotService : IChatbotService
    {
        // Evita loop infinito quando o usuário liga um nó sequencial em si mesmo (ou em ciclo)
        private const int LimiteMensagensPorPasso = 10;

        private static readonly string[] ComandosReinicio = { "menu", "reiniciar", "recomecar", "recomeçar", "inicio", "início", "voltar" };
        private static readonly string[] ComandosSaida = { "sair", "encerrar", "finalizar" };

        private readonly ApplicationDbContext _context;

        public ChatbotService(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Métodos de Construção do Fluxo

        public async Task<Fluxos> CriarFluxoAsync(string nome)
        {
            var fluxo = new Fluxos { Nome = nome };
            _context.Fluxos.Add(fluxo);
            await _context.SaveChangesAsync();
            return fluxo;
        }

        // Cria uma mensagem normal e já pode encadeá-la no nó anterior automaticamente
        public async Task<FluxoMensagem> AdicionarMensagemSequencialAsync(int fluxoId, string conteudo, int? noAnteriorId = null)
        {
            var novoNo = await CriarNoAsync(fluxoId, conteudo, 0, 0);

            if (noAnteriorId.HasValue)
            {
                var anterior = await _context.FluxoMensagens.FindAsync(noAnteriorId.Value);
                if (anterior != null)
                {
                    anterior.ProximaFluxoMensagemId = novoNo.Id;
                    await _context.SaveChangesAsync();
                }
            }

            return novoNo;
        }

        // Cria uma mensagem deixando ProximaFluxoMensagemId nulo, esperando a criação das respostas/opções
        public async Task<FluxoMensagem> AdicionarMensagemMenuAsync(int fluxoId, string conteudo)
        {
            return await CriarNoAsync(fluxoId, conteudo, 0, 0);
        }

        public async Task<Resposta> VincularOpcaoAoMenuAsync(int noMenuId, string palavraChave, int noDestinoId)
        {
            var opcao = new Resposta
            {
                FluxoMensagemAtualId = noMenuId,
                PalavraChave = Normalizar(palavraChave),
                ProximaFluxoMensagemId = noDestinoId
            };

            _context.Respostas.Add(opcao);
            await _context.SaveChangesAsync();
            return opcao;
        }

        #endregion

        #region Edição no Construtor Visual

        public async Task<FluxoMensagem> CriarNoAsync(int fluxoId, string conteudo, double posX, double posY, string? titulo = null)
        {
            var mensagem = new Mensagem { Conteudo = conteudo };
            _context.Mensagens.Add(mensagem);
            await _context.SaveChangesAsync();

            // O primeiro bloco do fluxo já nasce marcado como início
            var fluxoVazio = !await _context.FluxoMensagens.AnyAsync(fm => fm.FluxoId == fluxoId);

            var no = new FluxoMensagem
            {
                FluxoId = fluxoId,
                MensagemId = mensagem.Id,
                Titulo = titulo ?? string.Empty,
                PosX = posX,
                PosY = posY,
                EhInicio = fluxoVazio,
                ProximaFluxoMensagemId = null
            };

            _context.FluxoMensagens.Add(no);
            await _context.SaveChangesAsync();

            no.Mensagem = mensagem;
            return no;
        }

        public async Task<bool> AtualizarNoAsync(int noId, string conteudo, string? titulo = null)
        {
            var no = await _context.FluxoMensagens
                .Include(fm => fm.Mensagem)
                .FirstOrDefaultAsync(fm => fm.Id == noId);

            if (no == null) return false;

            no.Mensagem.Conteudo = conteudo;
            if (titulo != null) no.Titulo = titulo;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SalvarPosicaoAsync(int noId, double posX, double posY)
        {
            var no = await _context.FluxoMensagens.FindAsync(noId);
            if (no == null) return false;

            no.PosX = posX;
            no.PosY = posY;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DefinirNoInicialAsync(int noId)
        {
            var no = await _context.FluxoMensagens.FindAsync(noId);
            if (no == null) return false;

            var irmaos = await _context.FluxoMensagens
                .Where(fm => fm.FluxoId == no.FluxoId)
                .ToListAsync();

            foreach (var irmao in irmaos)
            {
                irmao.EhInicio = irmao.Id == noId;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ConectarSequencialAsync(int origemId, int? destinoId)
        {
            var origem = await _context.FluxoMensagens
                .Include(fm => fm.OpcoesRespostas)
                .FirstOrDefaultAsync(fm => fm.Id == origemId);

            if (origem == null) return false;

            if (destinoId.HasValue)
            {
                // Um nó é sequencial OU menu, nunca os dois — e nunca aponta para si mesmo
                if (destinoId.Value == origemId) return false;

                var destino = await _context.FluxoMensagens.FindAsync(destinoId.Value);
                if (destino == null || destino.FluxoId != origem.FluxoId) return false;

                if (origem.OpcoesRespostas.Any())
                {
                    _context.Respostas.RemoveRange(origem.OpcoesRespostas);
                }
            }

            origem.ProximaFluxoMensagemId = destinoId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Resposta?> CriarOpcaoAsync(int noMenuId, string palavraChave, int? destinoId, string? conteudoNovoNo)
        {
            var noMenu = await _context.FluxoMensagens.FindAsync(noMenuId);
            if (noMenu == null || string.IsNullOrWhiteSpace(palavraChave)) return null;

            var chave = Normalizar(palavraChave);

            var jaExiste = await _context.Respostas
                .AnyAsync(r => r.FluxoMensagemAtualId == noMenuId && r.PalavraChave == chave);
            if (jaExiste) return null;

            using var transacao = await _context.Database.BeginTransactionAsync();
            try
            {
                int destinoFinalId;

                if (destinoId.HasValue && destinoId.Value > 0)
                {
                    var destino = await _context.FluxoMensagens.FindAsync(destinoId.Value);
                    if (destino == null || destino.FluxoId != noMenu.FluxoId) return null;
                    destinoFinalId = destino.Id;
                }
                else
                {
                    // Cria o bloco de destino ao lado do menu, para o canvas já nascer organizado
                    var novoNo = await CriarNoAsync(
                        noMenu.FluxoId,
                        string.IsNullOrWhiteSpace(conteudoNovoNo) ? "Nova mensagem" : conteudoNovoNo!,
                        noMenu.PosX + 340,
                        noMenu.PosY + (await ContarOpcoesAsync(noMenuId) * 160));

                    destinoFinalId = novoNo.Id;
                }

                // Ao virar menu, o nó deixa de ser sequencial
                noMenu.ProximaFluxoMensagemId = null;

                var opcao = new Resposta
                {
                    FluxoMensagemAtualId = noMenuId,
                    PalavraChave = chave,
                    ProximaFluxoMensagemId = destinoFinalId
                };

                _context.Respostas.Add(opcao);
                await _context.SaveChangesAsync();
                await transacao.CommitAsync();

                return opcao;
            }
            catch
            {
                await transacao.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> AtualizarOpcaoAsync(int opcaoId, string palavraChave, int destinoId)
        {
            var opcao = await _context.Respostas.FindAsync(opcaoId);
            if (opcao == null || string.IsNullOrWhiteSpace(palavraChave)) return false;

            var destino = await _context.FluxoMensagens.FindAsync(destinoId);
            if (destino == null) return false;

            opcao.PalavraChave = Normalizar(palavraChave);
            opcao.ProximaFluxoMensagemId = destinoId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExcluirOpcaoAsync(int opcaoId)
        {
            var opcao = await _context.Respostas.FindAsync(opcaoId);
            if (opcao == null) return false;

            _context.Respostas.Remove(opcao);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExcluirNoAsync(int noId)
        {
            var no = await _context.FluxoMensagens
                .Include(fm => fm.OpcoesRespostas)
                .FirstOrDefaultAsync(fm => fm.Id == noId);

            if (no == null) return false;

            using var transacao = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Solta quem apontava para este nó (sequenciais, opções e conversas em andamento)
                var sequenciaisApontando = await _context.FluxoMensagens
                    .Where(fm => fm.ProximaFluxoMensagemId == noId)
                    .ToListAsync();
                foreach (var anterior in sequenciaisApontando) anterior.ProximaFluxoMensagemId = null;

                var opcoesApontando = await _context.Respostas
                    .Where(r => r.ProximaFluxoMensagemId == noId)
                    .ToListAsync();
                _context.Respostas.RemoveRange(opcoesApontando);

                var conversasParadas = await _context.Conversas
                    .Where(c => c.FluxoMensagemAtualId == noId)
                    .ToListAsync();
                foreach (var conversa in conversasParadas)
                {
                    conversa.FluxoMensagemAtualId = null;
                    conversa.Finalizada = true;
                }

                // 2. Remove as opções que saem dele e o próprio nó
                _context.Respostas.RemoveRange(no.OpcoesRespostas);
                await _context.SaveChangesAsync();

                var mensagemId = no.MensagemId;
                var eraInicio = no.EhInicio;
                var fluxoId = no.FluxoId;

                _context.FluxoMensagens.Remove(no);
                await _context.SaveChangesAsync();

                // 3. Limpa a mensagem órfã
                var mensagemEmUso = await _context.FluxoMensagens.AnyAsync(fm => fm.MensagemId == mensagemId);
                if (!mensagemEmUso)
                {
                    var mensagem = await _context.Mensagens.FindAsync(mensagemId);
                    if (mensagem != null) _context.Mensagens.Remove(mensagem);
                    await _context.SaveChangesAsync();
                }

                // 4. Se o nó removido era o início, promove outro para o fluxo não ficar sem porta de entrada
                if (eraInicio)
                {
                    var candidato = await _context.FluxoMensagens
                        .Where(fm => fm.FluxoId == fluxoId)
                        .OrderBy(fm => fm.Id)
                        .FirstOrDefaultAsync();

                    if (candidato != null)
                    {
                        candidato.EhInicio = true;
                        await _context.SaveChangesAsync();
                    }
                }

                await transacao.CommitAsync();
                return true;
            }
            catch
            {
                await transacao.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> ExcluirFluxoAsync(int fluxoId)
        {
            var fluxo = await _context.Fluxos
                .Include(f => f.FluxoMensagens)
                    .ThenInclude(fm => fm.OpcoesRespostas)
                .FirstOrDefaultAsync(f => f.Id == fluxoId);

            if (fluxo == null) return false;

            using var transacao = await _context.Database.BeginTransactionAsync();
            try
            {
                var nos = fluxo.FluxoMensagens.ToList();
                var nosIds = nos.Select(n => n.Id).ToList();
                var mensagensIds = nos.Select(n => n.MensagemId).Distinct().ToList();

                var conversas = await _context.Conversas
                    .Where(c => c.FluxoId == fluxoId || (c.FluxoMensagemAtualId != null && nosIds.Contains(c.FluxoMensagemAtualId.Value)))
                    .ToListAsync();
                _context.Conversas.RemoveRange(conversas);

                _context.Respostas.RemoveRange(nos.SelectMany(n => n.OpcoesRespostas));
                await _context.SaveChangesAsync();

                // Solta os vínculos sequenciais antes de apagar (FK Restrict)
                foreach (var no in nos) no.ProximaFluxoMensagemId = null;
                await _context.SaveChangesAsync();

                _context.FluxoMensagens.RemoveRange(nos);
                await _context.SaveChangesAsync();

                var mensagens = await _context.Mensagens
                    .Where(m => mensagensIds.Contains(m.Id))
                    .ToListAsync();
                _context.Mensagens.RemoveRange(mensagens);

                _context.Fluxos.Remove(fluxo);
                await _context.SaveChangesAsync();

                await transacao.CommitAsync();
                return true;
            }
            catch
            {
                await transacao.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> RenomearFluxoAsync(int fluxoId, string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return false;

            var fluxo = await _context.Fluxos.FindAsync(fluxoId);
            if (fluxo == null) return false;

            fluxo.Nome = nome.Trim();
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AlternarStatusFluxoAsync(int fluxoId)
        {
            var fluxo = await _context.Fluxos.FindAsync(fluxoId);
            if (fluxo == null) return false;

            fluxo.Status = !fluxo.Status;

            // Fluxo desativado não pode continuar sendo o padrão do WhatsApp
            if (!fluxo.Status) fluxo.EhPadrao = false;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DefinirFluxoPadraoAsync(int fluxoId)
        {
            var fluxo = await _context.Fluxos.FindAsync(fluxoId);
            if (fluxo == null) return false;

            var todos = await _context.Fluxos.ToListAsync();

            // Limpa o padrão anterior antes de gravar o novo (índice único parcial)
            foreach (var item in todos.Where(f => f.EhPadrao && f.Id != fluxoId))
            {
                item.EhPadrao = false;
            }
            await _context.SaveChangesAsync();

            fluxo.EhPadrao = true;
            fluxo.Status = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FluxoMensagem?> ObterNoInicialAsync(int fluxoId)
        {
            var inicio = await _context.FluxoMensagens
                .Include(fm => fm.Mensagem)
                .FirstOrDefaultAsync(fm => fm.FluxoId == fluxoId && fm.EhInicio);

            if (inicio != null) return inicio;

            // Compatibilidade com fluxos antigos (sem início marcado): pega o mais antigo
            return await _context.FluxoMensagens
                .Include(fm => fm.Mensagem)
                .Where(fm => fm.FluxoId == fluxoId)
                .OrderBy(fm => fm.Id)
                .FirstOrDefaultAsync();
        }

        #endregion

        #region Motor de Execução (Webhooks / WhatsApp)

        public async Task<Conversa> ObterOuCriarConversaAsync(string telefone, int fluxoIdInicial)
        {
            var conversa = await _context.Conversas
                .Include(c => c.FluxoMensagemAtual)
                    .ThenInclude(fm => fm!.Mensagem)
                .FirstOrDefaultAsync(c => c.TelefoneUsuario == telefone);

            if (conversa == null)
            {
                var primeiroNo = await ObterNoInicialAsync(fluxoIdInicial);

                conversa = new Conversa
                {
                    Protocolo = GerarProtocolo(),
                    TelefoneUsuario = telefone,
                    FluxoId = fluxoIdInicial,
                    FluxoMensagemAtualId = primeiroNo?.Id,
                    DataUltimaInteracao = DateTime.UtcNow
                };

                _context.Conversas.Add(conversa);
                await _context.SaveChangesAsync();

                // Força o carregamento do nó inicial para retorno completo
                conversa.FluxoMensagemAtual = primeiroNo;
            }

            return conversa;
        }

        public async Task<FluxoMensagem?> ProcessarEntradaWhatsAppAsync(string telefone, string textoDigitado)
        {
            // 1. Busca a sessão ativa do usuário
            var conversa = await _context.Conversas
                .FirstOrDefaultAsync(c => c.TelefoneUsuario == telefone);

            if (conversa == null || !conversa.FluxoMensagemAtualId.HasValue) return null;

            // 2. Busca os detalhes completos do nó em que o usuário está parado
            var noAtual = await CarregarNoAsync(conversa.FluxoMensagemAtualId.Value);
            if (noAtual == null) return null;

            var proximoNo = ResolverProximoNo(noAtual, textoDigitado);

            // 3. Atualiza o estado da conversa se encontramos um caminho válido
            if (proximoNo != null)
            {
                conversa.FluxoMensagemAtualId = proximoNo.Id;
                conversa.DataUltimaInteracao = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return proximoNo;
        }

        public async Task<RespostaBot> IniciarConversaAsync(string telefone, int fluxoId, string canal, string? nomeContato = null)
        {
            var fluxo = await _context.Fluxos.FindAsync(fluxoId);
            if (fluxo == null) return RespostaBot.Erro("Fluxo não encontrado.");

            var noInicial = await ObterNoInicialAsync(fluxoId);
            if (noInicial == null) return RespostaBot.Erro("Este fluxo ainda não tem nenhuma mensagem configurada.");

            var conversa = await _context.Conversas
                .FirstOrDefaultAsync(c => c.TelefoneUsuario == telefone && c.Canal == canal);

            if (conversa == null)
            {
                conversa = new Conversa
                {
                    Protocolo = GerarProtocolo(),
                    TelefoneUsuario = telefone,
                    Canal = canal
                };
                _context.Conversas.Add(conversa);
            }
            else
            {
                // Reinício: novo protocolo, mesmo histórico de número
                conversa.Protocolo = GerarProtocolo();
                conversa.DataInicio = DateTime.UtcNow;
            }

            conversa.FluxoId = fluxoId;
            conversa.FluxoMensagemAtualId = noInicial.Id;
            conversa.Finalizada = false;
            conversa.DataUltimaInteracao = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(nomeContato)) conversa.NomeContato = nomeContato;

            await _context.SaveChangesAsync();

            var resposta = await PercorrerAPartirDeAsync(conversa, noInicial);
            resposta.Protocolo = conversa.Protocolo;
            return resposta;
        }

        public async Task<RespostaBot> ProcessarMensagemAsync(string telefone, string texto, string canal, string? nomeContato = null)
        {
            texto ??= string.Empty;
            var entrada = Normalizar(texto);

            var conversa = await _context.Conversas
                .FirstOrDefaultAsync(c => c.TelefoneUsuario == telefone && c.Canal == canal);

            // Sem conversa ativa (ou já encerrada): abre o fluxo padrão
            if (conversa == null || conversa.Finalizada || !conversa.FluxoMensagemAtualId.HasValue)
            {
                var fluxoId = conversa?.FluxoId ?? 0;
                var fluxoPadrao = await ObterFluxoPadraoAsync(fluxoId);

                if (fluxoPadrao == null)
                {
                    return RespostaBot.Erro("Nenhum fluxo está publicado no momento. Defina um fluxo padrão no painel do WhatsApp.");
                }

                if (conversa != null)
                {
                    await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Entrada, texto);
                }

                var abertura = await IniciarConversaAsync(telefone, fluxoPadrao.Id, canal, nomeContato);

                // Registra a entrada que disparou a abertura quando a conversa acabou de nascer
                if (conversa == null)
                {
                    var novaConversa = await _context.Conversas
                        .FirstOrDefaultAsync(c => c.TelefoneUsuario == telefone && c.Canal == canal);
                    if (novaConversa != null)
                    {
                        await RegistrarHistoricoAsync(novaConversa, DirecoesMensagem.Entrada, texto, abertura.Mensagens);
                    }
                }
                else
                {
                    await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Saida, string.Empty, abertura.Mensagens);
                }

                return abertura;
            }

            if (!string.IsNullOrWhiteSpace(nomeContato) && conversa.NomeContato != nomeContato)
            {
                conversa.NomeContato = nomeContato;
            }

            await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Entrada, texto);

            // Comandos globais funcionam em qualquer ponto do fluxo
            if (ComandosSaida.Contains(entrada))
            {
                conversa.Finalizada = true;
                conversa.DataUltimaInteracao = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var despedida = new RespostaBot
                {
                    Finalizado = true,
                    Protocolo = conversa.Protocolo,
                    Mensagens = { "Atendimento encerrado. Se precisar, é só mandar uma mensagem que eu começo de novo. 👋" }
                };
                await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Saida, string.Empty, despedida.Mensagens);
                return despedida;
            }

            if (ComandosReinicio.Contains(entrada) && conversa.FluxoId.HasValue)
            {
                var reinicio = await IniciarConversaAsync(telefone, conversa.FluxoId.Value, canal, nomeContato);
                await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Saida, string.Empty, reinicio.Mensagens);
                return reinicio;
            }

            var noAtual = await CarregarNoAsync(conversa.FluxoMensagemAtualId.Value);
            if (noAtual == null)
            {
                return RespostaBot.Erro("Não consegui localizar o passo atual da conversa.");
            }

            var proximoNo = ResolverProximoNo(noAtual, texto);

            if (proximoNo == null)
            {
                var opcoes = noAtual.OpcoesRespostas.Select(o => o.PalavraChave).ToList();
                var invalida = new RespostaBot
                {
                    Sucesso = false,
                    EntradaInvalida = true,
                    NoAtualId = noAtual.Id,
                    Protocolo = conversa.Protocolo,
                    Opcoes = opcoes,
                    Mensagens =
                    {
                        opcoes.Any()
                            ? $"Não entendi 🤔 Responda com uma das opções: {string.Join(", ", opcoes)}."
                            : "Não entendi. Digite *menu* para voltar ao início."
                    }
                };

                await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Saida, string.Empty, invalida.Mensagens);
                return invalida;
            }

            conversa.FluxoMensagemAtualId = proximoNo.Id;
            conversa.DataUltimaInteracao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var resposta = await PercorrerAPartirDeAsync(conversa, proximoNo);
            resposta.Protocolo = conversa.Protocolo;
            await RegistrarHistoricoAsync(conversa, DirecoesMensagem.Saida, string.Empty, resposta.Mensagens);
            return resposta;
        }

        #endregion

        #region Apoio interno

        /// <summary>
        /// Emite a mensagem do nó recebido e continua emitindo enquanto os próximos
        /// passos forem sequenciais, parando em um menu ou no fim do fluxo.
        /// </summary>
        private async Task<RespostaBot> PercorrerAPartirDeAsync(Conversa conversa, FluxoMensagem noInicial)
        {
            var resposta = new RespostaBot();
            var visitados = new HashSet<int>();

            var atual = await CarregarNoAsync(noInicial.Id) ?? noInicial;

            while (atual != null && resposta.Mensagens.Count < LimiteMensagensPorPasso)
            {
                if (!visitados.Add(atual.Id)) break; // ciclo detectado

                if (!string.IsNullOrWhiteSpace(atual.Mensagem?.Conteudo))
                {
                    resposta.Mensagens.Add(atual.Mensagem!.Conteudo);
                }

                resposta.NoAtualId = atual.Id;

                // Menu: para aqui e espera a resposta do usuário
                if (atual.OpcoesRespostas.Any())
                {
                    resposta.Opcoes = atual.OpcoesRespostas
                        .OrderBy(o => o.Id)
                        .Select(o => o.PalavraChave)
                        .ToList();
                    break;
                }

                // Fim do fluxo
                if (!atual.ProximaFluxoMensagemId.HasValue)
                {
                    resposta.Finalizado = true;
                    break;
                }

                atual = await CarregarNoAsync(atual.ProximaFluxoMensagemId.Value);
            }

            if (resposta.NoAtualId.HasValue && conversa.FluxoMensagemAtualId != resposta.NoAtualId)
            {
                conversa.FluxoMensagemAtualId = resposta.NoAtualId;
            }

            if (resposta.Finalizado)
            {
                conversa.Finalizada = true;
            }

            conversa.DataUltimaInteracao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!resposta.Mensagens.Any())
            {
                resposta.Mensagens.Add("Este passo do fluxo está sem texto configurado.");
            }

            return resposta;
        }

        private async Task<FluxoMensagem?> CarregarNoAsync(int noId)
        {
            return await _context.FluxoMensagens
                .Include(fm => fm.Mensagem)
                .Include(fm => fm.OpcoesRespostas)
                .FirstOrDefaultAsync(fm => fm.Id == noId);
        }

        private FluxoMensagem? ResolverProximoNo(FluxoMensagem noAtual, string textoDigitado)
        {
            // REGRA 1: É uma mensagem comum (Sequencial)?
            if (noAtual.ProximaFluxoMensagemId.HasValue && !noAtual.OpcoesRespostas.Any())
            {
                return _context.FluxoMensagens
                    .Include(fm => fm.Mensagem)
                    .FirstOrDefault(fm => fm.Id == noAtual.ProximaFluxoMensagemId.Value);
            }

            // REGRA 2: É uma mensagem de Menu? Avalia o que o usuário respondeu.
            var busca = Normalizar(textoDigitado);
            var opcao = noAtual.OpcoesRespostas.FirstOrDefault(r => r.PalavraChave == busca);

            // Aceita também respostas por trecho ("quero suporte" casa com "suporte")
            opcao ??= noAtual.OpcoesRespostas
                .FirstOrDefault(r => r.PalavraChave.Length > 2 && busca.Contains(r.PalavraChave));

            if (opcao == null) return null;

            return _context.FluxoMensagens
                .Include(fm => fm.Mensagem)
                .FirstOrDefault(fm => fm.Id == opcao.ProximaFluxoMensagemId);
        }

        private async Task<Fluxos?> ObterFluxoPadraoAsync(int fluxoPreferidoId)
        {
            if (fluxoPreferidoId > 0)
            {
                var preferido = await _context.Fluxos
                    .FirstOrDefaultAsync(f => f.Id == fluxoPreferidoId && f.Status);
                if (preferido != null) return preferido;
            }

            return await _context.Fluxos.FirstOrDefaultAsync(f => f.EhPadrao && f.Status)
                ?? await _context.Fluxos.Where(f => f.Status).OrderBy(f => f.Id).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Grava no histórico a mensagem avulsa (<paramref name="conteudo"/>, na direção informada)
        /// e as mensagens que o bot devolveu (<paramref name="respostasDoBot"/>, sempre como saída).
        /// </summary>
        private async Task RegistrarHistoricoAsync(Conversa conversa, string direcao, string conteudo, IEnumerable<string>? respostasDoBot = null)
        {
            if (conversa.Id == 0) return;

            var itens = new List<HistoricoMensagem>();

            if (!string.IsNullOrWhiteSpace(conteudo))
            {
                itens.Add(new HistoricoMensagem { ConversaId = conversa.Id, Direcao = direcao, Conteudo = conteudo });
            }

            if (respostasDoBot != null)
            {
                itens.AddRange(respostasDoBot
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => new HistoricoMensagem
                    {
                        ConversaId = conversa.Id,
                        Direcao = DirecoesMensagem.Saida,
                        Conteudo = m
                    }));
            }

            if (!itens.Any()) return;

            _context.HistoricoMensagens.AddRange(itens);
            await _context.SaveChangesAsync();
        }

        private static string GerarProtocolo() =>
            DateTime.UtcNow.ToString("yyMMdd") + Random.Shared.Next(1000, 9999);

        private static string Normalizar(string? texto) =>
            (texto ?? string.Empty).Trim().ToLowerInvariant();

        private async Task<int> ContarOpcoesAsync(int noMenuId) =>
            await _context.Respostas.CountAsync(r => r.FluxoMensagemAtualId == noMenuId);

        #endregion
    }
}
