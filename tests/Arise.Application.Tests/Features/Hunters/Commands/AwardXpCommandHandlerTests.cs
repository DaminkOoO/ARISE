using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Hunters.Commands.AwardXp;
using Arise.Domain.Hunters;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Arise.Application.Tests.Features.Hunters.Commands;

public class AwardXpCommandHandlerTests
{
    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private HunterProfile CreerProfil()
    {
        var profil = HunterProfile.Create();
        _profils.GetByIdAsync(profil.Id, Arg.Any<CancellationToken>()).Returns(profil);
        return profil;
    }

    private AwardXpCommandHandler Handler() => new(_profils, _publisher);

    /// <summary>
    /// L'enveloppe attendue, typée <see cref="INotification"/> comme au site d'appel : le
    /// handler ne connaît que des <c>IDomainEvent</c> et publie ce que la fabrique lui rend.
    /// Typer l'attente sur le générique refermé viserait une autre surcharge de
    /// <see cref="IPublisher.Publish{T}"/>, que le handler n'appelle jamais.
    /// </summary>
    private static INotification Enveloppe(HunterRankedUpEvent evenement) =>
        new DomainEventNotification<HunterRankedUpEvent>(evenement);

    private Task<AwardXpResult> Attribuer(Guid hunterProfileId, int montant) =>
        Handler().Handle(new AwardXpCommand(hunterProfileId, montant), CancellationToken.None);

    [Fact]
    public async Task Applique_le_montant_d_XP_au_profil()
    {
        var profil = CreerProfil();

        await Attribuer(profil.Id, montant: 50);

        profil.CurrentXp.Should().Be(50);
    }

    [Fact]
    public async Task Sauvegarde_le_profil_apres_attribution()
    {
        var profil = CreerProfil();

        await Attribuer(profil.Id, montant: 50);

        await _profils.Received(1).SaveAsync(profil, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renvoie_l_etat_du_profil_apres_attribution()
    {
        var profil = CreerProfil();

        var resultat = await Attribuer(profil.Id, montant: 50);

        resultat.Should().Be(new AwardXpResult(
            profil.Id, profil.Level, profil.Rank, profil.CurrentXp, profil.XpToNextLevel));
    }

    // Arg.Any<INotification>() et non Arg.Any<object>() : la surcharge non générique de
    // IPublisher n'est jamais celle qu'emprunte le handler, et l'attente y était donc vide de
    // sens — elle serait restée verte même si le handler publiait à tort.
    [Fact]
    public async Task Ne_publie_aucun_evenement_quand_aucun_rang_n_est_franchi()
    {
        var profil = CreerProfil();

        await Attribuer(profil.Id, montant: 50);

        await _publisher.DidNotReceive().Publish(
            Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    // 520 XP amène pile du niveau 1 (rang E) au niveau 5 (rang D) : une seule frontière de
    // rang franchie, un seul événement attendu.
    [Fact]
    public async Task Publie_un_evenement_quand_un_rang_est_franchi()
    {
        var profil = CreerProfil();

        await Attribuer(profil.Id, montant: 520);

        await _publisher.Received(1).Publish(
            Enveloppe(new HunterRankedUpEvent(profil.Id, HunterRank.E, HunterRank.D)),
            Arg.Any<CancellationToken>());
    }

    // 1620 XP amène pile du niveau 1 au niveau 10 : deux frontières de rang franchies
    // (E -> D au niveau 5, D -> C au niveau 10). C'est exactement le cas que la revue
    // précédente a signalé : le handler ne doit pas coalescer les deux en un seul événement.
    [Fact]
    public async Task Publie_un_evenement_par_rang_franchi_quand_plusieurs_sont_traverses_d_un_coup()
    {
        var profil = CreerProfil();

        await Attribuer(profil.Id, montant: 1620);

        await _publisher.Received(1).Publish(
            Enveloppe(new HunterRankedUpEvent(profil.Id, HunterRank.E, HunterRank.D)),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(
            Enveloppe(new HunterRankedUpEvent(profil.Id, HunterRank.D, HunterRank.C)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Vide_les_evenements_du_profil_apres_les_avoir_publies()
    {
        var profil = CreerProfil();

        await Attribuer(profil.Id, montant: 1620);

        profil.DomainEvents.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------
    // Deux attributions simultanées. Le repository refuse l'écriture perdante et rafraîchit le
    // profil avec l'état gagnant ; le handler rejoue alors son attribution par-dessus, au lieu
    // de la perdre. Sans ce rejeu, le Chasseur qui gagne 20 XP par deux chemins au même instant
    // n'en verrait que 20 — ou, pire depuis que la base refuse, une erreur après une quête déjà
    // marquée accomplie.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Fait échouer les <paramref name="echecs"/> premières écritures comme le fait le jeton de
    /// concurrence. Le profil garde l'XP que la tentative perdue lui a appliqué : c'est aussi
    /// exactement ce que le rafraîchissement du repository y remettrait, puisque le gagnant a
    /// écrit le même montant.
    /// </summary>
    private void EcrituresPerduesAvantDePasser(HunterProfile profil, int echecs)
    {
        var restants = echecs;

        _profils.When(repository => repository.SaveAsync(profil, Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                if (restants-- > 0)
                {
                    throw new ConcurrentHunterProfileUpdateException();
                }
            });
    }

    [Fact]
    public async Task Rejoue_l_attribution_quand_une_attribution_simultanee_a_gagne()
    {
        var profil = CreerProfil();
        EcrituresPerduesAvantDePasser(profil, echecs: 1);

        await Attribuer(profil.Id, montant: 20);

        profil.CurrentXp.Should().Be(40);
    }

    [Fact]
    public async Task Rend_l_etat_du_profil_rejoue()
    {
        var profil = CreerProfil();
        EcrituresPerduesAvantDePasser(profil, echecs: 1);

        var resultat = await Attribuer(profil.Id, montant: 20);

        resultat.CurrentXp.Should().Be(40);
    }

    // Le rejeu est borné : une base durablement contendue ne doit pas faire tourner un handler
    // indéfiniment. Passé les tentatives prévues, l'échec remonte.
    [Fact]
    public async Task Abandonne_apres_trois_tentatives_perdues()
    {
        var profil = CreerProfil();
        EcrituresPerduesAvantDePasser(profil, echecs: int.MaxValue);

        var acte = () => Attribuer(profil.Id, montant: 20);

        await acte.Should().ThrowAsync<ConcurrentHunterProfileUpdateException>();
        await _profils.Received(3).SaveAsync(profil, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leve_une_exception_quand_le_profil_est_introuvable()
    {
        var idInconnu = Guid.NewGuid();
        _profils.GetByIdAsync(idInconnu, Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = () => Attribuer(idInconnu, montant: 50);

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
    }

    [Fact]
    public async Task Ne_publie_rien_quand_le_profil_est_introuvable()
    {
        var idInconnu = Guid.NewGuid();
        _profils.GetByIdAsync(idInconnu, Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = () => Attribuer(idInconnu, montant: 50);

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
        await _publisher.DidNotReceive().Publish(
            Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }
}
