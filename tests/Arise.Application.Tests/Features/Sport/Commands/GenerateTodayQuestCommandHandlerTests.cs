using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Sport;
using Arise.Application.Features.Sport.Commands.GenerateTodayQuest;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Sport.Commands;

/// <summary>
/// L'écriture de la quête du jour, nommée et adressable. Elle était jusqu'ici enfouie dans la
/// requête de lecture : personne d'autre ne pouvait la déclencher, et le
/// <c>briefing-worker</c> de la Phase 4 aurait dû dupliquer le bloc ou provoquer une écriture
/// en émettant une lecture.
/// </summary>
public class GenerateTodayQuestCommandHandlerTests
{
    private static readonly DateOnly Jour = new(2026, 7, 26);

    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly IQuestRepository _quetes = Substitute.For<IQuestRepository>();
    private readonly IQuestGenerationAgent _agent = Substitute.For<IQuestGenerationAgent>();

    private readonly HunterProfile _profil = HunterProfile.Create();

    public GenerateTodayQuestCommandHandlerTests()
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

    private Task<Quest> Generer(DateOnly? jour = null) =>
        new GenerateTodayQuestCommandHandler(_profils, _quetes, _agent).Handle(
            new GenerateTodayQuestCommand(_profil.Id, jour ?? Jour), CancellationToken.None);

    [Fact]
    public async Task Interroge_le_Systeme_pour_ecrire_la_quete()
    {
        await Generer();

        await _agent.Received(1).ExecuteAsync(
            Arg.Any<QuestGenerationAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rend_la_quete_ecrite_par_le_Systeme()
    {
        var quete = await Generer();

        quete.Title.Should().Be("L'Épreuve du Guerrier");
    }

    [Fact]
    public async Task Persiste_la_quete_generee()
    {
        await Generer();

        await _quetes.Received(1).SaveAsync(
            Arg.Is<Quest>(quete =>
                quete != null
                && quete.HunterProfileId == _profil.Id
                && quete.Domain == QuestDomain.Sport
                && quete.Title == "L'Épreuve du Guerrier"),
            Arg.Any<CancellationToken>());
    }

    // La date vient de la commande et non d'une horloge : c'est ce qui rend l'écriture
    // réutilisable par un worker qui génère la veille au soir pour le lendemain.
    [Fact]
    public async Task Date_la_quete_du_jour_demande()
    {
        var quete = await Generer(new DateOnly(2026, 8, 3));

        quete.QuestDate.Should().Be(new DateOnly(2026, 8, 3));
    }

    [Fact]
    public async Task Transmet_le_niveau_du_Chasseur_au_Systeme()
    {
        _profil.AwardXp(520); // niveau 5, rang D

        await Generer();

        await _agent.Received(1).ExecuteAsync(
            Arg.Is<QuestGenerationAgentRequest>(requete => requete != null && requete.Level == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transmet_le_rang_du_Chasseur_au_Systeme()
    {
        _profil.AwardXp(520); // niveau 5, rang D

        await Generer();

        await _agent.Received(1).ExecuteAsync(
            Arg.Is<QuestGenerationAgentRequest>(requete => requete != null && requete.Rank == HunterRank.D),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transmet_la_serie_en_cours_du_Chasseur_au_Systeme()
    {
        _profil.RegisterDailyCompletion(new DateOnly(2026, 7, 24));
        _profil.RegisterDailyCompletion(new DateOnly(2026, 7, 25));

        await Generer();

        await _agent.Received(1).ExecuteAsync(
            Arg.Is<QuestGenerationAgentRequest>(requete => requete != null && requete.StreakCurrent == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuse_de_generer_pour_un_Chasseur_dont_le_profil_est_introuvable()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = () => Generer();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
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

        await Generer();

        await _quetes.Received(1).SaveAsync(
            Arg.Is<Quest>(quete => quete != null && quete.IsFallback),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rend_une_quete_fraichement_generee_comme_non_completee()
    {
        var quete = await Generer();

        quete.IsCompleted.Should().BeFalse();
    }
}
