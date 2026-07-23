using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreationDesChasseurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:insensible_a_la_casse", "und-u-ks-level2,und-u-ks-level2,icu,False");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, collation: "insensible_a_la_casse"),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");

            // Ajouté à la main : le diff de modèle ne génère pas la contrepartie du
            // CREATE COLLATION ci-dessus, et sans elle un Down suivi d'un Up échoue sur une
            // collation déjà existante. OldAnnotation sans Annotation, c'est la façon dont
            // EF représente « cette collation disparaît » — d'où le DROP COLLATION.
            migrationBuilder.AlterDatabase()
                .OldAnnotation(
                    "Npgsql:CollationDefinition:insensible_a_la_casse",
                    "und-u-ks-level2,und-u-ks-level2,icu,False");
        }
    }
}
