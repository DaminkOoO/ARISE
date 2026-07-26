namespace Arise.Domain.Habits;

/// <summary>
/// Une habitude qu'un Chasseur s'est donnée : un nom, un rythme attendu, et l'appartenance à un
/// profil. Elle décrit une intention, pas un historique — ce qui a été fait un jour donné vivra
/// dans <c>HabitLog</c>, et la série propre à l'habitude s'en déduira (doc mécaniques,
/// section 2). Rien ici ne journalise ni ne compte.
/// </summary>
public sealed class Habit
{
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
    public static Habit Create(
        Guid hunterProfileId,
        string name,
        HabitFrequency frequency,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            HunterProfileId = hunterProfileId,
            Name = name,
            Frequency = frequency,
            CreatedAt = createdAt,
            IsArchived = false,
        };
}
