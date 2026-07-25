using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Sport.Commands.GenerateTodayQuest;
using Arise.Application.Features.Sport.Queries.GetTodayGymQuest;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Arise.Application.Tests.Features.Sport.Queries;

/// <summary>
/// La quête de sport du jour : lue si elle est déjà posée, sinon demandée à
/// <see cref="GenerateTodayQuestCommand"/>. La requête ne rédige donc plus rien elle-même — elle
/// décide seulement du jour du Chasseur et de la nécessité d'une génération.
/// </summary>
public class GetTodayGymQuestQueryHandlerTests
{
    private const string FuseauParis = "Europe/Paris";

    // 22h30 UTC le 25, soit déjà le 26 à Paris (UTC+2 en été) : le piège de date que le
    // fuseau du Chasseur existe pour éviter.
    private static readonly DateTimeOffset VeilleTardUtc = new(2026, 7, 25, 22, 30, 0, TimeSpan.Zero);

    private readonly IQuestRepository _quetes = Substitute.For<IQuestRepository>();
    private readonly ISender _envoi = Substitute.For<ISender>();

    private readonly HunterProfile _profil = HunterProfile.Create();

    public GetTodayGymQuestQueryHandlerTests()
    {
        _envoi.Send(Arg.Any<GenerateTodayQuestCommand>(), Arg.Any<CancellationToken>())
            .Returns(QueteGeneree(new DateOnly(2026, 7, 26)));
    }

    private Quest QueteGeneree(DateOnly jour) => Quest.Generate(
        _profil.Id,
        QuestDomain.Sport,
        jour,
        "L'Épreuve du Guerrier",
        "Bouge à ton rythme : marche, gainage, étirements.",
        QuestType.Quotidienne,
        QuestStat.Force,
        QuestDifficulty.Moyenne,
        20,
        isFallback: false);

    private GetTodayGymQuestQueryHandler Handler(DateTimeOffset? maintenant = null) =>
        new(_quetes, _envoi, new HorlogeFigee(maintenant ?? VeilleTardUtc));

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
    // déterministe, et le Chasseur doit retrouver le soir la quête qu'il a lue le matin.
    [Fact]
    public async Task Ne_demande_aucune_generation_quand_la_quete_du_jour_existe_deja()
    {
        PoserLaQueteDuJour(new DateOnly(2026, 7, 26), QueteDejaPosee(new DateOnly(2026, 7, 26)));

        await Demander();

        await _envoi.DidNotReceive().Send(
            Arg.Any<GenerateTodayQuestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Demande_la_generation_quand_aucune_quete_n_est_posee_pour_aujourd_hui()
    {
        await Demander();

        await _envoi.Received(1).Send(
            Arg.Any<GenerateTodayQuestCommand>(), Arg.Any<CancellationToken>());
    }

    // Passer par MediatR plutôt qu'appeler le handler d'écriture en direct : la commande garde
    // ainsi sa validation et son pipeline.
    [Fact]
    public async Task Cible_le_Chasseur_de_la_requete_dans_la_commande_de_generation()
    {
        await Demander();

        await _envoi.Received(1).Send(
            Arg.Is<GenerateTodayQuestCommand>(commande =>
                commande != null && commande.HunterProfileId == _profil.Id),
            Arg.Any<CancellationToken>());
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
    public async Task Fait_generer_a_la_date_du_fuseau_du_Chasseur()
    {
        await Demander();

        await _envoi.Received(1).Send(
            Arg.Is<GenerateTodayQuestCommand>(commande =>
                commande != null && commande.QuestDate == new DateOnly(2026, 7, 26)),
            Arg.Any<CancellationToken>());
    }

    // Le même instant, lu depuis un fuseau en retard sur UTC, tombe la veille : la date ne se
    // déduit jamais de l'horloge du serveur seule.
    [Fact]
    public async Task Fait_generer_la_veille_pour_un_Chasseur_dans_un_fuseau_en_retard()
    {
        await Demander("America/New_York");

        await _envoi.Received(1).Send(
            Arg.Is<GenerateTodayQuestCommand>(commande =>
                commande != null && commande.QuestDate == new DateOnly(2026, 7, 25)),
            Arg.Any<CancellationToken>());
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
        _envoi.Send(Arg.Any<GenerateTodayQuestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Quest.Generate(
                _profil.Id,
                QuestDomain.Sport,
                new DateOnly(2026, 7, 26),
                "Éveil du Corps",
                "Bouge à ton rythme aujourd'hui, Chasseur.",
                QuestType.Quotidienne,
                QuestStat.Force,
                QuestDifficulty.Facile,
                10,
                isFallback: true));

        var resultat = await Demander();

        resultat.EstRepli.Should().BeTrue();
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
