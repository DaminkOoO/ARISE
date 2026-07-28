using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Habits.Commands.LogHabit;
using Arise.Domain.Habits;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Habits.Commands;

/// <summary>
/// La journalisation d'une habitude : « je l'ai tenue aujourd'hui ». Elle écrit une ligne de
/// journal et rend la série recalculée — elle n'accorde <b>aucun XP</b> et ne touche pas à la
/// série d'engagement du profil, que seules les quêtes alimentent (doc mécaniques, section 2).
///
/// <para>Le jour daté est celui du Chasseur, déduit de son fuseau : c'est ce que la série
/// comptera, et non l'heure du serveur.</para>
/// </summary>
public class LogHabitCommandHandlerTests
{
    private static readonly DateTimeOffset Maintenant =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Dimanche 26 juillet 2026, tel que le Chasseur le vit à Paris.</summary>
    private static readonly DateOnly Aujourdhui = new(2026, 7, 26);

    private readonly IHabitRepository _habitudes = Substitute.For<IHabitRepository>();
    private readonly IHabitLogRepository _journaux = Substitute.For<IHabitLogRepository>();

    private readonly Guid _chasseur = Guid.NewGuid();
    private readonly Habit _habitude;

    public LogHabitCommandHandlerTests()
    {
        _habitude = Habit.Create(
            _chasseur, "Boire deux litres d'eau", HabitFrequency.Quotidienne, Maintenant);

        _habitudes.GetByIdAsync(_habitude.Id, Arg.Any<CancellationToken>()).Returns(_habitude);
        _journaux.GetDaysAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private Task<LogHabitResult> Journaliser(
        Guid? chasseur = null,
        Guid? habitude = null,
        string fuseau = "Europe/Paris",
        DateTimeOffset? maintenant = null) =>
        new LogHabitCommandHandler(
                _habitudes, _journaux, new HorlogeFigee(maintenant ?? Maintenant))
            .Handle(
                new LogHabitCommand(chasseur ?? _chasseur, habitude ?? _habitude.Id, fuseau),
                CancellationToken.None);

    /// <summary>Un seul journal est lu par test : le stub n'a pas à distinguer les habitudes.</summary>
    private void JournalDeja(params DateOnly[] jours) =>
        _journaux.GetDaysAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(jours);

    private Habit Declarer(HabitFrequency frequence)
    {
        var habitude = Habit.Create(_chasseur, "Séance longue", frequence, Maintenant);

        _habitudes.GetByIdAsync(habitude.Id, Arg.Any<CancellationToken>()).Returns(habitude);

        return habitude;
    }

    [Fact]
    public async Task Journalise_l_habitude_au_jour_du_Chasseur()
    {
        await Journaliser();

        await _journaux.Received(1).AddAsync(
            Arg.Is<HabitLog>(entree =>
                entree != null
                && entree.HabitId == _habitude.Id
                && entree.Day == Aujourdhui),
            Arg.Any<CancellationToken>());
    }

    // L'horloge est injectée pour rester figeable, comme à la déclaration : un test qui lirait
    // DateTimeOffset.UtcNow ne pourrait rien affirmer de l'horodatage.
    [Fact]
    public async Task Date_l_entree_de_l_horloge_injectee()
    {
        await Journaliser();

        await _journaux.Received(1).AddAsync(
            Arg.Is<HabitLog>(entree => entree != null && entree.LoggedAt == Maintenant),
            Arg.Any<CancellationToken>());
    }

    // Le cas qui tranche : il est 22h30 le 26 en UTC, donc déjà 00h30 le 27 à Paris. L'effort
    // appartient au jour que le Chasseur vit, pas à celui du serveur.
    [Fact]
    public async Task Date_l_entree_au_jour_du_fuseau_du_Chasseur_et_non_du_serveur()
    {
        await Journaliser(
            maintenant: new DateTimeOffset(2026, 7, 26, 22, 30, 0, TimeSpan.Zero));

        await _journaux.Received(1).AddAsync(
            Arg.Is<HabitLog>(entree => entree != null && entree.Day == new DateOnly(2026, 7, 27)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rend_le_jour_journalise()
    {
        var resultat = await Journaliser();

        resultat.Jour.Should().Be(Aujourdhui);
    }

    [Fact]
    public async Task Une_premiere_journalisation_ouvre_une_serie_d_un_jour()
    {
        var resultat = await Journaliser();

        resultat.SerieActuelle.Should().Be(1);
    }

    // La série rendue inclut la journalisation qui vient d'avoir lieu : l'écran affiche « 3 jours »
    // dès le tap, sans relire.
    [Fact]
    public async Task Rend_la_serie_prolongee_par_cette_journalisation()
    {
        JournalDeja(new DateOnly(2026, 7, 24), new DateOnly(2026, 7, 25));

        var resultat = await Journaliser();

        resultat.SerieActuelle.Should().Be(3);
    }

    // La série se compte au rythme déclaré : deux séances la même semaine ne valent qu'une
    // semaine d'engagement pour une hebdomadaire.
    [Fact]
    public async Task Compte_la_serie_au_rythme_de_l_habitude()
    {
        var hebdomadaire = Declarer(HabitFrequency.Hebdomadaire);
        JournalDeja(new DateOnly(2026, 7, 22));

        var resultat = await Journaliser(habitude: hebdomadaire.Id);

        resultat.SerieActuelle.Should().Be(1);
    }

    // Double-tap, renvoi réseau, deux appareils : le jour est déjà tenu, et le journal n'a pas à
    // recevoir une seconde ligne — sans quoi la série se compterait juste, mais l'historique
    // mentirait.
    [Fact]
    public async Task N_ecrit_pas_une_seconde_entree_pour_un_jour_deja_tenu()
    {
        JournalDeja(Aujourdhui);

        await Journaliser();

        await _journaux.DidNotReceive().AddAsync(
            Arg.Any<HabitLog>(), Arg.Any<CancellationToken>());
    }

    // Ce n'est pas une erreur : le Chasseur a bien tenu son habitude. C'est ce drapeau qui permet
    // à l'écran d'écrire « déjà validée aujourd'hui » plutôt que de rejouer l'animation.
    [Fact]
    public async Task Annonce_un_jour_deja_tenu_sans_le_traiter_comme_une_erreur()
    {
        JournalDeja(Aujourdhui);

        var resultat = await Journaliser();

        resultat.DejaJournalisee.Should().BeTrue();
    }

    [Fact]
    public async Task Rend_la_serie_courante_pour_un_jour_deja_tenu()
    {
        JournalDeja(new DateOnly(2026, 7, 25), Aujourdhui);

        var resultat = await Journaliser();

        resultat.SerieActuelle.Should().Be(2);
    }

    [Fact]
    public async Task Annonce_une_journalisation_neuve_quand_le_jour_n_etait_pas_tenu()
    {
        var resultat = await Journaliser();

        resultat.DejaJournalisee.Should().BeFalse();
    }

    // Deux taps simultanés, deux scopes, deux DbContext : la lecture préalable n'a rien vu, et
    // c'est l'index unique du journal qui tranche. Le perdant se comporte alors exactement comme
    // un double-tap séquentiel — l'habitude est tenue, le journal n'a qu'une ligne.
    [Fact]
    public async Task Une_journalisation_simultanee_perdue_se_comporte_comme_un_double_tap()
    {
        JournalDeja(new DateOnly(2026, 7, 25));
        _journaux.AddAsync(Arg.Any<HabitLog>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HabitAlreadyLoggedException()));

        var resultat = await Journaliser();

        resultat.DejaJournalisee.Should().BeTrue();
        resultat.SerieActuelle.Should().Be(2);
    }

    [Fact]
    public async Task Refuse_de_journaliser_une_habitude_inconnue()
    {
        _habitudes.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Habit?)null);

        var acte = async () => await Journaliser();

        await acte.Should().ThrowAsync<HabitNotFoundException>();
    }

    // Sans ce contrôle, n'importe quel Chasseur alimenterait la série des habitudes d'autrui.
    // Même exception que pour une habitude inconnue, pour ne pas révéler celle d'un autre.
    [Fact]
    public async Task Refuse_de_journaliser_l_habitude_d_un_autre_Chasseur()
    {
        var acte = async () => await Journaliser(chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<HabitNotFoundException>();
    }

    [Fact]
    public async Task N_ecrit_rien_pour_l_habitude_d_un_autre_Chasseur()
    {
        var acte = async () => await Journaliser(chasseur: Guid.NewGuid());

        await acte.Should().ThrowAsync<HabitNotFoundException>();
        await _journaux.DidNotReceive().AddAsync(
            Arg.Any<HabitLog>(), Arg.Any<CancellationToken>());
    }

    // Une habitude rangée a quitté la liste du Chasseur : la journaliser viendrait forcément d'un
    // écran périmé, et lui laisser prolonger une série invisible n'aurait aucun sens. Le message
    // dit quoi faire — la remettre dans la liste — plutôt que de reprocher le geste.
    [Fact]
    public async Task Refuse_de_journaliser_une_habitude_rangee()
    {
        _habitude.Archive();

        var acte = async () => await Journaliser();

        await acte.Should().ThrowAsync<HabitArchivedException>();
    }

    private sealed class HorlogeFigee(DateTimeOffset maintenant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => maintenant;
    }
}
