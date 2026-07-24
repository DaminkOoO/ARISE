using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AjoutDesProfilsDeProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hunter_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    current_xp = table.Column<int>(type: "integer", nullable: false),
                    xp_to_next_level = table.Column<int>(type: "integer", nullable: false),
                    streak_current = table.Column<int>(type: "integer", nullable: false),
                    streak_longest = table.Column<int>(type: "integer", nullable: false),
                    last_completion_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hunter_profiles", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hunter_profiles");
        }
    }
}
