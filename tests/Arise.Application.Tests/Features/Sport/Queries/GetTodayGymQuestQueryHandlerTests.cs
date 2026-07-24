using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Sport;
using Arise.Application.Features.Sport.Queries.GetTodayGymQuest;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Sport.Queries;

/// <summary>
/// La quête de sport du jour : générée une seule fois par jour et par Chasseur, puis relue.
/// Le Système n'est donc rappelé qu'au premier passage de la journée — la quête que le
/// Chasseur a lue le matin est celle qu'il retrouve le soir, texte compris.
/// </summary>
public class GetTodayGymQuestQueryHandlerTests
{
    private const string FuseauParis = "Europe/Paris";

    // 22h30 UTC le 25, soit déjà le 26 à Paris (UTC+2 en été) : le piège de date que le
    // fuseau du Chasseur existe pour éviter.
    private static readonly DateTimeOffset VeilleTardUtc = new(2026, 7, 25, 22, 30, 0, TimeSpan.Zero);

    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly IQuestRepository _quetes = Substitute.For<IQuestRepository>();
    private readonly IQuestGenerationAgent _agent = Substitute.For<IQuestGenerationAgent>();

    private readonly HunterProfile _profil = HunterProfile.Create();

    public GetTodayGymQuestQueryHandlerTests()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_profil);

        _agent.ExecuteAsync(Arg.Any<QuestGenerationAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuestGenerationAgentResult(
                "L'Épreuve du Guerrier",
                "Bouge à ton rythme : marche, gainage, étirements.",
                QuestType.Quotidienne,
                QuestStat.Force,
                QuestDifficulty.Moyenne,
                20,
                EstRepli: false));
    }

    private GetTodayGymQuestQueryHandler Handler(DateTimeOffset? maintenant = null) =>
        new(_profils, _quetes, _agent, new HorlogeFigee(maintenant ?? VeilleTardUtc));

    private Task<GetTodayGymQuestResult> Demander(
        string fuseau = FuseauParis, DateTimeOffset? maintenant = null) =>
        Handler(maintenant).Handle(
            new GetTodayGymQuestQuery(_profil.Id, fuseau), CancellationToken.None);

    private Quest QueteDejaPosee(DateOnly jour, string titre = "Quête déjà posée") =>
        Quest.Generate(
            _profil.Id,
            QuestDomain.Sport,
            jour,
            titre,
            "Marche à ton rythme, Chasseur.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Facile,
            10,
            isFallback: false);

    private void PoserLaQueteDuJour(DateOnly jour, Quest quete) =>
        _quetes.GetForDayAsync(_profil.Id, QuestDomain.Sport, jour, Arg.Any<CancellationToken>())
            .Returns(quete);

    [Fact]
    public async Task Renvoie_la_quete_deja_posee_pour_aujourd_hui()
    {
        PoserLaQueteDuJour(new DateOnly(2026, 7, 26), QueteDejaPosee(new DateOnly(2026, 7, 26)));

        var resultat = await Demander();

        resultat.Title.Should().Be("Quête déjà posée");
    }

    // Une seule génération par jour : le Système coûte un appel réseau et une réponse non
    // déterministe, et le Chasseur doit retrouver le matin la quête qu'il a lue le soir.
    [Fact]
    public async Task N_interroge_pas_le_Systeme_quand_la_quete_du_jour_existe_deja()
    {
        PoserLaQueteDuJour(new DateOnly(2026, 7, 26), QueteDejaPosee(new DateOnly(2026, 7, 26)));

        await Demander();

        await _agent.DidNotReceive().ExecuteAsync(
            Arg.Any<QuestGenerationAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ne_persiste_rien_quand_la_quete_du_jour_existe_deja()
    {
        PoserLaQueteDuJour(new DateOnly(2026, 7, 26), QueteDejaPosee(new DateOnly(2026, 7, 26)));

        await Demander();

        await _quetes.DidNotReceive().SaveAsync(Arg.Any<Quest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Interroge_le_Systeme_quand_aucune_quete_n_est_posee_pour_aujourd_hui()
    {
        await Demander();

        await _agent.Received(1).ExecuteAsync(
            Arg.Any<QuestGenerationAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renvoie_le_titre_genere_par_le_Systeme()
    {
        var resultat = await Demander();

        resultat.Title.Should().Be("L'Épreuve du Guerrier");
    }

    [Fact]
    public async Task Renvoie_la_description_generee_par_le_Systeme()
    {
        var resultat = await Demander();

        resultat.Description.Should().Be("Bouge à ton rythme : marche, gainage, étirements.");
    }

    [Fact]
    public async Task Renvoie_la_recompense_generee_par_le_Systeme()
    {
        var resultat = await Demander();

        resultat.XpReward.Should().Be(20);
    }

    [Fact]
    public async Task Renvoie_la_statistique_visee_par_la_quete()
    {
        var resultat = await Demander();

        resultat.StatTarget.Should().Be(QuestStat.Force);
    }

    [Fact]
    public async Task Persiste_la_quete_generee()
    {
        await Demander();

        await _quetes.Received(1).SaveAsync(
            Arg.Is<Quest>(quete =>
                quete != null
                && quete.HunterProfileId == _profil.Id
                && quete.Domain == QuestDomain.Sport
                && quete.Title == "L'Épreuve du Guerrier"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renvoie_l_identifiant_de_la_quete_persistee()
    {
        var resultat = await Demander();

        resultat.QuestId.Should().NotBeEmpty();
    }

    // Le jour est celui du Chasseur, pas celui du serveur : à 22h30 UTC le 25, il est déjà le
    // 26 à Paris, et la quête du 26 est celle qu'il faut chercher comme celle qu'il faut poser.
    [Fact]
    public async Task Cherche_la_quete_a_la_date_du_fuseau_du_Chasseur()
    {
        await Demander();

        await _quetes.Received(1).GetForDayAsync(
            _profil.Id, QuestDomain.Sport, new DateOnly(2026, 7, 26), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Date_la_quete_generee_du_jour_du_fuseau_du_Chasseur()
    {
        var resultat = await Demander();

        resultat.QuestDate.Should().Be(new DateOnly(2026, 7, 26));
    }

    // Le même instant, lu depuis un fuseau en retard sur UTC, tombe la veille : la date ne se
    // déduit jamais de l'horloge du serveur seule.
    [Fact]
    public async Task Date_la_quete_de_la_veille_pour_un_Chasseur_dans_un_fuseau_en_retard()
    {
        var resultat = await Demander("America/New_York");

        resultat.QuestDate.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public async Task Transmet_le_niveau_du_Chasseur_au_Systeme()
    {
        _profil.AwardXp(520); // niveau 5, rang D

        await Demander();

        await _agent.Received(1).ExecuteAsync(
            Arg.Is<QuestGenerationAgentRequest>(requete => requete != null && requete.Level == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transmet_le_rang_du_Chasseur_au_Systeme()
    {
        _profil.AwardXp(520); // niveau 5, rang D

        await Demander();

        await _agent.Received(1).ExecuteAsync(
            Arg.Is<QuestGenerationAgentRequest>(requete => requete != null && requete.Rank == HunterRank.D),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transmet_la_serie_en_cours_du_Chasseur_au_Systeme()
    {
        _profil.RegisterDailyCompletion(new DateOnly(2026, 7, 24));
        _profil.RegisterDailyCompletion(new DateOnly(2026, 7, 25));

        await Demander();

        await _agent.Received(1).ExecuteAsync(
            Arg.Is<QuestGenerationAgentRequest>(requete => requete != null && requete.StreakCurrent == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuse_de_generer_pour_un_Chasseur_dont_le_profil_est_introuvable()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = () => Demander();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
    }

    [Fact]
    public async Task Ne_marque_pas_repli_une_quete_reellement_generee()
    {
        var resultat = await Demander();

        resultat.EstRepli.Should().BeFalse();
    }

    // Le repli de l'agent traverse jusqu'au résultat : l'écran peut ainsi, s'il le souhaite un
    // jour, adapter son habillage « Système » à une réponse dégradée.
    [Fact]
    public async Task Repercute_le_repli_du_Systeme_dans_le_resultat()
    {
        _agent.ExecuteAsync(Arg.Any<QuestGenerationAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuestGenerationAgentResult(
                "Éveil du Corps",
                "Bouge à ton rythme aujourd'hui, Chasseur.",
                QuestType.Quotidienne,
                QuestStat.Force,
                QuestDifficulty.Facile,
                10,
                EstRepli: true));

        var resultat = await Demander();

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Persiste_le_repli_comme_tel()
    {
        _agent.ExecuteAsync(Arg.Any<QuestGenerationAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuestGenerationAgentResult(
                "Éveil du Corps",
                "Bouge à ton rythme aujourd'hui, Chasseur.",
                QuestType.Quotidienne,
                QuestStat.Force,
                QuestDifficulty.Facile,
                10,
                EstRepli: true));

        await Demander();

        await _quetes.Received(1).SaveAsync(
            Arg.Is<Quest>(quete => quete != null && quete.IsFallback),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renvoie_une_quete_fraichement_generee_comme_non_completee()
    {
        var resultat = await Demander();

        resultat.IsCompleted.Should().BeFalse();
    }

    private sealed class HorlogeFigee(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
