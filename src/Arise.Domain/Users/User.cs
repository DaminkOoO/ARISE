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
    public const int LongueurMinimaleNom = 3;

    /// <summary>
    /// Borne haute du nom, portée par l'entité et pas seulement par le formulaire : c'est
    /// aussi la largeur de la colonne. La faire respecter ici évite qu'un chemin d'écriture
    /// qui ne passe pas par la commande d'inscription — seed, import, onboarding généré par
    /// un agent — construise un <see cref="User"/> que le Domain accepte et que PostgreSQL
    /// refuse au <c>SaveChangesAsync</c>, loin du site fautif.
    /// </summary>
    public const int LongueurMaximaleNom = 32;

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
    /// Le nom d'utilisateur ou l'empreinte est vide ou blanc, le nom rogné sort des bornes,
    /// ou <paramref name="registeredAt"/> porte un décalage horaire non nul.
    /// </exception>
    public static User Register(
        string username,
        string passwordHash,
        DateTimeOffset registeredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        // Les espaces de bordure sont invisibles à la saisie : sans rognage, « sung » et
        // « sung  » deviennent deux comptes distincts. Les bornes portent sur le nom rogné,
        // qui est celui du compte.
        var nomCanonique = username.Trim();

        ArgumentOutOfRangeException.ThrowIfLessThan(
            nomCanonique.Length, LongueurMinimaleNom, nameof(username));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            nomCanonique.Length, LongueurMaximaleNom, nameof(username));

        // La colonne est un timestamp with time zone, sur lequel Npgsql refuse un
        // DateTimeOffset décalé. Sans cette garde, un appelant qui passe DateTimeOffset.Now
        // ne l'apprend qu'au SaveChangesAsync, loin d'ici.
        if (registeredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "L'instant d'inscription doit être en UTC (décalage nul).",
                nameof(registeredAt));
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Username = nomCanonique,
            PasswordHash = passwordHash,
            RegisteredAt = registeredAt,
        };
    }
}
