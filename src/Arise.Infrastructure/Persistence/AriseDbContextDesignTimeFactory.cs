using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Contexte fabriqué pour l'outillage <c>dotnet ef</c>, et pour lui seul.
///
/// <para>Générer une migration se fait à partir du modèle, sans jamais joindre de serveur :
/// la chaîne ci-dessous n'a donc pas à être réelle, et ne doit surtout pas l'être — un
/// identifiant de connexion versionné est un secret versionné.</para>
///
/// <para>Cette fabrique évite aussi de faire dépendre la génération des migrations du
/// démarrage de l'API : sans elle, <c>dotnet ef</c> construirait l'hôte web, qui réclamerait
/// une configuration complète pour produire un simple fichier C#.</para>
/// </summary>
internal sealed class AriseDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<AriseDbContext>
{
    public AriseDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AriseDbContext>()
            .UseNpgsql("Host=hôte-de-conception;Database=arise")
            .UseSnakeCaseNamingConvention()
            .Options);
}
