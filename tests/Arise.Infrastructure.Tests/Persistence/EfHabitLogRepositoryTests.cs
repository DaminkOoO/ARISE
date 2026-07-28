using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve le journal des habitudes sur un vrai Postgres : le round-trip d'une journalisation,
/// la projection en jours que promet <see cref="IHabitLogRepository"/>, et l'unicité par jour —
/// la seule chose qui puisse trancher deux taps simultanés, que la lecture préalable du handler
/// ne voit pas.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfHabitLogRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Tap =
        new(2026, 7, 26, 21, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Jour = new(2026, 7, 26);

    /// <summary>
    /// Une entrée de journal vise une habitude qui vise un Chasseur : les deux clés étrangères
    /// l'exigent, comme en production.
    /// </summary>
    private async Task<Habit> HabitudePosee(string nom)
    {
        var profil = HunterProfile.Create();
        var habitude = Habit.Create(profil.Id, nom, HabitFrequency.Quotidienne, Creation);

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);
        await fournisseur.GetRequiredService<IHabitRepository>()
            .AddAsync(habitude, CancellationToken.None);

        return habitude;
    }

    private async Task Journaliser(Habit habitude, DateOnly jour)
    {
        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHabitLogRepository>()
            .AddAsync(HabitLog.Create(habitude.Id, jour, Tap), CancellationToken.None);
    }

    private async Task<IReadOnlyList<DateOnly>> Relire(Habit habitude)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<IHabitLogRepository>()
            .GetDaysAsync(habitude.Id, CancellationToken.None);
    }

    [Fact]
    public async Task Relit_un_jour_journalise_depuis_un_contexte_neuf()
    {
        var habitude = await HabitudePosee("Boire deux litres d'eau");
        await Journaliser(habitude, Jour);

        (await Relire(habitude)).Should().ContainSingle().Which.Should().Be(Jour);
    }

    [Fact]
    public async Task Relit_tous_les_jours_tenus()
    {
        var habitude = await HabitudePosee("Lire vingt minutes");
        await Journaliser(habitude, new DateOnly(2026, 7, 24));
        await Journaliser(habitude, new DateOnly(2026, 7, 25));

        (await Relire(habitude)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Ne_rend_pas_les_jours_d_une_autre_habitude()
    {
        var habitude = await HabitudePosee("Méditer cinq minutes");
        var autre = await HabitudePosee("Étirements du soir");
        await Journaliser(autre, Jour);

        (await Relire(habitude)).Should().BeEmpty();
    }

    // Le jour survit tel quel à l'aller-retour : c'est lui que la série compte, et un décalage
    // d'un jour au stockage la fausserait sans que rien ne le signale.
    [Fact]
    public async Task Relit_le_jour_tel_qu_il_a_ete_ecrit()
    {
        var habitude = await HabitudePosee("Marcher trente minutes");
        await Journaliser(habitude, new DateOnly(2026, 1, 1));

        (await Relire(habitude)).Should().ContainSingle()
            .Which.Should().Be(new DateOnly(2026, 1, 1));
    }

    // La lecture préalable du handler ne tranche pas deux taps simultanés — deux scopes, deux
    // DbContext, aucun des deux ne voit l'autre. C'est cet index unique qui la tranche, traduit
    // en vocabulaire métier pour que la couche Application n'ait pas à connaître Npgsql.
    [Fact]
    public async Task Refuse_deux_entrees_pour_la_meme_habitude_le_meme_jour()
    {
        var habitude = await HabitudePosee("Faire dix pompes");
        await Journaliser(habitude, Jour);

        var acte = async () => await Journaliser(habitude, Jour);

        await acte.Should().ThrowAsync<HabitAlreadyLoggedException>();
    }

    // L'unicité porte sur le couple habitude/jour, et pas sur le jour seul : tenir deux
    // habitudes le même jour est le cas normal, pas un conflit.
    [Fact]
    public async Task Accepte_le_meme_jour_pour_deux_habitudes_distinctes()
    {
        var habitude = await HabitudePosee("Nager le mardi");
        var autre = await HabitudePosee("Écrire trois pages");

        await Journaliser(habitude, Jour);
        await Journaliser(autre, Jour);

        (await Relire(autre)).Should().ContainSingle();
    }

    /// <summary>
    /// La journalisation perdante n'annule pas la commande : le handler rattrape
    /// <see cref="HabitAlreadyLoggedException"/> et rend un succès, après quoi
    /// <c>TransactionBehavior</c> valide. Or une violation d'unicité <b>abandonne</b> la
    /// transaction PostgreSQL — sans le point de sauvegarde qu'EF Core pose avant chaque
    /// <c>SaveChangesAsync</c>, ce <c>COMMIT</c> échouerait sur un 25P02 et le Chasseur verrait
    /// une erreur technique pour un double-tap.
    /// </summary>
    [Fact]
    public async Task Une_entree_refusee_en_transaction_laisse_valider_la_suite()
    {
        var habitude = await HabitudePosee("Ranger le bureau");

        await using (var fournisseur = postgres.Fournisseur())
        {
            var journaux = fournisseur.GetRequiredService<IHabitLogRepository>();
            var uniteDeTravail = fournisseur.GetRequiredService<IUnitOfWork>();

            await uniteDeTravail.CommencerAsync(CancellationToken.None);

            await journaux.AddAsync(
                HabitLog.Create(habitude.Id, Jour, Tap), CancellationToken.None);

            var acte = async () => await journaux.AddAsync(
                HabitLog.Create(habitude.Id, Jour, Tap), CancellationToken.None);
            await acte.Should().ThrowAsync<HabitAlreadyLoggedException>();

            var validation = async () => await uniteDeTravail.ValiderAsync(CancellationToken.None);
            await validation.Should().NotThrowAsync();
        }

        (await Relire(habitude)).Should().ContainSingle();
    }
}
