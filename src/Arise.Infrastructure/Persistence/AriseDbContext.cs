using Arise.Domain.Hunters;
using Arise.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Arise.Infrastructure.Persistence;

public sealed class AriseDbContext(DbContextOptions<AriseDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Collation ICU non déterministe : elle compare « Sung » et « sung » comme égaux, à
    /// l'index unique comme à la recherche par nom.
    ///
    /// <para>Non déterministe a un prix, et il vaut la peine d'être connu : PostgreSQL
    /// refuse <c>LIKE</c> et les opérateurs de motif sur une colonne qui la porte. C'est
    /// acceptable ici parce que le nom d'un Chasseur ne se cherche que par égalité — le jour
    /// où un écran voudra une recherche « commence par », il faudra une colonne normalisée à
    /// côté, pas retirer cette collation.</para>
    /// </summary>
    public const string CollationInsensibleALaCasse = "insensible_a_la_casse";

    public DbSet<User> Users => Set<User>();

    public DbSet<HunterProfile> HunterProfiles => Set<HunterProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // und-u-ks-level2 : comparaison au niveau 2, qui ignore la casse mais garde les
        // accents distincts — « Séraphin » et « Seraphin » restent deux Chasseurs.
        modelBuilder.HasCollation(
            CollationInsensibleALaCasse,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false);

        // Balayage de l'assembly plutôt qu'une liste à tenir à jour : une configuration
        // ajoutée et jamais référencée serait silencieusement ignorée, et le mode de panne
        // — une contrainte absente en base — ne se voit qu'en production.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AriseDbContext).Assembly);
    }
}
