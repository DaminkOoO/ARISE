namespace Arise.Domain.Quests;

/// <summary>
/// Une quête posée pour un Chasseur, un domaine et un jour donnés — le contrat de sortie de
/// l'agent de génération (doc mécaniques, section 3) une fois devenu une ligne en base.
///
/// <para>Une quête ne se modifie pas après coup : son texte est celui que le Chasseur a lu le
/// matin. Seule sa complétion la fait évoluer, et c'est la seule mutation que le modèle
/// prévoit.</para>
/// </summary>
public sealed class Quest
{
    /// <summary>
    /// Bornes portées par l'entité et pas seulement par la validation de l'agent : ce sont
    /// aussi les largeurs de colonne. Les faire respecter ici évite qu'un chemin d'écriture
    /// qui ne passe pas par l'agent — seed, import, quête de pénalité posée par le worker —
    /// construise une quête que le Domain accepte et que PostgreSQL refuse au
    /// <c>SaveChangesAsync</c>, loin du site fautif.
    /// </summary>
    public const int LongueurMaximaleTitre = 80;

    /// <inheritdoc cref="LongueurMaximaleTitre"/>
    public const int LongueurMaximaleDescription = 400;

    private Quest()
    {
    }

    public Guid Id { get; private set; }

    public Guid HunterProfileId { get; private set; }

    public QuestDomain Domain { get; private set; }

    public DateOnly QuestDate { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public QuestType Type { get; private set; }

    public QuestStat StatTarget { get; private set; }

    public QuestDifficulty Difficulty { get; private set; }

    public int XpReward { get; private set; }

    public bool IsFallback { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Une quête est complétée dès lors qu'elle porte un instant de complétion — un seul
    /// champ, donc pas de drapeau qui puisse contredire la date.
    /// </summary>
    public bool IsCompleted => CompletedAt is not null;

    /// <summary>
    /// Pose la quête du jour d'un Chasseur.
    /// </summary>
    /// <param name="isFallback">
    /// <see langword="true"/> quand le texte vient du repli neutre de l'agent plutôt que du
    /// modèle. Persisté, et non recalculé à la lecture : sans cela, la même quête serait
    /// annoncée « de repli » le jour de sa génération puis « générée » à la relecture du
    /// lendemain.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Le Chasseur visé n'a pas d'identifiant, ou le titre ou la description est vide.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Le titre ou la description dépasse la largeur de sa colonne, ou la récompense sort de
    /// la fourchette de sa difficulté (<see cref="BaremeXpQuete"/>).
    /// </exception>
    public static Quest Generate(
        Guid hunterProfileId,
        QuestDomain domain,
        DateOnly questDate,
        string title,
        string description,
        QuestType type,
        QuestStat statTarget,
        QuestDifficulty difficulty,
        int xpReward,
        bool isFallback)
    {
        if (hunterProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Une quête doit viser un Chasseur identifié.", nameof(hunterProfileId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        // Les espaces de bordure sont invisibles à l'écran mais comptent dans la colonne : le
        // texte stocké est celui que le Chasseur lira, rogné une bonne fois.
        var titreCanonique = title.Trim();
        var descriptionCanonique = description.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            titreCanonique.Length, LongueurMaximaleTitre, nameof(title));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            descriptionCanonique.Length, LongueurMaximaleDescription, nameof(description));

        // Même barème que celui appliqué à la réponse du modèle : l'agent n'est pas le seul
        // chemin d'écriture, la règle ne peut donc pas vivre uniquement chez lui.
        if (!BaremeXpQuete.EstCoherent(type, difficulty, xpReward))
        {
            var (minimum, maximum) = BaremeXpQuete.Fourchette(type, difficulty);
            throw new ArgumentOutOfRangeException(
                nameof(xpReward),
                xpReward,
                $"La récompense d'une quête {difficulty} doit tenir entre {minimum} et {maximum} XP.");
        }

        return new Quest
        {
            Id = Guid.NewGuid(),
            HunterProfileId = hunterProfileId,
            Domain = domain,
            QuestDate = questDate,
            Title = titreCanonique,
            Description = descriptionCanonique,
            Type = type,
            StatTarget = statTarget,
            Difficulty = difficulty,
            XpReward = xpReward,
            IsFallback = isFallback,
            CompletedAt = null,
        };
    }
}
