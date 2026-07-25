using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arise.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Le jeton de concurrence des quêtes s'appuie sur <c>xmin</c>, la colonne système que
    /// PostgreSQL tient déjà sur chaque ligne de chaque table. Il n'y a donc rien à créer : le
    /// modèle apprend seulement à la lire, et le <c>AddColumn</c> que l'échafaudage propose
    /// serait de surcroît refusé par le serveur (« column name "xmin" conflicts with a system
    /// column name »).
    ///
    /// <para>Cette migration est vide à dessein, et existe pour tenir le modèle et l'historique
    /// des migrations synchronisés — ce que le test anti-oubli vérifie.</para>
    /// </summary>
    public partial class JetonDeConcurrenceSurLesQuetes : Migration
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
