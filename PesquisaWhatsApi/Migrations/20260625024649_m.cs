using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PesquisaWhatsApi.Migrations
{
    /// <inheritdoc />
    public partial class m : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Respostas_Nome",
                table: "Respostas");

            migrationBuilder.DropColumn(
                name: "Doenca",
                table: "Respostas");

            migrationBuilder.DropColumn(
                name: "EsquecimentoRemedio",
                table: "Respostas");

            migrationBuilder.DropColumn(
                name: "FrequenciaTecnologia",
                table: "Respostas");

            migrationBuilder.DropColumn(
                name: "Nome",
                table: "Respostas");

            migrationBuilder.DropColumn(
                name: "PreferenciaAbordagem",
                table: "Respostas");

            migrationBuilder.RenameColumn(
                name: "UsoApp",
                table: "Respostas",
                newName: "PalavraChave");

            migrationBuilder.RenameColumn(
                name: "Idade",
                table: "Respostas",
                newName: "ProximaFluxoMensagemId");

            migrationBuilder.AddColumn<int>(
                name: "FluxoMensagemAtualId",
                table: "Respostas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Fluxos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fluxos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mensagens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Conteudo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensagens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FluxoMensagens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FluxoId = table.Column<int>(type: "integer", nullable: false),
                    MensagemId = table.Column<int>(type: "integer", nullable: false),
                    ProximaFluxoMensagemId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FluxoMensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FluxoMensagens_FluxoMensagens_ProximaFluxoMensagemId",
                        column: x => x.ProximaFluxoMensagemId,
                        principalTable: "FluxoMensagens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FluxoMensagens_Fluxos_FluxoId",
                        column: x => x.FluxoId,
                        principalTable: "Fluxos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FluxoMensagens_Mensagens_MensagemId",
                        column: x => x.MensagemId,
                        principalTable: "Mensagens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Protocolo = table.Column<string>(type: "text", nullable: false),
                    TelefoneUsuario = table.Column<string>(type: "text", nullable: false),
                    FluxoMensagemAtualId = table.Column<int>(type: "integer", nullable: true),
                    DataUltimaInteracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversas_FluxoMensagens_FluxoMensagemAtualId",
                        column: x => x.FluxoMensagemAtualId,
                        principalTable: "FluxoMensagens",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_FluxoMensagemAtualId",
                table: "Respostas",
                column: "FluxoMensagemAtualId");

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_ProximaFluxoMensagemId",
                table: "Respostas",
                column: "ProximaFluxoMensagemId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversas_FluxoMensagemAtualId",
                table: "Conversas",
                column: "FluxoMensagemAtualId");

            migrationBuilder.CreateIndex(
                name: "IX_FluxoMensagens_FluxoId",
                table: "FluxoMensagens",
                column: "FluxoId");

            migrationBuilder.CreateIndex(
                name: "IX_FluxoMensagens_MensagemId",
                table: "FluxoMensagens",
                column: "MensagemId");

            migrationBuilder.CreateIndex(
                name: "IX_FluxoMensagens_ProximaFluxoMensagemId",
                table: "FluxoMensagens",
                column: "ProximaFluxoMensagemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Respostas_FluxoMensagens_FluxoMensagemAtualId",
                table: "Respostas",
                column: "FluxoMensagemAtualId",
                principalTable: "FluxoMensagens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Respostas_FluxoMensagens_ProximaFluxoMensagemId",
                table: "Respostas",
                column: "ProximaFluxoMensagemId",
                principalTable: "FluxoMensagens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Respostas_FluxoMensagens_FluxoMensagemAtualId",
                table: "Respostas");

            migrationBuilder.DropForeignKey(
                name: "FK_Respostas_FluxoMensagens_ProximaFluxoMensagemId",
                table: "Respostas");

            migrationBuilder.DropTable(
                name: "Conversas");

            migrationBuilder.DropTable(
                name: "FluxoMensagens");

            migrationBuilder.DropTable(
                name: "Fluxos");

            migrationBuilder.DropTable(
                name: "Mensagens");

            migrationBuilder.DropIndex(
                name: "IX_Respostas_FluxoMensagemAtualId",
                table: "Respostas");

            migrationBuilder.DropIndex(
                name: "IX_Respostas_ProximaFluxoMensagemId",
                table: "Respostas");

            migrationBuilder.DropColumn(
                name: "FluxoMensagemAtualId",
                table: "Respostas");

            migrationBuilder.RenameColumn(
                name: "ProximaFluxoMensagemId",
                table: "Respostas",
                newName: "Idade");

            migrationBuilder.RenameColumn(
                name: "PalavraChave",
                table: "Respostas",
                newName: "UsoApp");

            migrationBuilder.AddColumn<string>(
                name: "Doenca",
                table: "Respostas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EsquecimentoRemedio",
                table: "Respostas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FrequenciaTecnologia",
                table: "Respostas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "Respostas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferenciaAbordagem",
                table: "Respostas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_Nome",
                table: "Respostas",
                column: "Nome",
                unique: true);
        }
    }
}
