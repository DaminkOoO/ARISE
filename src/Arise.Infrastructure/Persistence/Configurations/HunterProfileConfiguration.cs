using Arise.Domain.Hunters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class HunterProfileConfiguration : IEntityTypeConfiguration<HunterProfile>
{
    public void Configure(EntityTypeBuilder<HunterProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Level)
            .IsRequired();

        // Conversion explicite en texte plutôt que l'entier sous-jacent par défaut : la doc de
        // HunterRank prévient que l'ordre des membres peut changer avec les mécaniques de jeu,
        // et un entier stocké figerait cet ordre en base à l'insu du code. Une ligne lue « D »
        // en SQL brut est aussi plus sûre à auditer qu'un « 1 ».
        builder.Property(profile => profile.Rank)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(1);

        builder.Property(profile => profile.CurrentXp)
            .IsRequired();

        builder.Property(profile => profile.XpToNextLevel)
            .IsRequired();

        builder.Property(profile => profile.StreakCurrent)
            .IsRequired();

        builder.Property(profile => profile.StreakLongest)
            .IsRequired();

        // Nullable : un Chasseur fraîchement Éveillé n'a encore rien complété.
        builder.Property(profile => profile.LastCompletionDate);
    }
}
