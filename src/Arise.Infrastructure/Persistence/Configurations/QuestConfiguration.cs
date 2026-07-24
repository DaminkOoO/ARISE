using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class QuestConfiguration : IEntityTypeConfiguration<Quest>
{
    /// <summary>
    /// Largeur commune aux colonnes d'énumération. Généreuse à dessein : ajouter un membre
    /// (« Perception », « Calendrier ») ne doit pas coûter une migration d'élargissement.
    /// </summary>
    private const int LongueurEnumeration = 20;

    public void Configure(EntityTypeBuilder<Quest> builder)
    {
        builder.HasKey(quest => quest.Id);

        // Une quête vise un Chasseur qui existe : sans cette clé étrangère, une quête
        // orpheline survivrait à la suppression de son profil et s'afficherait à personne.
        // Pas de navigation côté HunterProfile — le profil n'a pas à charger ses quêtes pour
        // calculer sa progression, et une collection y inviterait au chargement en cascade.
        builder.HasOne<HunterProfile>()
            .WithMany()
            .HasForeignKey(quest => quest.HunterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(quest => quest.QuestDate)
            .IsRequired();

        builder.Property(quest => quest.Title)
            .IsRequired()
            .HasMaxLength(Quest.LongueurMaximaleTitre);

        builder.Property(quest => quest.Description)
            .IsRequired()
            .HasMaxLength(Quest.LongueurMaximaleDescription);

        // Conversion explicite en texte plutôt que l'entier sous-jacent, comme pour le rang du
        // Chasseur : l'ordre des membres peut changer avec les mécaniques de jeu, et un entier
        // stocké figerait cet ordre en base à l'insu du code.
        builder.Property(quest => quest.Domain)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(LongueurEnumeration);

        builder.Property(quest => quest.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(LongueurEnumeration);

        builder.Property(quest => quest.StatTarget)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(LongueurEnumeration);

        builder.Property(quest => quest.Difficulty)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(LongueurEnumeration);

        builder.Property(quest => quest.XpReward)
            .IsRequired();

        builder.Property(quest => quest.IsFallback)
            .IsRequired();

        // Nullable : une quête fraîchement posée n'est pas encore complétée.
        builder.Property(quest => quest.CompletedAt);

        // Déduit de CompletedAt, sans champ de stockage : le mapper créerait une colonne qui
        // pourrait contredire la date de complétion.
        builder.Ignore(quest => quest.IsCompleted);

        // « Une seule génération par jour » tenue en base et pas seulement dans le handler :
        // deux requêtes concurrentes au réveil du Chasseur emprunteraient le même chemin de
        // lecture, verraient toutes deux « aucune quête », et poseraient deux quêtes. L'index
        // est borné au domaine, pour que la quête de Sport n'empêche pas celle d'Habitudes.
        builder.HasIndex(quest => new { quest.HunterProfileId, quest.Domain, quest.QuestDate })
            .IsUnique();
    }
}
