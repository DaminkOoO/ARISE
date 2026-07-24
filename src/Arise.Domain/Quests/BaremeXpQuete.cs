namespace Arise.Domain.Quests;

/// <summary>
/// Récompense d'XP admissible selon la difficulté d'une quête (doc mécaniques, section 1) :
/// facile 10-15, moyenne 15-25, difficile 25-40, quête de pénalité 10 fixes.
///
/// <para>Seule et unique définition de ces bornes dans le dépôt. Deux appelants s'en servent,
/// et pour deux raisons différentes : <see cref="Quest"/> refuse d'exister avec une récompense
/// incohérente, et l'agent de génération rejette la réponse du modèle avant même d'en faire une
/// quête. Un garde-fou écrit dans le prompt seul se contournerait à la première réponse
/// inattendue — celui-ci est du C#.</para>
///
/// <para>Les fourchettes se chevauchent à 15 et à 25, ce qui est voulu : un même montant peut
/// être justifiable pour deux difficultés voisines, et resserrer arbitrairement les bornes
/// ferait rejeter des réponses parfaitement conformes au document de référence.</para>
/// </summary>
public static class BaremeXpQuete
{
    /// <summary>
    /// Récompense fixe d'une quête de pénalité : elle est facile par conception, et sa valeur
    /// ne dépend donc pas de la difficulté que le modèle a cru bon d'annoncer.
    /// </summary>
    private const int XpPenalite = 10;

    /// <summary>
    /// Bornes inclusives admissibles pour ce couple type/difficulté. Sert aussi à rappeler au
    /// modèle, lors de l'unique nouvelle tentative, ce qu'il aurait dû rendre.
    /// </summary>
    public static (int Minimum, int Maximum) Fourchette(QuestType type, QuestDifficulty difficulte)
    {
        if (type == QuestType.Penalite)
        {
            return (XpPenalite, XpPenalite);
        }

        return difficulte switch
        {
            QuestDifficulty.Facile => (10, 15),
            QuestDifficulty.Moyenne => (15, 25),
            QuestDifficulty.Difficile => (25, 40),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulte)),
        };
    }

    /// <summary>
    /// <see langword="true"/> si <paramref name="xp"/> tombe dans la fourchette de ce couple
    /// type/difficulté. Un montant nul ou négatif est hors de toute fourchette : aucune quête
    /// ne retire d'XP.
    /// </summary>
    public static bool EstCoherent(QuestType type, QuestDifficulty difficulte, int xp)
    {
        var (minimum, maximum) = Fourchette(type, difficulte);

        return xp >= minimum && xp <= maximum;
    }
}
