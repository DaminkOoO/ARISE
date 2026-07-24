using Arise.Application.Common.Abstractions;
using Arise.Domain.Hunters;
using Arise.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve le repository de profils sur un vrai Postgres : le round-trip d'un profil
/// nouvellement créé (chemin <c>OnboardHunterCommandHandler</c>) et celui d'un profil existant
/// muté puis re-sauvegardé (chemin <c>AwardXpCommandHandler</c>), depuis un contexte neuf à
/// chaque lecture pour ne jamais lire le cache d'identité de l'écriture.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfHunterProfileRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Relit_un_profil_nouvellement_cree_depuis_un_contexte_neuf()
    {
        var profil = HunterProfile.Create();

        await using (var ecriture = postgres.Fournisseur())
        {
            await ecriture.GetRequiredService<IHunterProfileRepository>()
                .SaveAsync(profil, CancellationToken.None);
        }

        await using var lecture = postgres.Fournisseur();
        var relu = await lecture.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(profil.Id, CancellationToken.None);

        relu.Should().NotBeNull();
        relu!.Id.Should().Be(profil.Id);
        relu.Level.Should().Be(profil.Level);
        relu.Rank.Should().Be(profil.Rank);
        relu.CurrentXp.Should().Be(profil.CurrentXp);
        relu.XpToNextLevel.Should().Be(profil.XpToNextLevel);
        relu.StreakCurrent.Should().Be(profil.StreakCurrent);
        relu.StreakLongest.Should().Be(profil.StreakLongest);
        relu.LastCompletionDate.Should().Be(profil.LastCompletionDate);
    }

    // 520 XP fait franchir le niveau 5 (rang D) depuis le niveau de départ : Level, Rank et
    // CurrentXp bougent tous les trois, exactement ce que persiste AwardXpCommandHandler.
    [Fact]
    public async Task Relit_les_mutations_d_un_profil_existant_apres_attribution_d_XP()
    {
        var profil = HunterProfile.Create();

        await using (var creation = postgres.Fournisseur())
        {
            await creation.GetRequiredService<IHunterProfileRepository>()
                .SaveAsync(profil, CancellationToken.None);
        }

        await using (var mutation = postgres.Fournisseur())
        {
            var repository = mutation.GetRequiredService<IHunterProfileRepository>();
            var charge = await repository.GetByIdAsync(profil.Id, CancellationToken.None);
            charge!.AwardXp(520);
            await repository.SaveAsync(charge, CancellationToken.None);
        }

        await using var lecture = postgres.Fournisseur();
        var relu = await lecture.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(profil.Id, CancellationToken.None);

        relu.Should().NotBeNull();
        relu!.Level.Should().Be(5);
        relu.Rank.Should().Be(HunterRank.D);
        relu.CurrentXp.Should().Be(0);
    }

    // La série et sa date sont ce que RegisterDailyCompletion mute ; on éprouve aussi le cas
    // null explicitement, puisque LastCompletionDate démarre nullable.
    [Fact]
    public async Task Relit_la_serie_et_la_date_de_derniere_completion()
    {
        var profil = HunterProfile.Create();
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 24));

        await using (var ecriture = postgres.Fournisseur())
        {
            await ecriture.GetRequiredService<IHunterProfileRepository>()
                .SaveAsync(profil, CancellationToken.None);
        }

        await using var lecture = postgres.Fournisseur();
        var relu = await lecture.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(profil.Id, CancellationToken.None);

        relu.Should().NotBeNull();
        relu!.StreakCurrent.Should().Be(1);
        relu.StreakLongest.Should().Be(1);
        relu.LastCompletionDate.Should().Be(new DateOnly(2026, 7, 24));
    }

    [Fact]
    public async Task Rend_null_en_cherchant_un_profil_absent()
    {
        await using var lecture = postgres.Fournisseur();

        var relu = await lecture.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        relu.Should().BeNull();
    }
}
