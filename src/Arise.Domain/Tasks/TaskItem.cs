namespace Arise.Domain.Tasks;

/// <summary>
/// Une tâche ponctuelle du Chasseur : quelque chose à faire <b>une fois</b>, par opposition à
/// <c>Habit</c> qui est une intention qui revient. D'où les deux différences de modèle : une
/// échéance facultative plutôt qu'un rythme, et une complétion portée par l'entité plutôt qu'un
/// journal dont une série se déduit.
///
/// <para>Comme l'habitude, elle n'accorde <b>aucun XP</b> et ne lève aucun événement de domaine :
/// la série d'engagement du profil ne se nourrit que de quêtes (doc mécaniques, section 2), et le
/// document ne chiffre de récompense que pour celles-ci.</para>
/// </summary>
public sealed class TaskItem
{
    /// <summary>
    /// Borne haute du titre, portée par l'entité et pas seulement par le formulaire : c'est aussi
    /// la largeur de la colonne. La faire respecter ici évite qu'un chemin d'écriture qui ne
    /// passe pas par la commande — seed, import — construise une tâche que le Domain accepte et
    /// que PostgreSQL refuse au <c>SaveChangesAsync</c>, loin du site fautif.
    ///
    /// <para>Plus généreuse que celle du nom d'une habitude (60) : une habitude est une étiquette
    /// qu'on relit chaque jour — « Courir » —, là où une tâche est souvent la phrase entière de ce
    /// qu'il reste à faire, « Envoyer les justificatifs à la mutuelle avant vendredi ».</para>
    /// </summary>
    public const int LongueurMaximaleTitre = 120;

    // EF Core matérialise par ce constructeur ; le reste du monde passe par Create.
    private TaskItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid HunterProfileId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Échéance <b>facultative</b> : « ranger le garage » n'a pas de date, et lui en inventer une
    /// afficherait un retard que le Chasseur ne s'est jamais donné.
    /// </summary>
    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Une tâche est faite dès lors qu'elle porte un instant de complétion — un seul champ, comme
    /// sur <c>Quest</c>, donc pas de drapeau qui puisse contredire la date.
    /// </summary>
    public bool IsCompleted => CompletedAt is not null;

    /// <summary>
    /// Marque la tâche comme faite.
    ///
    /// <para><b>Idempotente</b>, pour la même raison que <c>Quest.Complete</c> : un double-tap, un
    /// renvoi réseau ou deux appareils du même Chasseur mènent tous au même appel, et aucun
    /// handler ne peut promettre d'être seul. La garde vit donc ici plutôt que dans un
    /// <c>if (!tache.IsCompleted)</c> côté handler, que le prochain domaine devrait réécrire à
    /// l'identique en espérant ne pas l'oublier.</para>
    ///
    /// <para>Aucun événement de domaine n'est levé, contrairement à <c>Quest.Complete</c> : rien
    /// n'écoute la complétion d'une tâche, ni XP ni série. Le jour où une extension de
    /// gamification voudra l'écouter, c'est ici qu'elle se branchera.</para>
    /// </summary>
    /// <returns>
    /// <see langword="true"/> si cet appel vient de faire la tâche, <see langword="false"/> si
    /// elle l'était déjà.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="instantDeLaCompletion"/> porte un décalage horaire non nul.
    /// </exception>
    public bool Complete(DateTimeOffset instantDeLaCompletion)
    {
        ExigerUtc(instantDeLaCompletion, nameof(instantDeLaCompletion));

        if (IsCompleted)
        {
            return false;
        }

        CompletedAt = instantDeLaCompletion;

        return true;
    }

    /// <summary>
    /// Déclare une tâche pour un Chasseur.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Le Chasseur visé n'a pas d'identifiant, le titre est vide ou blanc, ou
    /// <paramref name="createdAt"/> porte un décalage horaire non nul.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Le titre rogné dépasse la largeur de sa colonne.
    /// </exception>
    public static TaskItem Create(
        Guid hunterProfileId,
        string title,
        DateOnly? dueDate,
        DateTimeOffset createdAt)
    {
        if (hunterProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Une tâche doit appartenir à un Chasseur identifié.", nameof(hunterProfileId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        // Les espaces de bordure sont invisibles dans une liste mais comptent dans la colonne :
        // le titre stocké est celui que le Chasseur lira, rogné une bonne fois.
        var titreCanonique = title.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            titreCanonique.Length, LongueurMaximaleTitre, nameof(title));

        ExigerUtc(createdAt, nameof(createdAt));

        return new TaskItem
        {
            Id = Guid.NewGuid(),
            HunterProfileId = hunterProfileId,
            Title = titreCanonique,
            DueDate = dueDate,
            CreatedAt = createdAt,
            CompletedAt = null,
        };
    }

    /// <summary>
    /// Les colonnes sont des <c>timestamp with time zone</c>, sur lesquels Npgsql refuse un
    /// <see cref="DateTimeOffset"/> décalé. Sans cette garde, un appelant qui passe
    /// <c>DateTimeOffset.Now</c> ne l'apprend qu'au <c>SaveChangesAsync</c>, loin du site fautif.
    /// Même exigence que sur <c>Habit</c> et <c>HabitLog</c>, dont cette entité est voisine à
    /// l'écran comme dans le code.
    /// </summary>
    private static void ExigerUtc(DateTimeOffset instant, string nomDuParametre)
    {
        if (instant.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "L'instant doit être en UTC (décalage nul).", nomDuParametre);
        }
    }
}
