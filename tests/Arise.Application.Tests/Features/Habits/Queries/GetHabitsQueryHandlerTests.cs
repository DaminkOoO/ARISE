using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Habits.Queries.GetHabits;
using Arise.Domain.Habits;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Habits.Queries;

/// <summary>
/// La liste d'habitudes de l'écran Habitudes. Elle ne montre que ce que le Chasseur suit
/// aujourd'hui : une habitude rangée quitte la liste sans disparaître de la base, et sans qu'on
/// la lui rappelle.
/// </summary>
public class GetHabitsQueryHandlerTests
{
    private static readonly Guid Chasseur = Guid.NewGuid();

    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly IHabitRepository _habitudes = Substitute.For<IHabitRepository>();

    private static Habit Habitude(
        string nom = "Boire deux litres d'eau",
        HabitFrequency frequence = HabitFrequency.Quotidienne,
        DateTimeOffset? creation = null,
        bool archivee = false)
    {
        var habitude = Habit.Create(Chasseur, nom, frequence, creation ?? Creation);

        if (archivee)
        {
            habitude.Archive();
        }

        return habitude;
    }

    private void Declarees(params Habit[] habitudes) =>
        _habitudes.GetForHunterAsync(Chasseur, Arg.Any<CancellationToken>())
            .Returns(habitudes);

    private Task<IReadOnlyList<HabitSummary>> Lister() =>
        new GetHabitsQueryHandler(_habitudes).Handle(
            new GetHabitsQuery(Chasseur), CancellationToken.None);

    [Fact]
    public async Task Rend_les_habitudes_declarees_par_le_Chasseur()
    {
        var habitude = Habitude();
        Declarees(habitude);

        var listees = await Lister();

        listees.Should().ContainSingle().Which.HabitId.Should().Be(habitude.Id);
    }

    [Fact]
    public async Task Rend_le_nom_de_chaque_habitude()
    {
        Declarees(Habitude(nom: "Lire vingt minutes"));

        var listees = await Lister();

        listees.Should().ContainSingle().Which.Name.Should().Be("Lire vingt minutes");
    }

    [Fact]
    public async Task Rend_le_rythme_de_chaque_habitude()
    {
        Declarees(Habitude(frequence: HabitFrequency.Hebdomadaire));

        var listees = await Lister();

        listees.Should().ContainSingle()
            .Which.Frequency.Should().Be(HabitFrequency.Hebdomadaire);
    }

    // Une habitude rangée est une intention qui a changé, pas un échec : elle quitte la liste
    // du jour sans que le Système la ressorte pour le lui rappeler.
    [Fact]
    public async Task Ecarte_les_habitudes_archivees()
    {
        Declarees(
            Habitude(nom: "Étirements du soir"),
            Habitude(nom: "Courir le matin", archivee: true));

        var listees = await Lister();

        listees.Should().ContainSingle().Which.Name.Should().Be("Étirements du soir");
    }

    [Fact]
    public async Task Rend_une_liste_vide_quand_le_Chasseur_n_a_rien_declare()
    {
        Declarees();

        (await Lister()).Should().BeEmpty();
    }

    // Sans ordre explicite, la liste dépend de l'ordre des lignes rendu par PostgreSQL — elle se
    // réarrangerait d'un rafraîchissement à l'autre sous les doigts du Chasseur.
    [Fact]
    public async Task Ordonne_les_habitudes_de_la_plus_ancienne_a_la_plus_recente()
    {
        Declarees(
            Habitude(nom: "Déclarée ensuite", creation: Creation.AddDays(1)),
            Habitude(nom: "Déclarée d'abord", creation: Creation));

        var listees = await Lister();

        listees.Select(habitude => habitude.Name)
            .Should().ContainInOrder("Déclarée d'abord", "Déclarée ensuite");
    }

    // Deux habitudes déclarées au même instant — un seed, un import — laisseraient sinon l'ordre
    // à la base : le nom tranche, et l'écran reste stable.
    [Fact]
    public async Task Departage_par_le_nom_deux_habitudes_declarees_au_meme_instant()
    {
        Declarees(
            Habitude(nom: "Zéro sucre"),
            Habitude(nom: "Aérer la chambre"));

        var listees = await Lister();

        listees.Select(habitude => habitude.Name)
            .Should().ContainInOrder("Aérer la chambre", "Zéro sucre");
    }

    [Fact]
    public async Task Interroge_le_repository_sur_le_Chasseur_demande()
    {
        Declarees();

        await Lister();

        await _habitudes.Received(1).GetForHunterAsync(
            Chasseur, Arg.Any<CancellationToken>());
    }
}
