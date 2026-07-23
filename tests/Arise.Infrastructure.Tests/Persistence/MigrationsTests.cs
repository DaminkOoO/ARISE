using Arise.Infrastructure;
using Arise.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

public class MigrationsTests
{
    private static AriseDbContext Contexte() =>
        new ServiceCollection()
            .AddInfrastructure("Host=hôte-inutilisé;Database=arise")
            .BuildServiceProvider()
            .GetRequiredService<AriseDbContext>();

    /// <summary>
    /// Le mode de panne visé est silencieux et arrive vite : on ajoute une propriété, les
    /// tests de modèle passent, et personne ne remarque que la migration correspondante
    /// n'existe pas — jusqu'au déploiement, où la colonne manque en base.
    ///
    /// <para>La comparaison se fait entre le modèle courant et l'instantané du dossier
    /// <c>Migrations</c> : aucune base n'est contactée.</para>
    /// </summary>
    [Fact]
    public void Le_modele_n_a_aucun_changement_en_attente_de_migration()
    {
        Contexte().Database.HasPendingModelChanges().Should().BeFalse(
            "il reste à générer une migration : "
            + "dotnet ef migrations add <Nom> -p src/Arise.Infrastructure");
    }

    [Fact]
    public void Expose_au_moins_la_migration_initiale()
    {
        Contexte().Database.GetMigrations().Should().NotBeEmpty();
    }
}
