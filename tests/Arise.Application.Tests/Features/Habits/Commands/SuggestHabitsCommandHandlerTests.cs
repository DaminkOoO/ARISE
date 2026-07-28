using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Habits;
using Arise.Application.Features.Habits.Commands.SuggestHabits;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Habits.Commands;

/// <summary>
/// La demande de suggestions d'habitudes au Système.
///
/// <para>Elle n'écrit rien : le Chasseur choisit ensuite ce qu'il retient, et c'est
/// <c>CreateHabitCommand</c> qui déclare. Une commande qui créerait les habitudes d'office
/// remplirait sa liste de choses qu'il n'a pas voulues.</para>
/// </summary>
public class SuggestHabitsCommandHandlerTests
{
    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly IHabitRepository _habitudes = Substitute.For<IHabitRepository>();
    private readonly IHabitSuggestionAgent _systeme = Substitute.For<IHabitSuggestionAgent>();

    private readonly HunterProfile _profil = HunterProfile.Create();

    public SuggestHabitsCommandHandlerTests()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_profil);
        _habitudes.GetForHunterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        Propose(new HabitSuggestion("Boire deux litres d'eau", HabitFrequency.Quotidienne));
    }

    private void Propose(params HabitSuggestion[] suggestions) =>
        _systeme.ExecuteAsync(
                Arg.Any<HabitSuggestionAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HabitSuggestionAgentResult(suggestions, EstRepli: false));

    private void DejaDeclarees(params Habit[] habitudes) =>
        _habitudes.GetForHunterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(habitudes);

    private Habit Habitude(string nom, bool archivee = false)
    {
        var habitude = Habit.Create(
            _profil.Id, nom, HabitFrequency.Quotidienne, Creation);

        if (archivee)
        {
            habitude.Archive();
        }

        return habitude;
    }

    private Task<SuggestHabitsResult> Suggerer(Guid? chasseur = null) =>
        new SuggestHabitsCommandHandler(_profils, _habitudes, _systeme).Handle(
            new SuggestHabitsCommand(chasseur ?? _profil.Id), CancellationToken.None);

    // Le contexte transmis est celui que le dépôt connaît réellement du Chasseur : sans lui, le
    // Système proposerait la même liste à un débutant et à un Chasseur de rang S.
    [Fact]
    public async Task Donne_au_Systeme_le_niveau_et_le_rang_du_Chasseur()
    {
        await Suggerer();

        await _systeme.Received(1).ExecuteAsync(
            Arg.Is<HabitSuggestionAgentRequest>(demande =>
                demande != null
                && demande.Level == _profil.Level
                && demande.Rank == _profil.Rank),
            Arg.Any<CancellationToken>());
    }

    // Sans cette liste, le Système reproposerait ce que le Chasseur suit déjà — la suggestion la
    // plus inutile qui soit.
    [Fact]
    public async Task Donne_au_Systeme_les_habitudes_deja_suivies()
    {
        DejaDeclarees(Habitude("Courir le matin"));

        await Suggerer();

        await _systeme.Received(1).ExecuteAsync(
            Arg.Is<HabitSuggestionAgentRequest>(demande =>
                demande != null && demande.HabitudesExistantes.Contains("Courir le matin")),
            Arg.Any<CancellationToken>());
    }

    // Une habitude rangée n'est plus suivie : la reproposer est justement le service attendu.
    [Fact]
    public async Task Ne_donne_pas_au_Systeme_les_habitudes_rangees()
    {
        DejaDeclarees(Habitude("Méditer cinq minutes", archivee: true));

        await Suggerer();

        await _systeme.Received(1).ExecuteAsync(
            Arg.Is<HabitSuggestionAgentRequest>(demande =>
                demande != null && demande.HabitudesExistantes.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rend_les_suggestions_du_Systeme()
    {
        Propose(new HabitSuggestion("Lire vingt minutes", HabitFrequency.Quotidienne));

        var resultat = await Suggerer();

        resultat.Suggestions.Should().ContainSingle()
            .Which.Name.Should().Be("Lire vingt minutes");
    }

    [Fact]
    public async Task Rend_le_rythme_de_chaque_suggestion()
    {
        Propose(new HabitSuggestion("Séance longue", HabitFrequency.Hebdomadaire));

        (await Suggerer()).Suggestions.Should().ContainSingle()
            .Which.Frequency.Should().Be(HabitFrequency.Hebdomadaire);
    }

    // Le prompt le demande, mais un garde-fou écrit uniquement dans le prompt n'en est pas un :
    // le modèle repropose parfois ce qu'on vient de lui interdire, et la déclaration échouerait
    // alors sur l'unicité — après que le Chasseur a tapé dessus.
    [Fact]
    public async Task Ecarte_une_suggestion_deja_suivie_par_le_Chasseur()
    {
        DejaDeclarees(Habitude("Courir le matin"));
        Propose(
            new HabitSuggestion("Courir le matin", HabitFrequency.Quotidienne),
            new HabitSuggestion("Lire vingt minutes", HabitFrequency.Quotidienne));

        var resultat = await Suggerer();

        resultat.Suggestions.Should().ContainSingle()
            .Which.Name.Should().Be("Lire vingt minutes");
    }

    // Même insensibilité à la casse que l'index unique des habitudes : sans elle, « courir le
    // matin » passerait le filtre et heurterait la contrainte à la déclaration.
    [Fact]
    public async Task Ecarte_une_suggestion_deja_suivie_sans_distinction_de_casse()
    {
        DejaDeclarees(Habitude("Courir le matin"));
        Propose(new HabitSuggestion("courir LE matin", HabitFrequency.Quotidienne));

        (await Suggerer()).Suggestions.Should().BeEmpty();
    }

    // Mais les accents restent distinctifs, comme dans la collation du contexte : « Méditer » et
    // « Mediter » sont deux noms différents, et écarter le second priverait le Chasseur d'une
    // suggestion que la base accepterait.
    [Fact]
    public async Task Garde_une_suggestion_qui_ne_differe_que_par_un_accent()
    {
        DejaDeclarees(Habitude("Méditer"));
        Propose(new HabitSuggestion("Mediter", HabitFrequency.Quotidienne));

        (await Suggerer()).Suggestions.Should().ContainSingle();
    }

    [Fact]
    public async Task Ecarte_les_doublons_au_sein_d_une_meme_reponse()
    {
        Propose(
            new HabitSuggestion("Lire vingt minutes", HabitFrequency.Quotidienne),
            new HabitSuggestion("lire vingt minutes", HabitFrequency.Hebdomadaire));

        (await Suggerer()).Suggestions.Should().ContainSingle();
    }

    // L'écran doit pouvoir dire « le Système n'a rien de personnalisé aujourd'hui » plutôt que de
    // faire passer un repli générique pour une suggestion sur mesure.
    [Fact]
    public async Task Rend_le_drapeau_de_repli_du_Systeme()
    {
        _systeme.ExecuteAsync(
                Arg.Any<HabitSuggestionAgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HabitSuggestionAgentResult([], EstRepli: true));

        (await Suggerer()).EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Refuse_de_suggerer_pour_un_Chasseur_inconnu()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = async () => await Suggerer();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
    }

    // Un appel au Système coûte du temps et de l'argent : le refuser au plus tôt n'est pas une
    // optimisation gratuite.
    [Fact]
    public async Task N_appelle_pas_le_Systeme_pour_un_Chasseur_inconnu()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = async () => await Suggerer();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
        await _systeme.DidNotReceive().ExecuteAsync(
            Arg.Any<HabitSuggestionAgentRequest>(), Arg.Any<CancellationToken>());
    }
}
