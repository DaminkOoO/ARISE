using Arise.Domain.Habits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class HabitLogConfiguration : IEntityTypeConfiguration<HabitLog>
{
    public void Configure(EntityTypeBuilder<HabitLog> builder)
    {
        builder.HasKey(log => log.Id);

        // Une entrée appartient à une habitude qui existe, et disparaît avec elle : le journal
        // d'une habitude supprimée ne veut plus rien dire. Ranger une habitude, en revanche, ne
        // supprime rien — c'est tout l'intérêt de l'archivage, qui garde l'histoire.
        //
        // Pas de navigation côté Habit : la série se recalcule depuis une projection de jours,
        // et une collection ici inviterait à charger les entrées entières pour rien.
        builder.HasOne<Habit>()
            .WithMany()
            .HasForeignKey(log => log.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(log => log.Day)
            .IsRequired();

        builder.Property(log => log.LoggedAt)
            .IsRequired();

        // La garde d'idempotence du handler repose sur une lecture préalable, ce qui ne tranche
        // pas deux taps simultanés — deux scopes, deux DbContext, aucun des deux ne voit l'autre.
        // C'est cet index qui la tranche, et le handler traite le refus comme un double-tap.
        //
        // Il sert du même coup la lecture des jours d'une habitude, qui est le chemin de la
        // série : sans lui, chaque affichage balaierait le journal de tous les Chasseurs.
        builder.HasIndex(log => new { log.HabitId, log.Day })
            .IsUnique();
    }
}
