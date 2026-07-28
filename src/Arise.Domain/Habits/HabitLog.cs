namespace Arise.Domain.Habits;

/// <summary>
/// Une ligne de journal : « cette habitude a été tenue ce jour-là ». C'est la seule trace dont
/// la série d'une habitude se déduit (doc mécaniques, section 2 : « <c>Habit</c> a sa propre
/// série calculée depuis <c>HabitLog</c> »).
///
/// <para>L'entrée ne porte donc <b>aucun compteur</b> : la série est recalculée à la lecture par
/// <see cref="SerieDHabitude"/>. Un compteur stocké ici dériverait de son journal à la première
/// écriture concurrente ou au premier correctif de règle, et plus rien ne dirait lequel des deux
/// a raison.</para>
///
/// <para>Deux dates, et elles ne disent pas la même chose. <see cref="Day"/> est le jour <b>du
/// Chasseur</b> — celui auquel l'effort appartient, fourni par l'appelant qui seul connaît son
/// fuseau — et c'est lui que la série compte. <see cref="LoggedAt"/> n'est que l'horodatage du
/// tap. Les confondre daterait au 26 une habitude tenue le 25 et validée à 00h05.</para>
/// </summary>
public sealed class HabitLog
{
    // EF Core matérialise par ce constructeur ; le reste du monde passe par Create.
    private HabitLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid HabitId { get; private set; }

    /// <summary>
    /// Le jour du Chasseur auquel l'effort appartient — pas celui du serveur, ni celui déduit de
    /// <see cref="LoggedAt"/>.
    /// </summary>
    public DateOnly Day { get; private set; }

    public DateTimeOffset LoggedAt { get; private set; }

    /// <summary>
    /// Journalise une habitude tenue.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// L'habitude visée n'a pas d'identifiant, ou <paramref name="loggedAt"/> porte un décalage
    /// horaire non nul.
    /// </exception>
    public static HabitLog Create(Guid habitId, DateOnly day, DateTimeOffset loggedAt)
    {
        if (habitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Une entrée de journal doit viser une habitude identifiée.", nameof(habitId));
        }

        // Même garde que sur Habit.Create : la colonne est un timestamp with time zone, sur
        // lequel Npgsql refuse un DateTimeOffset décalé. Sans elle, un appelant qui passe
        // DateTimeOffset.Now ne l'apprend qu'au SaveChangesAsync, loin d'ici.
        if (loggedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "L'instant de journalisation doit être en UTC (décalage nul).", nameof(loggedAt));
        }

        return new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            Day = day,
            LoggedAt = loggedAt,
        };
    }
}
