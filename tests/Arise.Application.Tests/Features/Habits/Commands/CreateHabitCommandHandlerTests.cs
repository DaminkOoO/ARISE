using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Habits.Commands.CreateHabit;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Habits.Commands;

/// <summary>
/// La déclaration d'une habitude. Elle n'accorde aucun XP et ne touche à aucune série : c'est la
/// journalisation (<c>LogHabitCommand</c>) qui fera progresser le Chasseur, pas le fait de
/// s'être donné une intention.
/// </summary>
public class CreateHabitCommandHandlerTests
{
    private static readonly DateTimeOffset Maintenant =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly IHunterProfileRepository _profils = Substitute.For<IHunterProfileRepository>();
    private readonly IHabitRepository _habitudes = Substitute.For<IHabitRepository>();

    private readonly HunterProfile _profil = HunterProfile.Create();

    public CreateHabitCommandHandlerTests()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_profil);
    }

    private Task<CreateHabitResult> Creer(
        string nom = "Boire deux litres d'eau",
        HabitFrequency frequence = HabitFrequency.Quotidienne,
        Guid? chasseur = null) =>
        new CreateHabitCommandHandler(_profils, _habitudes, new HorlogeFigee()).Handle(
            new CreateHabitCommand(chasseur ?? _profil.Id, nom, frequence),
            CancellationToken.None);

    [Fact]
    public async Task Persiste_l_habitude_declaree()
    {
        await Creer(nom: "Lire vingt minutes", frequence: HabitFrequency.Hebdomadaire);

        await _habitudes.Received(1).AddAsync(
            Arg.Is<Habit>(habitude =>
                habitude != null
                && habitude.HunterProfileId == _profil.Id
                && habitude.Name == "Lire vingt minutes"
                && habitude.Frequency == HabitFrequency.Hebdomadaire),
            Arg.Any<CancellationToken>());
    }

    // L'écran a besoin de l'identifiant pour enchaîner sur la première journalisation sans
    // relire toute la liste.
    [Fact]
    public async Task Rend_l_identifiant_de_l_habitude_creee()
    {
        var resultat = await Creer();

        resultat.HabitId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Rend_le_nom_reellement_enregistre()
    {
        var resultat = await Creer(nom: "  Courir  ");

        resultat.Name.Should().Be("Courir");
    }

    [Fact]
    public async Task Rend_le_rythme_enregistre()
    {
        var resultat = await Creer(frequence: HabitFrequency.Hebdomadaire);

        resultat.Frequency.Should().Be(HabitFrequency.Hebdomadaire);
    }

    // L'horloge est injectée pour rester figeable : la date de création ordonne la liste, et un
    // test qui la lirait sur DateTimeOffset.UtcNow ne pourrait rien en affirmer.
    [Fact]
    public async Task Date_l_habitude_de_l_horloge_injectee()
    {
        await Creer();

        await _habitudes.Received(1).AddAsync(
            Arg.Is<Habit>(habitude => habitude != null && habitude.CreatedAt == Maintenant),
            Arg.Any<CancellationToken>());
    }

    // La clé étrangère finirait par refuser l'écriture, mais loin d'ici et dans une langue que
    // le Chasseur n'a pas à lire.
    [Fact]
    public async Task Refuse_de_declarer_une_habitude_pour_un_Chasseur_inconnu()
    {
        _profils.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HunterProfile?)null);

        var acte = async () => await Creer();

        await acte.Should().ThrowAsync<HunterProfileNotFoundException>();
    }

    // Deux habitudes homonymes rendent la liste illisible et scindent en deux la série de ce qui
    // est, pour le Chasseur, une seule intention.
    [Fact]
    public async Task Refuse_un_nom_deja_porte_par_une_habitude_du_Chasseur()
    {
        _habitudes.ExistsWithNameAsync(
                _profil.Id, "Courir", Arg.Any<CancellationToken>())
            .Returns(true);

        var acte = async () => await Creer(nom: "Courir");

        await acte.Should().ThrowAsync<HabitNameAlreadyTakenException>();
    }

    [Fact]
    public async Task N_ecrit_rien_quand_le_nom_est_deja_porte()
    {
        _habitudes.ExistsWithNameAsync(
                _profil.Id, "Courir", Arg.Any<CancellationToken>())
            .Returns(true);

        var acte = async () => await Creer(nom: "Courir");

        await acte.Should().ThrowAsync<HabitNameAlreadyTakenException>();
        await _habitudes.DidNotReceive().AddAsync(
            Arg.Any<Habit>(), Arg.Any<CancellationToken>());
    }

    // Le nom cherché est celui que l'habitude portera : interroger le brut laisserait passer
    // « Courir » puis « Courir  » comme deux déclarations distinctes.
    [Fact]
    public async Task Cherche_l_unicite_sur_le_nom_rogne()
    {
        await Creer(nom: "  Courir  ");

        await _habitudes.Received(1).ExistsWithNameAsync(
            _profil.Id, "Courir", Arg.Any<CancellationToken>());
    }

    private sealed class HorlogeFigee : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Maintenant;
    }
}
