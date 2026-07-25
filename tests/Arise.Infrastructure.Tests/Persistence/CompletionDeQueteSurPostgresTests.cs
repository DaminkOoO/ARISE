using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Sport.Commands.CompleteGymQuest;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// La complétion d'une quête, éprouvée sur un vrai Postgres avec le vrai MediatR : la commande,
/// l'<c>AwardXpCommand</c> qu'elle déclenche et le <c>StreakUpdateHandler</c> qu'elle réveille
/// mutent tous deux le <b>même</b> profil de Chasseur, chacun le chargeant et le sauvegardant de
/// son côté.
///
/// <para>C'est le seul test qui puisse le prouver honnêtement : deux doublures ne se marchent
/// jamais dessus, une ligne relue en base si. L'XP et la série sont vérifiés sur la ligne relue,
/// pas sur l'instance mutée en mémoire.</para>
/// </summary>
[Collection(PostgresCollection.Nom)]
public class CompletionDeQueteSurPostgresTests(PostgresFixture postgres)
{
    // 03h30 UTC le 26, soit encore 23h30 le 25 à New York : la série doit compter le 25.
    private static readonly DateTimeOffset TardLeVingtCinqANewYork =
        new(2026, 7, 26, 3, 30, 0, TimeSpan.Zero);

    private const string FuseauNewYork = "America/New_York";

    private static Quest QueteDuJour(Guid chasseur, DateOnly jour) =>
        Quest.Generate(
            chasseur,
            QuestDomain.Sport,
            jour,
            "L'Épreuve du Guerrier",
            "Bouge à ton rythme : marche, gainage, étirements.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Moyenne,
            20,
            isFallback: false);

    private async Task<(Guid Chasseur, Guid Quete)> ChasseurAvecQueteDuJour()
    {
        var profil = HunterProfile.Create();
        var quete = QueteDuJour(profil.Id, new DateOnly(2026, 7, 25));

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);
        await fournisseur.GetRequiredService<IQuestRepository>()
            .SaveAsync(quete, CancellationToken.None);

        return (profil.Id, quete.Id);
    }

    /// <summary>
    /// La quête de la veille, restée à accomplir, et celle du jour : deux quêtes distinctes sur
    /// un même profil, toutes deux dans la fenêtre de complétion.
    /// </summary>
    private async Task<(Guid Chasseur, Guid Veille, Guid Jour)> ChasseurAvecDeuxQuetes()
    {
        var profil = HunterProfile.Create();
        var veille = QueteDuJour(profil.Id, new DateOnly(2026, 7, 24));
        var jour = QueteDuJour(profil.Id, new DateOnly(2026, 7, 25));

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);
        await fournisseur.GetRequiredService<IQuestRepository>()
            .SaveAsync(veille, CancellationToken.None);
        await fournisseur.GetRequiredService<IQuestRepository>()
            .SaveAsync(jour, CancellationToken.None);

        return (profil.Id, veille.Id, jour.Id);
    }

    private async Task Completer(Guid chasseur, Guid quete)
    {
        await using var fournisseur =
            postgres.FournisseurApplicatif(new HorlogeFigee(TardLeVingtCinqANewYork));

        await fournisseur.GetRequiredService<ISender>().Send(
            new CompleteGymQuestCommand(chasseur, quete, FuseauNewYork), CancellationToken.None);
    }

    private async Task<HunterProfile> ProfilRelu(Guid chasseur)
    {
        await using var fournisseur = postgres.Fournisseur();
        var profil = await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(chasseur, CancellationToken.None);

        return profil!;
    }

    [Fact]
    public async Task Accorde_l_XP_de_la_quete_sur_la_ligne_relue()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).CurrentXp.Should().Be(20);
    }

    // L'autre moitié de la même écriture : si l'attribution d'XP et la mise à jour de série se
    // marchaient dessus, l'une des deux serait ici à sa valeur de départ.
    [Fact]
    public async Task Compte_la_completion_dans_la_serie_sur_la_ligne_relue()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).StreakCurrent.Should().Be(1);
    }

    [Fact]
    public async Task Date_la_serie_du_jour_du_Chasseur_sur_la_ligne_relue()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).LastCompletionDate.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public async Task Marque_la_quete_completee_en_base()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Completer(chasseur, quete);

        await using var fournisseur = postgres.Fournisseur();
        var relue = await fournisseur.GetRequiredService<IQuestRepository>()
            .GetByIdAsync(quete, CancellationToken.None);

        relue!.IsCompleted.Should().BeTrue();
    }

    // Le double-tap, joué en vrai : deux commandes successives, un seul gain. La garde de
    // l'entité tient parce que la première complétion est persistée avant la seconde lecture.
    [Fact]
    public async Task N_accorde_l_XP_qu_une_fois_quand_la_quete_est_completee_deux_fois()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Completer(chasseur, quete);
        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).CurrentXp.Should().Be(20);
    }

    [Fact]
    public async Task Ne_compte_la_serie_qu_une_fois_quand_la_quete_est_completee_deux_fois()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Completer(chasseur, quete);
        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).StreakCurrent.Should().Be(1);
    }

    // Le double-tap *simultané*, joué en vrai : deux commandes parties ensemble, chacune dans
    // son scope — c'est ainsi que deux requêtes HTTP arrivent. Aucune des deux ne voit l'autre
    // en mémoire ; seul le jeton de concurrence en base empêche les deux de créditer.
    [Fact]
    public async Task N_accorde_l_XP_qu_une_fois_quand_deux_completions_partent_ensemble()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Task.WhenAll(Completer(chasseur, quete), Completer(chasseur, quete));

        (await ProfilRelu(chasseur)).CurrentXp.Should().Be(20);
    }

    [Fact]
    public async Task Ne_compte_la_serie_qu_une_fois_quand_deux_completions_partent_ensemble()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await Task.WhenAll(Completer(chasseur, quete), Completer(chasseur, quete));

        (await ProfilRelu(chasseur)).StreakCurrent.Should().Be(1);
    }

    // Deux quêtes distinctes complétées ensemble : chaque scope charge le profil au même total,
    // ajoute ses 20 XP et écrit le sien. Sans jeton de concurrence, le second effaçait le
    // premier et 40 XP gagnés n'en faisaient que 20 — le témoin séquentiel, lui, donnait bien
    // 40. Dès la Phase 2, ce sera Sport et Habitudes au même instant.
    [Fact]
    public async Task Cumule_l_XP_de_deux_quetes_completees_ensemble()
    {
        var (chasseur, veille, jour) = await ChasseurAvecDeuxQuetes();

        await Task.WhenAll(Completer(chasseur, veille), Completer(chasseur, jour));

        (await ProfilRelu(chasseur)).CurrentXp.Should().Be(40);
    }

    // Deux jours consécutifs comptés, quel que soit l'ordre dans lequel la course s'est jouée :
    // c'est la garde du domaine — la série ne recule pas — qui le tient.
    [Fact]
    public async Task Compte_les_deux_jours_de_deux_quetes_completees_ensemble()
    {
        var (chasseur, veille, jour) = await ChasseurAvecDeuxQuetes();

        await Task.WhenAll(Completer(chasseur, veille), Completer(chasseur, jour));

        (await ProfilRelu(chasseur)).LastCompletionDate.Should().Be(new DateOnly(2026, 7, 25));
    }

    private sealed class HorlogeFigee(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
