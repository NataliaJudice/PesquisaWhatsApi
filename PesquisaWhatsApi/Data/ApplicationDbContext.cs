using Microsoft.EntityFrameworkCore;
using PesquisaWhatsApi.Models;

namespace PesquisaWhatsApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Fluxos> Fluxos { get; set; }
        public DbSet<Mensagem> Mensagens { get; set; }
        public DbSet<FluxoMensagem> FluxoMensagens { get; set; }
        public DbSet<Resposta> Respostas { get; set; }
        public DbSet<Conversa> Conversas { get; set; }
        public DbSet<HistoricoMensagem> HistoricoMensagens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configuração do auto-relacionamento sequencial (Caminho Direto)
            modelBuilder.Entity<FluxoMensagem>()
                .HasOne(fm => fm.ProximaFluxoMensagem)
                .WithMany()
                .HasForeignKey(fm => fm.ProximaFluxoMensagemId)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. Configuração das opções de resposta (Menu de Opções)
            modelBuilder.Entity<Resposta>()
                .HasOne(r => r.FluxoMensagemAtual)
                .WithMany(fm => fm.OpcoesRespostas)
                .HasForeignKey(r => r.FluxoMensagemAtualId)
                .OnDelete(DeleteBehavior.Cascade); // Se remover o nó, limpa as opções dele

            modelBuilder.Entity<Resposta>()
                .HasOne(r => r.ProximaFluxoMensagem)
                .WithMany()
                .HasForeignKey(r => r.ProximaFluxoMensagemId)
                .OnDelete(DeleteBehavior.Restrict); // Evita concorrência de cascata

            // 3. Conversa aponta para o fluxo e para o passo atual sem arrastar exclusões em cascata
            modelBuilder.Entity<Conversa>()
                .HasOne(c => c.Fluxo)
                .WithMany()
                .HasForeignKey(c => c.FluxoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Conversa>()
                .HasOne(c => c.FluxoMensagemAtual)
                .WithMany()
                .HasForeignKey(c => c.FluxoMensagemAtualId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Conversa>()
                .HasIndex(c => new { c.TelefoneUsuario, c.Canal });

            // 4. Histórico acompanha o ciclo de vida da conversa
            modelBuilder.Entity<HistoricoMensagem>()
                .HasOne(h => h.Conversa)
                .WithMany(c => c.Historico)
                .HasForeignKey(h => h.ConversaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Só um fluxo pode ser o padrão do WhatsApp por vez
            modelBuilder.Entity<Fluxos>()
                .HasIndex(f => f.EhPadrao)
                .IsUnique()
                .HasFilter("\"EhPadrao\" = true");
        }
    }
}
