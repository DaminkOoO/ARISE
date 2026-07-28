using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Hunters.Commands.AwardXp;
using Arise.Application.Features.Tasks.Commands.CompleteTask;
using Arise.Domain.Habits;
using Arise.Domain.Tasks;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Arise.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// La complétion d'une tâche. Aucun fuseau horaire dans la commande, contrairement à celle d'une
/// quête : rien ici ne dépend du <b>jour</b> du Chasseur — pas de série à créditer, pas de fenêtre
/// de complétion à faire respecter. Seul l'instant absolu est horodaté.
/// </summary>
public class CompleteTaskCommandHandlerTests
{
    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Maintenant =
        new(2026, 7, 27, 18, 15, 0, TimeSpan.Zero);

    private readonly ITaskItemRepository _taches = Substitute.For<ITaskItemRepository>();
    private readonly IHabitLogRepository _journaux = Substitute.For<IHabitLogRepository>();
    private readonly ISender _mediateur = Substitute.For<ISender>();

    private readonly Guid _chasseur = Guid.NewGuid();
    private readonly TaskItem _tache;

    public CompleteTaskCommandHandlerTests()
    {
        _tache = TaskItem.Create(_chasseur, "Appeler le dentiste", null, Creation);

        _taches.GetByIdAsync(_tache.Id, Arg.Any<CancellationToken>()).Returns(_tache);
        _journaux.GetDayFrequenciesForHunterAsync(
                Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _taches.CountCompletedBetweenAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(0);
    }

    /// <summary>Ce que le Chasseur a déjà tenu aujourd'hui, avant le geste en cours.</summary>
    private void DejaTenuAujourdHui(HabitFrequency[]? habitudes = null, int taches = 0)
    {
        _journaux.GetDayFrequenciesForHunterAsync(
                Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(habitudes ?? []);
        _taches.CountCompletedBetweenAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(taches);
    }

    private Task<CompleteTaskResult> Completer(
        Guid? chasseur = null, Guid? tache = null, string fuseau = "Europe/Paris") =>
        new CompleteTaskCommandHandler(
                _taches, _journaux, _mediateur, new HorlogeFigee())
            .Handle(
                new CompleteTaskCommand(chasseur ?? _chasseur, tache ?? _tache.Id, fuseau),
                CancellationToken.None);

    [Fact]
    public async Task Marque_la_tache_faite()
    {
        await Completer();

        _tache.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Persiste_la_tache_completee()
    {
        await Completer();

        await _taches.Received(1).SaveAsync(_tache, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Horodate_la_completion_de_l_horloge_injectee()
    {
        (await Completer()).CompletedAt.Should().Be(Maintenant);
    }

    [Fact]
    public async Task Annonce_une_completion_neuve()
    {
        (await Completer()).DejaCompletee.Should().BeFalse();
    }

    [Fact]
    public async Task Rend_l_identifiant_de_la_tache_completee()
    {
        (await Completer()).TaskId.Should().Be(_tache.Id);
    }

    // Double-tap, renvoi réseau, deux appareils : ce n'est pas une erreur, la tâche est faite.
    [Fact]
    public async Task Annonce_une_tache_deja_faite_sans_la_traiter_comme_une_erreur()
    {
        _tache.Complete(Creation);

        (await Completer()).DejaCompletee.Should().BeTrue();
    }

    // L'instant rendu reste celui de la vraie complétion : afficher celui du second tap
    // réécrirait l'histoire pour un geste qui n'a rien changé.
    [Fact]
    public async Task Rend_l_instant_de_la_premiere_completion_pour_une_tache_deja_faite()
    {
        _tache.Complete(Creation);

        (await Completer()).CompletedAt.Should().Be(Creation);
    }

    [Fact]
    public async Task N_ecrit_rien_pour_une_tache_deja_faite()
    {
        _tache.Complete(Creation);

        await Completer();

        await _taches.DidNotReceive().SaveAsync(
            Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuse_de_completer_une_tache_inconnue()
    {
        _taches.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TaskItem?)null);

        var acte = async () => await Completer();

        await acte.Should().ThrowAsync<TaskNotFoundException>();
    }

    // Sans ce contrôle, n'importe quel Chasseur cocherait les tâches des autres. Même exception
    // que pour une tâche inconnue, pour ne pas révéler celle d'autrui.
    [Fact]
    public async Task Refuse_de_completer_la_tache_d_un_autre_Chasseur()
    {
        var acte = async () => await Completer(chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<TaskNotFoundException>();
    }

    [Fact]
    public async Task N_ecrit_rien_pour_la_tache_d_un_autre_Chasseur()
    {
        var acte = async () => await Completer(chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<TaskNotFoundException>();
        await _taches.DidNotReceive().SaveAsync(
            Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ne_marque_pas_faite_la_tache_d_un_autre_Chasseur()
    {
        var acte = async () => await Completer(chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<TaskNotFoundException>();
        _tache.IsCompleted.Should().BeFalse();
    }

    // --- XP d'engagement (doc mécaniques, section 1) -----------------------------------------

    [Fact]
    public async Task Accorde_cinq_XP_pour_une_tache_cochee()
    {
        await Completer();

        await _mediateur.Received(1).Send(
            Arg.Is<AwardXpCommand>(commande =>
                commande != null && commande.HunterProfileId == _chasseur && commande.Montant == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rend_l_XP_acquis()
    {
        (await Completer()).XpAcquis.Should().Be(5);
    }

    [Fact]
    public async Task N_accorde_aucun_XP_pour_une_tache_deja_faite()
    {
        _tache.Complete(Creation);

        var resultat = await Completer();

        resultat.XpAcquis.Should().Be(0);
        await _mediateur.DidNotReceive().Send(
            Arg.Any<AwardXpCommand>(), Arg.Any<CancellationToken>());
    }

    // Cinq tâches déjà cochées valent 25 XP : le plafond est atteint.
    [Fact]
    public async Task N_accorde_plus_rien_une_fois_le_plafond_atteint()
    {
        DejaTenuAujourdHui(taches: 5);

        (await Completer()).XpAcquis.Should().Be(0);
    }

    // Le plafond rogne le gain, jamais le geste : la tâche reste cochée et persistée.
    [Fact]
    public async Task Coche_quand_meme_la_tache_une_fois_le_plafond_atteint()
    {
        DejaTenuAujourdHui(taches: 5);

        await Completer();

        _tache.IsCompleted.Should().BeTrue();
        await _taches.Received(1).SaveAsync(_tache, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rogne_le_gain_qui_deborderait_du_plafond()
    {
        // 23 XP déjà acquis (1 hebdomadaire + 1 quotidienne + 2 tâches) : il en reste 2.
        DejaTenuAujourdHui(
            habitudes: [HabitFrequency.Hebdomadaire, HabitFrequency.Quotidienne],
            taches: 2);

        (await Completer()).XpAcquis.Should().Be(2);
    }

    // Le plafond est cumulé : les habitudes tenues aujourd'hui le réduisent aussi.
    [Fact]
    public async Task Compte_les_habitudes_du_jour_dans_le_plafond()
    {
        DejaTenuAujourdHui(
            habitudes:
            [
                HabitFrequency.Hebdomadaire,
                HabitFrequency.Hebdomadaire,
                HabitFrequency.Quotidienne,
            ]);

        // 23 XP acquis, il en reste 2 sur les 5 que vaut une tâche.
        (await Completer()).XpAcquis.Should().Be(2);
    }

    // La journée du Chasseur, pas celle du serveur : c'est la seule raison d'être du fuseau dans
    // cette commande, la complétion elle-même n'en ayant aucun besoin.
    [Fact]
    public async Task Compte_le_plafond_sur_la_journee_du_Chasseur()
    {
        await Completer(fuseau: "Europe/Paris");

        // Le 27 à 18h15 UTC est le 27 à 20h15 à Paris : la fenêtre interrogée est celle du 27
        // local, qui commence le 26 à 22h00 UTC.
        await _taches.Received(1).CountCompletedBetweenAsync(
            _chasseur,
            new DateTimeOffset(2026, 7, 26, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 27, 22, 0, 0, TimeSpan.Zero),
            Arg.Any<CancellationToken>());
    }

    private sealed class HorlogeFigee : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Maintenant;
    }
}
