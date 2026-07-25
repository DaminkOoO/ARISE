using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Hunters.EventHandlers;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Hunters.EventHandlers;

/// <summary>
/// La série d'engagement du Chasseur se met à jour ici et nulle part ailleurs : peu importe quel
/// handler de commande — Sport aujourd'hui, Budget et Habitudes demain — a déclenché la
/// complétion, la logique reste à un seul endroit (doc mécaniques, section 2).
/// </summary>
public class StreakUpdateHandlerTests
{
    private static readonly DateOnly Jour = new(2026, 7, 26);

    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();

    private HunterProfile CreerProfil()
    {
        var profil = HunterProfile.Create();
        _profils.GetByIdAsync(profil.Id, Arg.Any<CancellationToken>()).Returns(profil);
        return profil;
    }

    private Task Notifier(
        Guid chasseur, DateOnly? jour = null, QuestType type = QuestType.Quotidienne) =>
        new StreakUpdateHandler(_profils).Handle(
            new DomainEventNotification<QuestCompletedEvent>(new QuestCompletedEvent(
                Guid.NewGuid(), chasseur, QuestDomain.Sport, type, jour ?? Jour)),
            CancellationToken.None);

    [Fact]
    public async Task Compte_la_completion_dans_la_serie_du_Chasseur()
    {
        var profil = CreerProfil();

        await Notifier(profil.Id);

        profil.StreakCurrent.Should().Be(1);
    }

    // La date vient de l'événement, pas de l'horloge du handler : c'est le producteur de la
    // complétion qui connaît le fuseau du Chasseur, et lui seul.
    [Fact]
    public async Task Date_la_serie_du_jour_porte_par_l_evenement()
    {
        var profil = CreerProfil();

        await Notifier(profil.Id, new DateOnly(2026, 7, 25));

        profil.LastCompletionDate.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public async Task Sauvegarde_le_profil_apres_avoir_compte_la_completion()
    {
        var profil = CreerProfil();

        await Notifier(profil.Id);

        await _profils.Received(1).SaveAsync(profil, Arg.Any<CancellationToken>());
    }

    // Une quête de pénalité compte autant qu'une quotidienne : elle est là pour redonner un
    // point d'appui après une série rompue, pas pour être décomptée.
    [Theory]
    [InlineData(QuestType.Quotidienne)]
    [InlineData(QuestType.Penalite)]
    public async Task Compte_la_completion_quel_que_soit_le_type_de_quete(QuestType type)
    {
        var profil = CreerProfil();

        await Notifier(profil.Id, type: type);

        profil.StreakCurrent.Should().Be(1);
    }

    // Compléter deux quêtes dans la même journée — celle du Sport puis celle des Habitudes —
    // ne fait pas monter la série de deux : elle compte des jours, pas des quêtes.
    [Fact]
    public async Task Ne_compte_qu_une_fois_deux_completions_du_meme_jour()
    {
        var profil = CreerProfil();

        await Notifier(profil.Id);
        await Notifier(profil.Id);

        profil.StreakCurrent.Should().Be(1);
    }

    [Fact]
    public async Task Leve_une_exception_quand_le_profil_est_introuvable()
    {
        var idInconnu = Guid.NewGuid();
        _profils.GetByIdAsync(idInconnu, Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = () => Notifier(idInconnu);

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
    }
}
