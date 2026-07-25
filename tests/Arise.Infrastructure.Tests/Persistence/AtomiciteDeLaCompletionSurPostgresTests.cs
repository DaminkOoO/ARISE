using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Features.Sport.Commands.CompleteGymQuest;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// L'atomicité de la complétion, prouvée par la seule voie honnête : une panne réelle au milieu
/// d'une commande réelle, sur un vrai Postgres. Le provider InMemory ignore les transactions —
/// il rendrait ces tests verts sans jamais rien annuler.
///
/// <para>La panne est provoquée dans la <b>publication des événements</b>, c'est-à-dire à la
/// toute fin : la quête est déjà marquée complétée, l'XP déjà accordé, et la série déjà écrite
/// par <c>StreakUpdateHandler</c> — l'abonné saboteur est enregistré après lui. Les trois
/// écritures doivent donc disparaître ensemble, alors qu'elles ont été faites par trois chemins
/// distincts (un handler de commande, un handler de commande imbriquée, un abonné).</para>
/// </summary>
[Collection(PostgresCollection.Nom)]
public class AtomiciteDeLaCompletionSurPostgresTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset TardLeVingtCinqANewYork =
        new(2026, 7, 26, 3, 30, 0, TimeSpan.Zero);

    private const string FuseauNewYork = "America/New_York";

    private static readonly DateOnly JourDeLaQuete = new(2026, 7, 25);

    private async Task<(Guid Chasseur, Guid Quete)> ChasseurAvecQueteDuJour()
    {
        var profil = HunterProfile.Create();
        var quete = Quest.Generate(
            profil.Id,
            QuestDomain.Sport,
            JourDeLaQuete,
            "L'Épreuve du Guerrier",
            "Bouge à ton rythme : marche, gainage, étirements.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Moyenne,
            20,
            isFallback: false);

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);
        await fournisseur.GetRequiredService<IQuestRepository>()
            .SaveAsync(quete, CancellationToken.None);

        return (profil.Id, quete.Id);
    }

    /// <summary>
    /// Complète en greffant un abonné qui tombe. La panne remonte forcément — c'est le sujet
    /// d'un test à part —, on l'absorbe ici pour pouvoir observer la base ensuite.
    /// </summary>
    private async Task CompleterAvecPanneALaPublication(Guid chasseur, Guid quete)
    {
        try
        {
            await Completer(chasseur, quete, AvecAbonneQuiTombe);
        }
        catch (InvalidOperationException)
        {
            // Attendue.
        }
    }

    private async Task<CompleteGymQuestResult> Completer(
        Guid chasseur, Guid quete, Action<IServiceCollection>? enPlus = null)
    {
        await using var fournisseur =
            postgres.FournisseurApplicatif(new HorlogeFigee(TardLeVingtCinqANewYork), enPlus);

        return await fournisseur.GetRequiredService<ISender>().Send(
            new CompleteGymQuestCommand(chasseur, quete, FuseauNewYork), CancellationToken.None);
    }

    private static void AvecAbonneQuiTombe(IServiceCollection services) =>
        services.AddTransient<INotificationHandler<DomainEventNotification<QuestCompletedEvent>>,
            AbonneQuiTombe>();

    private async Task<HunterProfile> ProfilRelu(Guid chasseur)
    {
        await using var fournisseur = postgres.Fournisseur();

        return (await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(chasseur, CancellationToken.None))!;
    }

    private async Task<Quest> QueteRelue(Guid quete)
    {
        await using var fournisseur = postgres.Fournisseur();

        return (await fournisseur.GetRequiredService<IQuestRepository>()
            .GetByIdAsync(quete, CancellationToken.None))!;
    }

    [Fact]
    public async Task Laisse_la_quete_a_accomplir_quand_la_publication_echoue()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await CompleterAvecPanneALaPublication(chasseur, quete);

        (await QueteRelue(quete)).IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task N_accorde_aucun_XP_quand_la_publication_echoue()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await CompleterAvecPanneALaPublication(chasseur, quete);

        (await ProfilRelu(chasseur)).CurrentXp.Should().Be(0);
    }

    [Fact]
    public async Task Ne_compte_aucune_serie_quand_la_publication_echoue()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await CompleterAvecPanneALaPublication(chasseur, quete);

        (await ProfilRelu(chasseur)).StreakCurrent.Should().Be(0);
    }

    // L'annulation ne transforme pas la panne en succès silencieux : le Chasseur doit voir un
    // échec, pas un écran qui annonce une quête accomplie qui ne l'est pas.
    [Fact]
    public async Task Laisse_la_panne_remonter_jusqu_a_l_appelant()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        var acte = async () => await Completer(chasseur, quete, AvecAbonneQuiTombe);

        (await acte.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage(AbonneQuiTombe.Message);
    }

    /// <summary>
    /// La promesse qui manquait, et toute la raison de cette tâche : après une panne, le retap
    /// du Chasseur est une <b>vraie première complétion</b>, pas un « déjà accomplie ».
    ///
    /// <para>C'est le symptôme exact que décrivait le défaut : une commande tombée à mi-parcours
    /// laissait la quête marquée complétée en base, si bien qu'au retap la garde d'idempotence
    /// répondait <c>DejaCompletee: true</c> — avec un montant d'XP que le Chasseur n'avait sur
    /// aucune ligne. La panne « se rattrapait », disait le commentaire ; rien ne la rattrapait.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Traite_le_second_essai_comme_une_premiere_completion()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await CompleterAvecPanneALaPublication(chasseur, quete);
        var resultat = await Completer(chasseur, quete);

        resultat.DejaCompletee.Should().BeFalse();
    }

    [Fact]
    public async Task Accorde_l_XP_au_second_essai_apres_la_panne()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await CompleterAvecPanneALaPublication(chasseur, quete);
        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).CurrentXp.Should().Be(20);
    }

    [Fact]
    public async Task Compte_la_serie_au_second_essai_apres_la_panne()
    {
        var (chasseur, quete) = await ChasseurAvecQueteDuJour();

        await CompleterAvecPanneALaPublication(chasseur, quete);
        await Completer(chasseur, quete);

        (await ProfilRelu(chasseur)).StreakCurrent.Should().Be(1);
    }

    /// <summary>
    /// Abonné saboteur. Enregistré après <c>StreakUpdateHandler</c> — MediatR réveille les
    /// abonnés dans l'ordre d'enregistrement — pour que la panne survienne une fois la série
    /// déjà écrite : c'est le scénario le plus exigeant pour l'annulation.
    /// </summary>
    private sealed class AbonneQuiTombe
        : INotificationHandler<DomainEventNotification<QuestCompletedEvent>>
    {
        public const string Message = "panne simulée pendant la publication du fait de complétion";

        public Task Handle(
            DomainEventNotification<QuestCompletedEvent> notification,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);
    }

    private sealed class HorlogeFigee(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
