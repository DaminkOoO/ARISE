using Arise.Domain.Habits;
using FluentAssertions;

namespace Arise.Domain.Tests.Habits;

/// <summary>
/// Une ligne de journal : « cette habitude a été tenue ce jour-là ». C'est la seule trace dont
/// la série d'une habitude se déduit (doc mécaniques, section 2) — l'entrée ne porte donc aucun
/// compteur, et rien ici ne calcule de série.
///
/// <para>Deux dates, et elles ne disent pas la même chose : <c>Day</c> est le jour <b>du
/// Chasseur</b>, celui auquel l'effort appartient et que la série comptera ; <c>LoggedAt</c>
/// n'est que l'horodatage du tap. Les confondre daterait au 26 une habitude tenue le 25 et
/// validée à 00h05.</para>
/// </summary>
public class HabitLogTests
{
    private static readonly Guid Habitude = Guid.NewGuid();

    private static readonly DateOnly Jour = new(2026, 7, 26);

    private static readonly DateTimeOffset Tap =
        new(2026, 7, 26, 21, 30, 0, TimeSpan.Zero);

    private static HabitLog Journaliser(
        Guid? habitude = null, DateOnly? jour = null, DateTimeOffset? tap = null) =>
        HabitLog.Create(habitude ?? Habitude, jour ?? Jour, tap ?? Tap);

    [Fact]
    public void Journalise_pour_l_habitude_visee()
    {
        Journaliser().HabitId.Should().Be(Habitude);
    }

    [Fact]
    public void Journalise_une_entree_dotee_d_un_identifiant()
    {
        Journaliser().Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Journalise_au_jour_du_Chasseur_recu()
    {
        Journaliser(jour: new DateOnly(2026, 7, 25)).Day
            .Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public void Retient_l_instant_du_tap()
    {
        Journaliser().LoggedAt.Should().Be(Tap);
    }

    // Le cas qui tranche (doc mécaniques, section 2) : la séance a lieu le 25 à 23h50, le
    // Chasseur valide à 00h05 le 26. Le jour du Chasseur est fourni par l'appelant, seul à le
    // connaître, et l'entité ne le redéduit surtout pas de l'horodatage.
    [Fact]
    public void Ne_deduit_pas_le_jour_du_Chasseur_de_l_instant_du_tap()
    {
        var entree = Journaliser(
            jour: new DateOnly(2026, 7, 25),
            tap: new DateTimeOffset(2026, 7, 26, 0, 5, 0, TimeSpan.Zero));

        entree.Day.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public void Refuse_une_entree_sans_habitude()
    {
        var acte = () => Journaliser(habitude: Guid.Empty);

        acte.Should().Throw<ArgumentException>();
    }

    // Même garde que sur Habit.Create : la colonne est un timestamp with time zone, sur lequel
    // Npgsql refuse un DateTimeOffset décalé. Sans elle, un appelant qui passe DateTimeOffset.Now
    // ne l'apprend qu'au SaveChangesAsync, loin d'ici.
    [Fact]
    public void Refuse_un_instant_de_tap_decale()
    {
        var acte = () => Journaliser(
            tap: new DateTimeOffset(2026, 7, 26, 21, 30, 0, TimeSpan.FromHours(2)));

        acte.Should().Throw<ArgumentException>();
    }
}
