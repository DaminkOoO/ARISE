using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arise.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vide à dessein, comme <see cref="JetonDeConcurrenceSurLesQuetes"/> : le jeton s'appuie
    /// sur <c>xmin</c>, colonne système que PostgreSQL tient déjà sur chaque ligne. Il n'y a
    /// rien à créer — le <c>AddColumn</c> échafaudé serait d'ailleurs refusé par le serveur
    /// (« column name "xmin" conflicts with a system column name ») —, seulement un modèle et
    /// un historique de migrations à garder synchronisés.
    /// </summary>
    public partial class JetonDeConcurrenceSurLesProfils : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
