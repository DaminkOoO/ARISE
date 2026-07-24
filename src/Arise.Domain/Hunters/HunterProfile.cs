using Arise.Domain.Common;

namespace Arise.Domain.Hunters;

/// <summary>
/// Progression d'un Chasseur : XP, niveau, rang et série d'engagement quotidien. Son compte
/// (nom d'utilisateur, mot de passe) vit dans <see cref="Arise.Domain.Users.User"/>, pas ici —
/// deux entités qui ne changent jamais pour la même raison.
///
/// <para>Le niveau et le rang dérivent de l'XP total ; aucun des deux ne s'écrit
/// directement depuis l'extérieur, pour qu'ils ne puissent pas diverger de ce que l'XP
/// implique.</para>
/// </summary>
public sealed class HunterProfile
{
    private readonly List<IDomainEvent> _domainEvents = [];

    // EF Core matérialise par ce constructeur ; le reste du monde passe par Create.
    private HunterProfile()
    {
    }

    public Guid Id { get; private set; }

    public int Level { get; private set; }

    public HunterRank Rank { get; private set; }

    public int CurrentXp { get; private set; }

    public int XpToNextLevel { get; private set; }

    public int StreakCurrent { get; private set; }

    public int StreakLongest { get; private set; }

    public DateOnly? LastCompletionDate { get; private set; }

    /// <summary>
    /// Événements accumulés depuis la dernière <see cref="ClearDomainEvents"/> — à publier
    /// par la couche Application (via MediatR) après persistance, pas à consommer ici.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Crée le profil de départ d'un Chasseur fraîchement Éveillé. Ces valeurs sont des
    /// constantes déterministes (doc mécaniques, section 4) : jamais générées par un agent,
    /// pour que l'équilibrage du jeu ne dépende pas du caprice d'un LLM.
    /// </summary>
    public static HunterProfile Create()
    {
        const int niveauDeDepart = 1;

        return new HunterProfile
        {
            Id = Guid.NewGuid(),
            Level = niveauDeDepart,
            Rank = RankFor(niveauDeDepart),
            CurrentXp = 0,
            XpToNextLevel = CalculerXpProchainNiveau(niveauDeDepart),
            StreakCurrent = 0,
            StreakLongest = 0,
            LastCompletionDate = null,
        };
    }

    /// <summary>
    /// Attribue de l'XP et fait monter le niveau autant de fois que nécessaire — une boucle,
    /// pas un simple test, pour qu'un gros gain (Boss Raid) puisse franchir plusieurs niveaux
    /// d'un coup. Un franchissement de rang au passage lève <see cref="HunterRankedUpEvent"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="montant"/> est négatif ou nul — l'XP ne se retire jamais silencieusement.
    /// </exception>
    public void AwardXp(int montant)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(montant, 0);

        CurrentXp += montant;

        while (CurrentXp >= CalculerXpProchainNiveau(Level))
        {
            CurrentXp -= CalculerXpProchainNiveau(Level);
            Level += 1;

            var nouveauRang = RankFor(Level);
            if (nouveauRang != Rank)
            {
                _domainEvents.Add(new HunterRankedUpEvent(Id, Rank, nouveauRang));
                Rank = nouveauRang;
            }
        }

        XpToNextLevel = CalculerXpProchainNiveau(Level);
    }

    /// <summary>
    /// Enregistre qu'au moins une quête <c>daily</c> ou <c>penalty</c> a été complétée à la
    /// date <paramref name="today"/> (fuseau horaire du Chasseur — l'appelant est responsable
    /// de convertir avant d'appeler). Fonction pure : date + état → nouvel état, idempotente
    /// pour deux appels le même jour.
    /// </summary>
    public void RegisterDailyCompletion(DateOnly today)
    {
        if (LastCompletionDate == today)
        {
            // Déjà comptabilisé aujourd'hui : ni la série ni le record n'ont à bouger.
            return;
        }

        StreakCurrent = LastCompletionDate == today.AddDays(-1)
            ? StreakCurrent + 1
            : 1; // première complétion, ou trou de deux jours ou plus → on repart de 1.

        LastCompletionDate = today;
        StreakLongest = Math.Max(StreakLongest, StreakCurrent);
    }

    /// <summary>
    /// Constate qu'un jour entier s'est écoulé sans complétion et remet la série courante à
    /// zéro le cas échéant. Un fait, jamais une punition : la série repart simplement de 0
    /// à la prochaine complétion.
    /// </summary>
    /// <returns><see langword="true"/> si la série vient d'être rompue par cet appel.</returns>
    public bool CheckStreakBreak(DateOnly today)
    {
        if (LastCompletionDate is { } derniereCompletion
            && derniereCompletion < today.AddDays(-1)
            && StreakCurrent > 0)
        {
            StreakCurrent = 0;
            return true;
        }

        return false;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Mappe un niveau vers son rang (doc mécaniques, section 1) : E 1-4, D 5-9, C 10-14,
    /// B 15-19, A 20-24, S 25 et au-delà, sans plafond.
    /// </summary>
    public static HunterRank RankFor(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        return level switch
        {
            <= 4 => HunterRank.E,
            <= 9 => HunterRank.D,
            <= 14 => HunterRank.C,
            <= 19 => HunterRank.B,
            <= 24 => HunterRank.A,
            _ => HunterRank.S,
        };
    }

    /// <summary>
    /// XP requis pour passer du niveau <paramref name="level"/> au suivant (doc mécaniques,
    /// section 1). Seule et unique définition de cette formule dans le dépôt.
    /// </summary>
    private static int CalculerXpProchainNiveau(int level) => 100 + ((level - 1) * 20);
}
