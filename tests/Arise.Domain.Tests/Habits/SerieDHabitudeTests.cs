using Arise.Domain.Habits;
using FluentAssertions;

namespace Arise.Domain.Tests.Habits;

/// <summary>
/// La série d'une habitude, recalculée depuis son journal (doc mécaniques, section 2). Locale à
/// chaque habitude, elle ne se confond pas avec la série d'engagement du profil Chasseur, que
/// seules les quêtes alimentent.
///
/// <para>L'unité vient du rythme déclaré : des <b>jours</b> consécutifs pour une quotidienne,
/// des <b>semaines</b> pour une hebdomadaire.</para>
/// </summary>
public class SerieDHabitudeTests
{
    /// <summary>Dimanche 26 juillet 2026 — dernier jour de la semaine du lundi 20.</summary>
    private static readonly DateOnly AujourdHui = new(2026, 7, 26);

    private static int Serie(
        HabitFrequency frequence, DateOnly aujourdHui, params DateOnly[] jours) =>
        SerieDHabitude.Calculer(frequence, jours, aujourdHui);

    private static DateOnly Juillet(int jour) => new(2026, 7, jour);

    // --- Habitude quotidienne : la série compte des jours ------------------------------------

    [Fact]
    public void Une_habitude_jamais_tenue_n_a_pas_de_serie()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui).Should().Be(0);
    }

    [Fact]
    public void Une_habitude_tenue_aujourd_hui_ouvre_une_serie_d_un_jour()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(26)).Should().Be(1);
    }

    [Fact]
    public void Compte_les_jours_consecutifs_jusqu_a_aujourd_hui()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(24), Juillet(25), Juillet(26))
            .Should().Be(3);
    }

    // La journée n'est pas finie : une habitude tenue trois jours de suite jusqu'à hier garde sa
    // série jusqu'à ce soir. Sans cette tolérance, l'écran annoncerait « série rompue » chaque
    // matin au réveil, à quelqu'un qui n'a encore rien manqué.
    [Fact]
    public void Une_habitude_tenue_jusqu_a_hier_garde_sa_serie()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(23), Juillet(24), Juillet(25))
            .Should().Be(3);
    }

    [Fact]
    public void Une_habitude_laissee_depuis_avant_hier_a_perdu_sa_serie()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(23), Juillet(24))
            .Should().Be(0);
    }

    // Seul le segment qui touche aujourd'hui compte : les quatre jours de la semaine passée ont
    // été tenus, mais le trou du 22 les a détachés du présent.
    [Fact]
    public void Ne_compte_que_le_segment_qui_touche_aujourd_hui()
    {
        Serie(
            HabitFrequency.Quotidienne,
            AujourdHui,
            Juillet(18), Juillet(19), Juillet(20), Juillet(21),
            Juillet(25), Juillet(26))
            .Should().Be(2);
    }

    // Le journal porte un index unique par jour, mais la série ne s'appuie pas dessus pour être
    // juste : deux entrées le même jour restent un jour de série.
    [Fact]
    public void Deux_entrees_le_meme_jour_ne_comptent_que_pour_un()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(26), Juillet(26), Juillet(25))
            .Should().Be(2);
    }

    // Le calcul ne présuppose pas l'ordre du repository : un ORDER BY oublié en Infrastructure ne
    // doit pas se traduire par une série fausse à l'écran.
    [Fact]
    public void Ne_depend_pas_de_l_ordre_dans_lequel_les_jours_arrivent()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(25), Juillet(23), Juillet(26), Juillet(24))
            .Should().Be(4);
    }

    // Le jour du Chasseur peut devancer celui du serveur — tablette restée à Paris où il est déjà
    // le 27, téléphone encore à New York le 26. Même philosophie que la série du profil
    // (doc mécaniques, section 2) : une date d'avance ne retire rien à qui n'a rien manqué.
    [Fact]
    public void Un_jour_journalise_en_avance_ne_rompt_pas_la_serie()
    {
        Serie(HabitFrequency.Quotidienne, AujourdHui, Juillet(25), Juillet(26), Juillet(27))
            .Should().Be(3);
    }

    // --- Habitude hebdomadaire : la série compte des semaines ---------------------------------

    [Fact]
    public void Une_habitude_hebdomadaire_tenue_cette_semaine_ouvre_une_serie_d_une_semaine()
    {
        Serie(HabitFrequency.Hebdomadaire, AujourdHui, Juillet(22)).Should().Be(1);
    }

    [Fact]
    public void Compte_les_semaines_consecutives_pour_une_habitude_hebdomadaire()
    {
        Serie(HabitFrequency.Hebdomadaire, AujourdHui, Juillet(8), Juillet(15), Juillet(22))
            .Should().Be(3);
    }

    // C'est tout l'intérêt du rythme hebdomadaire : deux séances dans la semaine tiennent
    // l'engagement une fois, pas deux.
    [Fact]
    public void Deux_entrees_la_meme_semaine_ne_comptent_que_pour_une()
    {
        Serie(HabitFrequency.Hebdomadaire, AujourdHui, Juillet(21), Juillet(24))
            .Should().Be(1);
    }

    // Symétrique de la tolérance quotidienne : la semaine en cours n'est pas finie, celle du
    // 13 au 19 suffit à tenir la série jusqu'à dimanche soir.
    [Fact]
    public void Une_habitude_hebdomadaire_tenue_la_semaine_passee_garde_sa_serie()
    {
        Serie(HabitFrequency.Hebdomadaire, AujourdHui, Juillet(8), Juillet(15))
            .Should().Be(2);
    }

    [Fact]
    public void Une_habitude_hebdomadaire_laissee_depuis_deux_semaines_a_perdu_sa_serie()
    {
        Serie(HabitFrequency.Hebdomadaire, AujourdHui, Juillet(8)).Should().Be(0);
    }

    // La semaine commence le lundi : le dimanche 26 clôt celle du lundi 20, il n'ouvre pas celle
    // du 27. Découper au dimanche scinderait en deux séries l'habitude tenue le samedi puis le
    // dimanche suivant.
    [Fact]
    public void La_semaine_commence_le_lundi()
    {
        Serie(HabitFrequency.Hebdomadaire, Juillet(27), Juillet(26), Juillet(27))
            .Should().Be(2);
    }
}
