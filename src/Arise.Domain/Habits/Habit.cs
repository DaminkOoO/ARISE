namespace Arise.Domain.Habits;

/// <summary>
/// Une habitude qu'un Chasseur s'est donnée : un nom, un rythme attendu, et l'appartenance à un
/// profil. Elle décrit une intention, pas un historique — ce qui a été fait un jour donné vivra
/// dans <c>HabitLog</c>, et la série propre à l'habitude s'en déduira (doc mécaniques,
/// section 2). Rien ici ne journalise ni ne compte.
/// </summary>
public sealed class Habit
{
    /// <summary>
    /// Borne haute du nom, portée par l'entité et pas seulement par le formulaire : c'est aussi
    /// la largeur de la colonne. La faire respecter ici évite qu'un chemin d'écriture qui ne
    /// passe pas par la commande — seed, import, suggestion d'habitudes générée par un agent —
    /// construise une <see cref="Habit"/> que le Domain accepte et que PostgreSQL refuse au
    /// <c>SaveChangesAsync</c>, loin du site fautif.
    ///
    /// <para>Plus courte que celle d'un titre de quête : le nom d'une habitude est une étiquette
    /// de liste sur un écran de 390 points de large, pas une phrase.</para>
    /// </summary>
    public const int LongueurMaximaleNom = 60;

    // EF Core matérialise par ce constructeur ; le reste du monde passe par Create.
    private Habit()
    {
    }

    public Guid Id { get; private set; }

    public Guid HunterProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public HabitFrequency Frequency { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsArchived { get; private set; }

    /// <summary>
    /// Déclare une habitude pour un Chasseur.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Le Chasseur visé n'a pas d'identifiant, le nom est vide ou blanc, ou
    /// <paramref name="createdAt"/> porte un décalage horaire non nul.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Le nom rogné dépasse la largeur de sa colonne.
    /// </exception>
    public static Habit Create(
        Guid hunterProfileId,
        string name,
        HabitFrequency frequency,
        DateTimeOffset createdAt)
    {
        if (hunterProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Une habitude doit appartenir à un Chasseur identifié.", nameof(hunterProfileId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Les espaces de bordure sont invisibles dans une liste : sans rognage, « Courir » et
        // « Courir  » deviennent deux habitudes distinctes. La borne porte donc sur le nom
        // rogné, qui est celui que l'habitude portera.
        var nomCanonique = name.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            nomCanonique.Length, LongueurMaximaleNom, nameof(name));

        // La colonne est un timestamp with time zone, sur lequel Npgsql refuse un
        // DateTimeOffset décalé. Sans cette garde, un appelant qui passe DateTimeOffset.Now ne
        // l'apprend qu'au SaveChangesAsync, loin d'ici.
        if (createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "L'instant de création doit être en UTC (décalage nul).", nameof(createdAt));
        }

        return new Habit
        {
            Id = Guid.NewGuid(),
            HunterProfileId = hunterProfileId,
            Name = nomCanonique,
            Frequency = frequency,
            CreatedAt = createdAt,
            IsArchived = false,
        };
    }
}
