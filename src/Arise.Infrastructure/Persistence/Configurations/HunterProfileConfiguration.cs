using Arise.Domain.Hunters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class HunterProfileConfiguration : IEntityTypeConfiguration<HunterProfile>
{
    public void Configure(EntityTypeBuilder<HunterProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        // Jeton de concurrence optimiste sur la colonne système xmin de PostgreSQL, comme pour
        // les quêtes : chaque UPDATE se voit ajouter un « WHERE xmin = <celui lu> ».
        //
        // Sans lui, deux gains d'XP simultanés — la quête de la veille et celle du jour,
        // demain le Sport et les Habitudes — lisent le même total, ajoutent chacun leur montant
        // et écrivent le leur : le second efface le premier, et 40 XP gagnés n'en font que 20.
        // Le chemin d'écriture rejoue son attribution par-dessus l'état gagnant.
        //
        // Propriété fantôme : le Domain n'a pas à porter un numéro de version qui ne veut rien
        // dire pour lui. Un uint marqué IsRowVersion est reconnu par la convention Npgsql, qui
        // le mappe sur xmin — colonne système, donc rien à créer et migration vide.
        builder.Property<uint>("xmin").IsRowVersion();

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
