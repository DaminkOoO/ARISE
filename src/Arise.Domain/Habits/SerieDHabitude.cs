namespace Arise.Domain.Habits;

/// <summary>
/// La série d'une habitude, recalculée depuis son journal (doc mécaniques, section 2 :
/// « <c>Habit</c> a sa propre série calculée depuis <c>HabitLog</c> »). Seule et unique
/// définition de cette règle dans le dépôt.
///
/// <para><b>À ne pas confondre</b> avec la série d'engagement du profil Chasseur
/// (<c>HunterProfile.StreakCurrent</c>), que seules les quêtes alimentent et qui est un
/// compteur stocké. Celle-ci est locale à une habitude, et <b>dérivée</b> : rien ne la stocke,
/// donc rien ne peut diverger de son journal.</para>
///
/// <para>L'unité vient du rythme déclaré — des jours pour une quotidienne, des semaines pour une
/// hebdomadaire. Les deux se ramènent à un même comptage de périodes consécutives, ce qui évite
/// deux algorithmes à garder d'accord.</para>
/// </summary>
public static class SerieDHabitude
{
    /// <summary>
    /// Longueur de la série de cette habitude au jour <paramref name="aujourdHui"/> — exprimé
    /// dans le fuseau du Chasseur, comme les jours du journal.
    /// </summary>
    /// <param name="joursJournalises">
    /// Les jours où l'habitude a été tenue, dans n'importe quel ordre et avec d'éventuels
    /// doublons : le calcul ne présuppose rien de ce que rend le stockage.
    /// </param>
    public static int Calculer(
        HabitFrequency frequence,
        IEnumerable<DateOnly> joursJournalises,
        DateOnly aujourdHui)
    {
        var periodes = joursJournalises
            .Select(jour => IndexDePeriode(frequence, jour))
            // Deux séances le même jour — ou deux la même semaine pour une hebdomadaire — ne
            // tiennent l'engagement qu'une fois. L'index unique du journal l'empêche déjà par
            // jour, mais la règle appartient au calcul, pas à la contrainte de base.
            .Distinct()
            .OrderByDescending(index => index)
            .ToList();

        if (periodes.Count == 0)
        {
            return 0;
        }

        var periodeCourante = IndexDePeriode(frequence, aujourdHui);

        // Tolérance d'exactement une période : la journée (ou la semaine) en cours n'est pas
        // finie. Sans elle, l'écran annoncerait « série rompue » chaque matin au réveil, à
        // quelqu'un qui n'a encore rien manqué — le contraire du ton attendu (règle n°5).
        //
        // La soustraction peut être négative, et c'est voulu : le jour du Chasseur devance
        // parfois celui du serveur — tablette restée à Paris, téléphone passé à New York. Même
        // philosophie que la série du profil : une date d'avance ne retire rien à qui n'a rien
        // manqué.
        if (periodeCourante - periodes[0] > 1)
        {
            return 0;
        }

        // On remonte depuis la période la plus récente : seul le segment qui touche le présent
        // est une série. Les jours d'un segment plus ancien ont été tenus, mais le trou les a
        // détachés.
        var serie = 1;

        for (var rang = 1; rang < periodes.Count; rang++)
        {
            if (periodes[rang] != periodes[rang - 1] - 1)
            {
                break;
            }

            serie++;
        }

        return serie;
    }

    /// <summary>
    /// Numéro de la période à laquelle ce jour appartient. Deux périodes consécutives diffèrent
    /// exactement de 1, ce qui est toute la propriété dont le comptage a besoin.
    /// </summary>
    private static int IndexDePeriode(HabitFrequency frequence, DateOnly jour) => frequence switch
    {
        HabitFrequency.Quotidienne => jour.DayNumber,

        // Division entière du lundi de la semaine : les lundis sont espacés de 7 jours, donc
        // deux semaines voisines donnent bien deux entiers voisins, quel que soit le reste.
        HabitFrequency.Hebdomadaire => LundiDeLaSemaine(jour).DayNumber / 7,

        _ => throw new ArgumentOutOfRangeException(nameof(frequence)),
    };

    /// <summary>
    /// La semaine commence le lundi. Découper au dimanche — ce que fait
    /// <see cref="DayOfWeek"/>, dont le zéro est le dimanche — scinderait en deux séries
    /// l'habitude tenue le samedi puis le dimanche suivant.
    /// </summary>
    private static DateOnly LundiDeLaSemaine(DateOnly jour) =>
        jour.AddDays(-(((int)jour.DayOfWeek + 6) % 7));
}
