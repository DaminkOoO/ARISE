using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Hunters.Commands.AwardXp;
using Arise.Application.Features.Sport.Commands.CompleteGymQuest;
using Arise.Domain.Quests;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Arise.Application.Tests.Features.Sport.Commands;

/// <summary>
/// La complétion d'une quête de sport : elle marque la quête, la persiste, puis fait accorder
/// l'XP par <see cref="AwardXpCommand"/> et laisse la série se mettre à jour sur l'événement de
/// complétion. Le barème vit sur la quête — le handler ne le recalcule jamais.
/// </summary>
public class CompleteGymQuestCommandHandlerTests
{
    private const string FuseauNewYork = "America/New_York";

    // 03h30 UTC le 26, soit encore 23h30 le 25 à New York : le piège de date de la série. La
    // journée que le Chasseur vient de tenir est le 25, pas celle du serveur.
    private static readonly DateTimeOffset TardLeVingtCinqANewYork =
        new(2026, 7, 26, 3, 30, 0, TimeSpan.Zero);

    private readonly IQuestRepository _quetes = Substitute.For<IQuestRepository>();
    private readonly ISender _envoi = Substitute.For<ISender>();
    private readonly IPublisher _publication = Substitute.For<IPublisher>();

    private readonly Guid _chasseur = Guid.NewGuid();

    private Quest QuetePosee(int xp = 20)
    {
        var quete = Quest.Generate(
            _chasseur,
            QuestDomain.Sport,
            new DateOnly(2026, 7, 25),
            "L'Épreuve du Guerrier",
            "Bouge à ton rythme : marche, gainage, étirements.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Moyenne,
            xp,
            isFallback: false);

        _quetes.GetByIdAsync(quete.Id, Arg.Any<CancellationToken>()).Returns(quete);

        return quete;
    }

    private Task<CompleteGymQuestResult> Completer(
        Guid queteId,
        Guid? chasseur = null,
        string fuseau = FuseauNewYork,
        DateTimeOffset? maintenant = null) =>
        new CompleteGymQuestCommandHandler(
            _quetes,
            _envoi,
            _publication,
            new HorlogeFigee(maintenant ?? TardLeVingtCinqANewYork))
            .Handle(
                new CompleteGymQuestCommand(chasseur ?? _chasseur, queteId, fuseau),
                CancellationToken.None);

    [Fact]
    public async Task Marque_la_quete_comme_completee()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        quete.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Persiste_la_quete_completee()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        await _quetes.Received(1).SaveAsync(quete, Arg.Any<CancellationToken>());
    }

    // Le barème vit sur la quête, posé à sa génération : le handler ne le recalcule pas, sans
    // quoi la récompense affichée le matin ne serait plus celle accordée le soir.
    [Fact]
    public async Task Fait_accorder_l_XP_annonce_par_la_quete()
    {
        var quete = QuetePosee(xp: 25);

        await Completer(quete.Id);

        await _envoi.Received(1).Send(
            Arg.Is<AwardXpCommand>(commande =>
                commande != null
                && commande.HunterProfileId == _chasseur
                && commande.Montant == 25),
            Arg.Any<CancellationToken>());
    }

    // La complétion est persistée avant que l'XP ne soit accordé : si le processus tombait
    // entre les deux, le Chasseur perdrait un gain — pas la garde qui l'empêche d'être accordé
    // deux fois à la reprise. Des deux pannes, c'est la seule qui se rattrape.
    [Fact]
    public async Task Persiste_la_completion_avant_de_faire_accorder_l_XP()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        Received.InOrder(() =>
        {
            _quetes.SaveAsync(quete, Arg.Any<CancellationToken>());
            _envoi.Send(Arg.Any<AwardXpCommand>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Publie_l_evenement_de_completion()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        await _publication.Received(1).Publish(
            Arg.Is<INotification>(notification =>
                notification is DomainEventNotification<QuestCompletedEvent>),
            Arg.Any<CancellationToken>());
    }

    // Le piège de date que la série paierait : à 23h30 à New York, le serveur est déjà le
    // lendemain en UTC. Compter cette complétion pour le 26 volerait au Chasseur la journée
    // qu'il vient de tenir, et romprait sa série au passage.
    [Fact]
    public async Task Date_la_completion_du_jour_du_Chasseur_et_non_de_celui_du_serveur()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        CompletionPubliee().JourDuChasseur.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public async Task Date_la_completion_du_lendemain_pour_un_Chasseur_dont_le_jour_a_deja_tourne()
    {
        var quete = QuetePosee();

        await Completer(quete.Id, fuseau: "Europe/Paris");

        CompletionPubliee().JourDuChasseur.Should().Be(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public async Task Rattache_l_evenement_publie_au_Chasseur_et_a_sa_quete()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        CompletionPubliee().Should().BeEquivalentTo(new
        {
            QuestId = quete.Id,
            HunterProfileId = _chasseur,
        });
    }

    [Fact]
    public async Task Vide_les_evenements_de_la_quete_apres_les_avoir_publies()
    {
        var quete = QuetePosee();

        await Completer(quete.Id);

        quete.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Rend_l_XP_gagne_par_la_complétion()
    {
        var quete = QuetePosee(xp: 25);

        var resultat = await Completer(quete.Id);

        resultat.XpGagne.Should().Be(25);
    }

    [Fact]
    public async Task Rend_l_instant_de_completion()
    {
        var quete = QuetePosee();

        var resultat = await Completer(quete.Id);

        resultat.CompletedAt.Should().Be(TardLeVingtCinqANewYork);
    }

    [Fact]
    public async Task N_annonce_pas_une_quete_fraichement_completee_comme_deja_faite()
    {
        var quete = QuetePosee();

        var resultat = await Completer(quete.Id);

        resultat.DejaCompletee.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // Seconde complétion. Le risque n°1 de cette commande : double-tap sur le bouton, renvoi
    // réseau du client, deux appareils. L'accomplissement tient, l'XP ne se rejoue pas.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Ne_fait_pas_accorder_l_XP_deux_fois_pour_la_meme_quete()
    {
        var quete = QuetePosee();
        await Completer(quete.Id);

        await Completer(quete.Id);

        await _envoi.Received(1).Send(Arg.Any<AwardXpCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ne_publie_pas_deux_fois_la_completion_de_la_meme_quete()
    {
        var quete = QuetePosee();
        await Completer(quete.Id);

        await Completer(quete.Id);

        await _publication.Received(1).Publish(
            Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Annonce_une_quete_deja_accomplie_sans_la_refuser()
    {
        var quete = QuetePosee();
        await Completer(quete.Id);

        var resultat = await Completer(quete.Id);

        resultat.DejaCompletee.Should().BeTrue();
    }

    // Zéro parce que l'XP a déjà été accordé, pas parce qu'il est refusé : le Chasseur qui a
    // tapé deux fois n'a rien fait de mal.
    [Fact]
    public async Task Ne_recompte_aucun_XP_pour_une_quete_deja_accomplie()
    {
        var quete = QuetePosee();
        await Completer(quete.Id);

        var resultat = await Completer(quete.Id);

        resultat.XpGagne.Should().Be(0);
    }

    [Fact]
    public async Task Conserve_l_instant_de_la_premiere_completion()
    {
        var quete = QuetePosee();
        await Completer(quete.Id);

        var resultat = await Completer(
            quete.Id, maintenant: TardLeVingtCinqANewYork.AddHours(2));

        resultat.CompletedAt.Should().Be(TardLeVingtCinqANewYork);
    }

    // ---------------------------------------------------------------------------------------
    // Quête inconnue, ou quête d'un autre Chasseur.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Leve_une_exception_quand_la_quete_est_introuvable()
    {
        var acte = () => Completer(Guid.NewGuid());

        await acte.Should().ThrowAsync<QuestNotFoundException>();
    }

    // Sans ce contrôle, n'importe quel Chasseur complèterait les quêtes des autres — et
    // s'accorderait leur XP. Le rattachement annoncé par la commande n'est pas une simple
    // étiquette de routage.
    [Fact]
    public async Task Refuse_de_completer_la_quete_d_un_autre_Chasseur()
    {
        var quete = QuetePosee();

        var acte = () => Completer(quete.Id, chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<QuestNotFoundException>();
    }

    [Fact]
    public async Task Ne_marque_pas_completee_la_quete_d_un_autre_Chasseur()
    {
        var quete = QuetePosee();

        var acte = () => Completer(quete.Id, chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<QuestNotFoundException>();
        quete.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task N_accorde_aucun_XP_pour_la_quete_d_un_autre_Chasseur()
    {
        var quete = QuetePosee();

        var acte = () => Completer(quete.Id, chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<QuestNotFoundException>();
        await _envoi.DidNotReceive().Send(Arg.Any<AwardXpCommand>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Le fait de complétion réellement publié, extrait de l'enveloppe MediatR.
    /// </summary>
    private QuestCompletedEvent CompletionPubliee() =>
        _publication.ReceivedCalls()
            .Select(appel => appel.GetArguments()[0])
            .OfType<DomainEventNotification<QuestCompletedEvent>>()
            .Should().ContainSingle().Which.DomainEvent;

    private sealed class HorlogeFigee(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
