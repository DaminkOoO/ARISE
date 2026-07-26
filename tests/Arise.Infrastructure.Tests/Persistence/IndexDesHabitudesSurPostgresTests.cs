using Arise.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve les index réellement posés sur <c>habits</c> par les migrations, sur le vrai
/// Postgres.
///
/// <para>L'assertion porte sur <c>pg_indexes</c> et non sur un plan d'exécution : un
/// <c>EXPLAIN</c> exigerait de peupler la table de milliers de lignes puis d'<c>ANALYZE</c>r
/// pour que le planificateur daigne quitter le <c>Seq Scan</c>, et le verdict resterait suspendu
/// à ses estimations de coût — donc à la version de Postgres et au volume choisi. Le catalogue,
/// lui, dit sans ambiguïté ce que le schéma déployé contient. C'est la régression qu'on
/// verrouille : la disparition de l'index, pas l'humeur du planificateur.</para>
/// </summary>
[Collection(PostgresCollection.Nom)]
public class IndexDesHabitudesSurPostgresTests(PostgresFixture postgres)
{
    private async Task<IReadOnlyList<string>> DefinitionsDesIndex()
    {
        await using var fournisseur = postgres.Fournisseur();

        return await fournisseur.GetRequiredService<AriseDbContext>().Database
            .SqlQueryRaw<string>(
                "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'habits'")
            .ToListAsync();
    }

    /// <summary>
    /// Un index <b>filtré</b> ne sert qu'aux requêtes qui portent son prédicat :
    /// <c>WHERE hunter_profile_id = $1</c> n'implique pas <c>NOT is_archived</c>, et PostgreSQL
    /// retombe alors sur un balayage complet.
    /// </summary>
    private static bool EstNonFiltreEtOuvreSurLeChasseur(string definition) =>
        definition.Contains("(hunter_profile_id", StringComparison.Ordinal)
        && !definition.Contains("WHERE", StringComparison.Ordinal);

    /// <summary>
    /// Sans cet index, lister les habitudes d'un Chasseur balaye celles de <b>tous</b> les
    /// Chasseurs — à chaque ouverture de l'écran Habitudes, et à chaque vérification de la
    /// cascade lors de la suppression d'un profil. L'index unique partiel sur
    /// <c>(hunter_profile_id, name)</c> ne peut pas y suppléer : son filtre le réserve aux
    /// requêtes qui excluent les archivées.
    /// </summary>
    [Fact]
    public async Task Un_index_non_filtre_ouvre_sur_le_Chasseur()
    {
        var definitions = await DefinitionsDesIndex();

        definitions.Where(EstNonFiltreEtOuvreSurLeChasseur).Should().NotBeEmpty(
            "lister les habitudes d'un Chasseur ne doit pas balayer toute la table ; "
            + "index posés : {0}",
            string.Join(" | ", definitions));
    }
}
