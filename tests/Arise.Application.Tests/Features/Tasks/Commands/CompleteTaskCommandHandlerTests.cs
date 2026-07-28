using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Tasks.Commands.CompleteTask;
using Arise.Domain.Tasks;
using FluentAssertions;
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

    private readonly Guid _chasseur = Guid.NewGuid();
    private readonly TaskItem _tache;

    public CompleteTaskCommandHandlerTests()
    {
        _tache = TaskItem.Create(_chasseur, "Appeler le dentiste", null, Creation);

        _taches.GetByIdAsync(_tache.Id, Arg.Any<CancellationToken>()).Returns(_tache);
    }

    private Task<CompleteTaskResult> Completer(Guid? chasseur = null, Guid? tache = null) =>
        new CompleteTaskCommandHandler(_taches, new HorlogeFigee()).Handle(
            new CompleteTaskCommand(chasseur ?? _chasseur, tache ?? _tache.Id),
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

    private sealed class HorlogeFigee : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Maintenant;
    }
}
