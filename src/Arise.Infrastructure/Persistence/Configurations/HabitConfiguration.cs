using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    /// <summary>
    /// Largeur commune aux colonnes d'énumération, comme pour les quêtes. Généreuse à dessein :
    /// ajouter un rythme (« Mensuelle ») ne doit pas coûter une migration d'élargissement.
    /// </summary>
    private const int LongueurEnumeration = 20;

    /// <summary>
    /// Le filtre de l'index unique, en SQL : seules les habitudes actives se disputent un nom.
    /// La colonne est nommée par la convention snake_case du contexte.
    /// </summary>
    private const string FiltreDesHabitudesActives = "NOT is_archived";

    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.HasKey(habit => habit.Id);

        // Une habitude appartient à un Chasseur qui existe : sans cette clé étrangère, une
        // habitude orpheline survivrait à la suppression de son profil. Pas de navigation côté
        // HunterProfile — le profil n'a pas à charger ses habitudes pour calculer sa
        // progression, et une collection y inviterait au chargement en cascade.
        builder.HasOne<HunterProfile>()
            .WithMany()
            .HasForeignKey(habit => habit.HunterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(habit => habit.Name)
            .IsRequired()
            // Le même plafond que l'entité : une colonne plus étroite tronquerait un nom que la
            // commande vient d'accepter.
            .HasMaxLength(Habit.LongueurMaximaleNom)
            // « Courir » et « courir » désignent la même intention : la collation les rend
            // égales à l'index unique comme à la recherche par nom.
            .UseCollation(AriseDbContext.CollationInsensibleALaCasse);

        // Conversion explicite en texte plutôt que l'entier sous-jacent, comme pour le rang du
        // Chasseur et les énumérations de quête : l'ordre des membres peut changer avec les
        // mécaniques de jeu, et un entier stocké figerait cet ordre en base à l'insu du code.
        builder.Property(habit => habit.Frequency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(LongueurEnumeration);

        builder.Property(habit => habit.CreatedAt)
            .IsRequired();

        builder.Property(habit => habit.IsArchived)
            .IsRequired();

        // Le handler vérifie l'unicité par une lecture préalable, ce qui ne tranche pas une
        // course entre deux déclarations simultanées. C'est cet index qui la tranche — la
        // lecture ne sert qu'à formuler un message en français.
        //
        // Filtré sur les seules habitudes actives : ranger « Courir » en janvier doit laisser le
        // Chasseur s'y remettre en mars, sans que la base lui oppose une ligne qu'il ne voit
        // plus nulle part.
        builder.HasIndex(habit => new { habit.HunterProfileId, habit.Name })
            .IsUnique()
            .HasFilter(FiltreDesHabitudesActives);

        // Et un index non filtré sur le seul Chasseur, qui n'est pas redondant avec le
        // précédent : PostgreSQL ne peut servir un index partiel que si la requête porte son
        // prédicat, et « les habitudes du Chasseur » — archivées comprises, c'est le contrat de
        // GetForHunterAsync — ne l'implique pas. Sans lui, lister l'écran Habitudes balaye la
        // table entière, et la cascade à la suppression d'un profil paye le même prix.
        //
        // Déclaré à la main parce que la convention d'EF Core n'a pas créé l'index de clé
        // étrangère qu'elle crée d'ordinaire : elle le juge couvert dès qu'un index commence par
        // les colonnes de la FK, sans regarder si ce dernier est filtré.
        builder.HasIndex(habit => habit.HunterProfileId);
    }
}
