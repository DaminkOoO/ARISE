using Arise.Domain.Users;
using Arise.Infrastructure;
using Arise.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve la <b>forme du modèle</b>, telle que la produit le câblage réel
/// (<c>AddInfrastructure</c>) — pas une configuration montée pour l'occasion, qui ne dirait
/// rien de ce que l'API utilisera.
///
/// <para>Aucun de ces tests n'ouvre de connexion : EF construit le modèle et sait en tirer
/// le DDL sans base. C'est délibéré — ces tests tournent en CI sans Docker. Ce que le
/// serveur PostgreSQL en fait réellement (unicité rejetée, casse ignorée) relève des tests
/// Testcontainers du repository, pas d'ici.</para>
/// </summary>
public class AriseDbContextTests
{
    private static AriseDbContext Contexte()
    {
        var services = new ServiceCollection()
            // Jamais contactée : construire le modèle et en dériver le DDL ne demande pas
            // de connexion.
            .AddInfrastructure("Host=hôte-inutilisé;Database=arise")
            .BuildServiceProvider();

        return services.GetRequiredService<AriseDbContext>();
    }

    /// <summary>
    /// Le modèle de conception, et non <c>DbContext.Model</c> : ce dernier est optimisé pour
    /// l'exécution et écarte ce dont les requêtes n'ont pas besoin — la collation, entre
    /// autres, qui ne sert qu'à écrire le schéma.
    /// </summary>
    private static IModel Modele() =>
        Contexte().GetService<IDesignTimeModel>().Model;

    private static IEntityType Chasseurs() =>
        Modele().FindEntityType(typeof(User))
        ?? throw new InvalidOperationException("Le modèle ne connaît pas l'entité User.");

    private static IProperty Propriete(string nom) =>
        Chasseurs().FindProperty(nom)
        ?? throw new InvalidOperationException($"Le modèle ne connaît pas la propriété {nom}.");

    [Fact]
    public void Expose_les_Chasseurs_dans_le_modele()
    {
        Modele().FindEntityType(typeof(User)).Should().NotBeNull();
    }

    [Fact]
    public void Nomme_la_table_des_Chasseurs_en_snake_case()
    {
        Chasseurs().GetTableName().Should().Be("users");
    }

    // Username seul ne prouverait rien : il s'écrit pareil dans les deux conventions.
    [Theory]
    [InlineData(nameof(User.PasswordHash), "password_hash")]
    [InlineData(nameof(User.RegisteredAt), "registered_at")]
    public void Nomme_les_colonnes_en_snake_case(string propriete, string colonne)
    {
        Propriete(propriete).GetColumnName().Should().Be(colonne);
    }

    [Fact]
    public void Prend_l_identifiant_du_Chasseur_pour_cle_primaire()
    {
        Chasseurs().FindPrimaryKey()!.Properties.Should().ContainSingle()
            .Which.Name.Should().Be(nameof(User.Id));
    }

    // Le handler d'inscription vérifie l'unicité par une lecture, ce qui ne tranche pas une
    // course entre deux inscriptions simultanées : c'est cet index qui la tranche.
    [Fact]
    public void Rend_le_nom_du_Chasseur_unique()
    {
        Chasseurs().GetIndexes()
            .Should().ContainSingle(index =>
                index.Properties.Count == 1
                && index.Properties[0].Name == nameof(User.Username))
            .Which.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Borne_la_longueur_du_nom_du_Chasseur_comme_le_validator()
    {
        Propriete(nameof(User.Username)).GetMaxLength()
            .Should().Be(User.LongueurMaximaleNom);
    }

    [Theory]
    [InlineData(nameof(User.Username))]
    [InlineData(nameof(User.PasswordHash))]
    public void Rend_obligatoires_les_colonnes_d_authentification(string propriete)
    {
        Propriete(propriete).IsNullable.Should().BeFalse();
    }

    // L'empreinte reste sans plafond : sa longueur dépend de l'algorithme, et un jour de
    // migration d'algorithme, une colonne trop étroite tronquerait silencieusement des
    // empreintes — c'est-à-dire enfermerait les Chasseurs dehors.
    [Fact]
    public void Ne_borne_pas_la_longueur_de_l_empreinte()
    {
        Propriete(nameof(User.PasswordHash)).GetMaxLength().Should().BeNull();
    }

    [Fact]
    public void Compare_les_noms_de_Chasseur_sans_distinction_de_casse()
    {
        Propriete(nameof(User.Username)).GetCollation()
            .Should().Be(AriseDbContext.CollationInsensibleALaCasse);
    }

    // La collation ne sert à rien si la base ne la connaît pas : le DDL doit la créer.
    [Fact]
    public void Cree_la_collation_insensible_a_la_casse_dans_le_schema()
    {
        var ddl = Contexte().Database.GenerateCreateScript();

        ddl.Should().Contain($"CREATE COLLATION {AriseDbContext.CollationInsensibleALaCasse}");
    }

    // Une collation déterministe comparerait « Sung » et « sung » comme deux noms distincts
    // malgré l'index unique : c'est le deterministic = false qui fait tout le travail.
    [Fact]
    public void Declare_la_collation_comme_non_deterministe()
    {
        var ddl = Contexte().Database.GenerateCreateScript();

        ddl.Should().Contain("DETERMINISTIC = False");
    }
}
