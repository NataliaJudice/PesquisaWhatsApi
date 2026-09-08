using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PesquisaWhatsApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConstrutorVisualEWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversas_FluxoMensagens_FluxoMensagemAtualId",
                table: "Conversas");

            migrationBuilder.AddColumn<bool>(
                name: "EhPadrao",
                table: "Fluxos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EhInicio",
                table: "FluxoMensagens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "PosX",
                table: "FluxoMensagens",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PosY",
                table: "FluxoMensagens",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "FluxoMensagens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Canal",
                table: "Conversas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInicio",
                table: "Conversas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Finalizada",
                table: "Conversas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FluxoId",
                table: "Conversas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeContato",
                table: "Conversas",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistoricoMensagens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversaId = table.Column<int>(type: "integer", nullable: false),
                    Direcao = table.Column<string>(type: "text", nullable: false),
                    Conteudo = table.Column<string>(type: "text", nullable: false),
                    DataEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoMensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricoMensagens_Conversas_ConversaId",
                        column: x => x.ConversaId,
                        principalTable: "Conversas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fluxos_EhPadrao",
                table: "Fluxos",
                column: "EhPadrao",
                unique: true,
                filter: "\"EhPadrao\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Conversas_FluxoId",
                table: "Conversas",
                column: "FluxoId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversas_TelefoneUsuario_Canal",
                table: "Conversas",
                columns: new[] { "TelefoneUsuario", "Canal" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoMensagens_ConversaId",
                table: "HistoricoMensagens",
                column: "ConversaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversas_FluxoMensagens_FluxoMensagemAtualId",
                table: "Conversas",
                column: "FluxoMensagemAtualId",
                principalTable: "FluxoMensagens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversas_Fluxos_FluxoId",
                table: "Conversas",
                column: "FluxoId",
                principalTable: "Fluxos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ----- Ajuste dos dados já existentes -----

            // Conversas antigas vieram todas do simulador
            migrationBuilder.Sql(@"UPDATE ""Conversas"" SET ""Canal"" = 'simulador' WHERE ""Canal"" = '';");
            migrationBuilder.Sql(@"UPDATE ""Conversas"" SET ""DataInicio"" = ""DataUltimaInteracao"" WHERE ""DataInicio"" < '2000-01-01';");

            // Fluxos antigos não tinham bloco de início marcado: promove o passo mais antigo de cada fluxo
            migrationBuilder.Sql(@"
                UPDATE ""FluxoMensagens"" fm
                SET ""EhInicio"" = true
                FROM (
                    SELECT MIN(""Id"") AS ""PrimeiroId""
                    FROM ""FluxoMensagens""
                    GROUP BY ""FluxoId""
                ) primeiros
                WHERE fm.""Id"" = primeiros.""PrimeiroId"";");

            // Distribui em grade os blocos que ainda não têm posição salva no canvas
            migrationBuilder.Sql(@"
                UPDATE ""FluxoMensagens"" fm
                SET ""PosX"" = 120 + ((ordenados.""Ordem"" - 1) % 4) * 340,
                    ""PosY"" = 120 + (((ordenados.""Ordem"" - 1) / 4) * 260)
                FROM (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""FluxoId"" ORDER BY ""Id"") AS ""Ordem""
                    FROM ""FluxoMensagens""
                ) ordenados
                WHERE fm.""Id"" = ordenados.""Id"" AND fm.""PosX"" = 0 AND fm.""PosY"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversas_FluxoMensagens_FluxoMensagemAtualId",
                table: "Conversas");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversas_Fluxos_FluxoId",
                table: "Conversas");

            migrationBuilder.DropTable(
                name: "HistoricoMensagens");

            migrationBuilder.DropIndex(
                name: "IX_Fluxos_EhPadrao",
                table: "Fluxos");

            migrationBuilder.DropIndex(
                name: "IX_Conversas_FluxoId",
                table: "Conversas");

            migrationBuilder.DropIndex(
                name: "IX_Conversas_TelefoneUsuario_Canal",
                table: "Conversas");

            migrationBuilder.DropColumn(
                name: "EhPadrao",
                table: "Fluxos");

            migrationBuilder.DropColumn(
                name: "EhInicio",
                table: "FluxoMensagens");

            migrationBuilder.DropColumn(
                name: "PosX",
                table: "FluxoMensagens");

            migrationBuilder.DropColumn(
                name: "PosY",
                table: "FluxoMensagens");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "FluxoMensagens");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "Conversas");

            migrationBuilder.DropColumn(
                name: "DataInicio",
                table: "Conversas");

            migrationBuilder.DropColumn(
                name: "Finalizada",
                table: "Conversas");

            migrationBuilder.DropColumn(
                name: "FluxoId",
                table: "Conversas");

            migrationBuilder.DropColumn(
                name: "NomeContato",
                table: "Conversas");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversas_FluxoMensagens_FluxoMensagemAtualId",
                table: "Conversas",
                column: "FluxoMensagemAtualId",
                principalTable: "FluxoMensagens",
                principalColumn: "Id");
        }
    }
}
