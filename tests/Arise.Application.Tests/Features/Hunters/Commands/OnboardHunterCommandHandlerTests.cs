using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Hunters;
using Arise.Application.Features.Hunters.Commands.OnboardHunter;
// IHunterProfileRepository vit encore dans Common.Abstractions (générique, réutilisé par
// AwardXp) ; seul IOnboardingAgent a déménagé vers Features.Hunters, propre au domaine.
using Arise.Application.Common.Exceptions;
using Arise.Domain.Hunters;
using Arise.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Hunters.Commands;

public class OnboardHunterCommandHandlerTests
{
    private const string Narration = "Le Système t'a repéré, Chasseur. Ta voie vers le Sport commence.";

    private readonly IOnboardingAgent _agentOnboarding = Substitute.For<IOnboardingAgent>();
    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly IUserRepository _comptes = Substitute.For<IUserRepository>();

    private readonly User _compte = User.Register(
        "sung", "empreinte", new DateTimeOffset(2026, 7, 23, 11, 0, 0, TimeSpan.Zero));

    public OnboardHunterCommandHandlerTests()
    {
        _agentOnboarding
            .ExecuteAsync(Arg.Any<OnboardingAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OnboardingAgentResult(Narration, EstRepli: false));

        _comptes.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_compte);
    }

    private OnboardHunterCommandHandler Handler() => new(_agentOnboarding, _profils, _comptes);

    private Task<OnboardHunterResult> Eveiller(params HunterGoal[] objectifs) =>
        Handler().Handle(
            new OnboardHunterCommand(_compte.Id, objectifs), CancellationToken.None);

    [Fact]
    public async Task Cree_un_profil_au_niveau_de_depart()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.Level.Should().Be(1);
    }

    [Fact]
    public async Task Cree_un_profil_au_rang_E()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.Rank.Should().Be(HunterRank.E);
    }

    [Fact]
    public async Task Cree_un_profil_sans_XP()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.CurrentXp.Should().Be(0);
    }

    [Fact]
    public async Task Sauvegarde_le_profil_cree()
    {
        await Eveiller(HunterGoal.Sport);

        await _profils.Received(1).SaveAsync(
            Arg.Is<HunterProfile>(profil => profil != null && profil.Level == 1 && profil.CurrentXp == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Interroge_l_agent_d_onboarding_avec_les_objectifs_declares()
    {
        await Eveiller(HunterGoal.Budget, HunterGoal.Calendrier);

        await _agentOnboarding.Received(1).ExecuteAsync(
            Arg.Is<OnboardingAgentRequest>(requete =>
                requete != null
                && requete.Objectifs.SequenceEqual(new[] { HunterGoal.Budget, HunterGoal.Calendrier })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renvoie_la_narration_generee_par_l_agent()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.AwakeningNarrative.Should().Be(Narration);
    }

    [Fact]
    public async Task Ne_marque_pas_repli_quand_l_agent_a_reussi()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.EstRepli.Should().BeFalse();
    }

    [Fact]
    public async Task Repercute_le_repli_de_l_agent_dans_le_resultat()
    {
        _agentOnboarding
            .ExecuteAsync(Arg.Any<OnboardingAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OnboardingAgentResult("Narration de secours neutre.", EstRepli: true));

        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.EstRepli.Should().BeTrue();
    }

    // L'agent ne touche jamais aux valeurs numériques du profil (doc mécaniques, section 4) :
    // même en repli, le profil créé reste au niveau 1, rang E, 0 XP.
    [Fact]
    public async Task Cree_le_profil_par_defaut_meme_quand_l_agent_est_en_repli()
    {
        _agentOnboarding
            .ExecuteAsync(Arg.Any<OnboardingAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OnboardingAgentResult("Narration de secours neutre.", EstRepli: true));

        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.Level.Should().Be(1);
        resultat.Rank.Should().Be(HunterRank.E);
        resultat.CurrentXp.Should().Be(0);
    }

    [Fact]
    public async Task Renvoie_l_Id_du_profil_sauvegarde()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        resultat.HunterProfileId.Should().NotBeEmpty();
    }

    // --- Rattachement du profil au compte -----------------------------------------------------
    //
    // C'est cette liaison qui rend sûrs tous les endpoints protégés : sans elle, le profil visé
    // devrait être annoncé par l'appelant, et un Chasseur authentifié agirait sur les données
    // d'un autre en changeant un identifiant dans le corps de sa requête.

    [Fact]
    public async Task Rattache_le_profil_cree_au_compte_qui_s_eveille()
    {
        var resultat = await Eveiller(HunterGoal.Sport);

        _compte.HunterProfileId.Should().Be(resultat.HunterProfileId);
    }

    [Fact]
    public async Task Persiste_le_rattachement()
    {
        await Eveiller(HunterGoal.Sport);

        await _comptes.Received(1).SaveAsync(_compte, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuse_d_eveiller_un_compte_inconnu()
    {
        _comptes.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var acte = async () => await Eveiller(HunterGoal.Sport);

        await acte.Should().ThrowAsync<UserNotFoundException>();
    }

    // Un second éveil écraserait toute la progression : l'ancien profil deviendrait
    // inatteignable, XP et séries compris.
    [Fact]
    public async Task Refuse_d_eveiller_un_compte_qui_porte_deja_un_profil()
    {
        _compte.RattacherLeProfil(Guid.NewGuid());

        var acte = async () => await Eveiller(HunterGoal.Sport);

        await acte.Should().ThrowAsync<HunterAlreadyAwakenedException>();
    }

    // Un appel au Système coûte du temps et de l'argent : le refus doit précéder la dépense.
    [Fact]
    public async Task N_appelle_pas_le_Systeme_pour_un_compte_deja_eveille()
    {
        _compte.RattacherLeProfil(Guid.NewGuid());

        var acte = async () => await Eveiller(HunterGoal.Sport);

        await acte.Should().ThrowAsync<HunterAlreadyAwakenedException>();
        await _agentOnboarding.DidNotReceive().ExecuteAsync(
            Arg.Any<OnboardingAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task N_ecrit_aucun_profil_pour_un_compte_inconnu()
    {
        _comptes.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var acte = async () => await Eveiller(HunterGoal.Sport);

        await acte.Should().ThrowAsync<UserNotFoundException>();
        await _profils.DidNotReceive().SaveAsync(
            Arg.Any<HunterProfile>(), Arg.Any<CancellationToken>());
    }
}
