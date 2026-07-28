using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Tasks.Commands.CreateTask;
using Arise.Domain.Hunters;
using Arise.Domain.Tasks;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// La déclaration d'une tâche. Elle n'accorde aucun XP et ne touche à aucune série : se donner
/// quelque chose à faire ne fait pas progresser le Chasseur.
///
/// <para>Aucun contrôle d'unicité du titre, contrairement aux habitudes : deux tâches
/// « Appeler le dentiste » à deux mois d'écart sont deux tâches, pas un doublon.</para>
/// </summary>
public class CreateTaskCommandHandlerTests
{
    private static readonly DateTimeOffset Maintenant =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly ITaskItemRepository _taches = Substitute.For<ITaskItemRepository>();

    private readonly HunterProfile _profil = HunterProfile.Create();

    public CreateTaskCommandHandlerTests()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_profil);
    }

    private Task<CreateTaskResult> Creer(
        string titre = "Appeler le dentiste",
        DateOnly? echeance = null,
        Guid? chasseur = null) =>
        new CreateTaskCommandHandler(_profils, _taches, new HorlogeFigee()).Handle(
            new CreateTaskCommand(chasseur ?? _profil.Id, titre, echeance),
            CancellationToken.None);

    [Fact]
    public async Task Persiste_la_tache_declaree()
    {
        await Creer(titre: "Envoyer les documents", echeance: new DateOnly(2026, 8, 1));

        await _taches.Received(1).AddAsync(
            Arg.Is<TaskItem>(tache =>
                tache != null
                && tache.HunterProfileId == _profil.Id
                && tache.Title == "Envoyer les documents"
                && tache.DueDate == new DateOnly(2026, 8, 1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Persiste_une_tache_sans_echeance()
    {
        await Creer(echeance: null);

        await _taches.Received(1).AddAsync(
            Arg.Is<TaskItem>(tache => tache != null && tache.DueDate == null),
            Arg.Any<CancellationToken>());
    }

    // L'écran a besoin de l'identifiant pour enchaîner sur la complétion sans relire la liste.
    [Fact]
    public async Task Rend_l_identifiant_de_la_tache_creee()
    {
        (await Creer()).TaskId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Rend_le_titre_reellement_enregistre()
    {
        (await Creer(titre: "  Payer le loyer  ")).Title.Should().Be("Payer le loyer");
    }

    [Fact]
    public async Task Rend_l_echeance_enregistree()
    {
        (await Creer(echeance: new DateOnly(2026, 8, 1))).DueDate
            .Should().Be(new DateOnly(2026, 8, 1));
    }

    // L'horloge est injectée pour rester figeable : la date de création départage deux tâches de
    // même échéance dans la liste, et un test qui la lirait sur DateTimeOffset.UtcNow ne pourrait
    // rien en affirmer.
    [Fact]
    public async Task Date_la_tache_de_l_horloge_injectee()
    {
        await Creer();

        await _taches.Received(1).AddAsync(
            Arg.Is<TaskItem>(tache => tache != null && tache.CreatedAt == Maintenant),
            Arg.Any<CancellationToken>());
    }

    // La clé étrangère finirait par refuser l'écriture, mais loin d'ici et dans une langue que le
    // Chasseur n'a pas à lire.
    [Fact]
    public async Task Refuse_de_declarer_une_tache_pour_un_Chasseur_inconnu()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = async () => await Creer();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
    }

    [Fact]
    public async Task N_ecrit_rien_pour_un_Chasseur_inconnu()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = async () => await Creer();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
        await _taches.DidNotReceive().AddAsync(
            Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    private sealed class HorlogeFigee : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Maintenant;
    }
}
