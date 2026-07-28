using Arise.Domain.Hunters;
using Arise.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(task => task.Id);

        // Une tâche appartient à un Chasseur qui existe : sans cette clé étrangère, une tâche
        // orpheline survivrait à la suppression de son profil. Pas de navigation côté
        // HunterProfile — le profil n'a pas à charger ses tâches pour calculer sa progression.
        builder.HasOne<HunterProfile>()
            .WithMany()
            .HasForeignKey(task => task.HunterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(task => task.Title)
            .IsRequired()
            // Le même plafond que l'entité : une colonne plus étroite tronquerait un titre que la
            // commande vient d'accepter.
            .HasMaxLength(TaskItem.LongueurMaximaleTitre);

        // Pas de collation insensible à la casse ici, contrairement au nom d'une habitude : cette
        // collation n'existait que pour rendre l'index unique des habitudes aveugle à la casse.
        // Deux tâches homonymes sont légitimes, il n'y a donc pas d'index unique à servir — et
        // une collation non déterministe interdirait au passage tout LIKE sur la colonne, ce qui
        // coûterait une recherche « commence par » que l'écran des tâches voudra sans doute.

        // Nullable, et c'est le cœur du modèle : « ranger le garage » n'a pas d'échéance, et une
        // colonne non nulle lui en inventerait une — donc un retard que le Chasseur ne s'est
        // jamais donné.
        builder.Property(task => task.DueDate)
            .IsRequired(false);

        builder.Property(task => task.CreatedAt)
            .IsRequired();

        builder.Property(task => task.CompletedAt)
            .IsRequired(false);

        // Lister les tâches d'un Chasseur ne doit pas balayer celles de tous les Chasseurs — à
        // chaque ouverture de l'écran Habitudes & Tâches, et à chaque vérification de la cascade
        // lors de la suppression d'un profil.
        //
        // Non filtré sur les tâches à faire, bien que la requête écarte les faites : le contrat
        // du repository est de rendre tout ce que le Chasseur a déclaré, et un index partiel ne
        // sert que les requêtes qui portent son prédicat.
        builder.HasIndex(task => task.HunterProfileId);
    }
}
