using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PesquisaWhatsApi.Migrations
{
    /// <inheritdoc />
    public partial class criarestruturainicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Respostas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Idade = table.Column<int>(type: "integer", nullable: false),
                    FrequenciaTecnologia = table.Column<string>(type: "text", nullable: false),
                    EsquecimentoRemedio = table.Column<string>(type: "text", nullable: false),
                    UsoApp = table.Column<string>(type: "text", nullable: false),
                    PreferenciaAbordagem = table.Column<string>(type: "text", nullable: false),
                    Doenca = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Respostas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_Nome",
                table: "Respostas",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Respostas");
        }
    }
}
