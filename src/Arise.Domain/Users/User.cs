namespace Arise.Domain.Users;

/// <summary>
/// Compte d'un Chasseur : ce avec quoi il s'authentifie, et rien d'autre. Sa progression
/// (XP, niveau, rang, séries) vit dans son profil, pas ici.
///
/// <para>Le mot de passe en clair n'entre jamais dans ce type. L'empreinte est calculée en
/// amont — la couche Domain n'a pas de dépendance de hachage, et ne peut donc pas se
/// tromper de sens : elle reçoit une empreinte, jamais un secret à protéger.</para>
/// </summary>
public sealed class User
{
    // EF Core matérialise par ce constructeur ; le reste du monde passe par Register.
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>
    /// Inscrit un Chasseur. <paramref name="passwordHash"/> est une empreinte déjà calculée :
    /// y passer un mot de passe en clair le stockerait tel quel, sans que rien ne proteste.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Le nom d'utilisateur ou l'empreinte est vide ou blanc.
    /// </exception>
    public static User Register(
        string username,
        string passwordHash,
        DateTimeOffset registeredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            // Les espaces de bordure sont invisibles à la saisie : sans rognage, « sung » et
            // « sung  » deviennent deux comptes distincts.
            Username = username.Trim(),
            PasswordHash = passwordHash,
            RegisteredAt = registeredAt,
        };
    }
}
