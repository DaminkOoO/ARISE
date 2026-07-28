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
    /// Le profil de progression de ce compte, ou <see langword="null"/> tant que le Chasseur ne
    /// s'est pas éveillé.
    ///
    /// <para>La liaison est portée <b>ici</b> et non sur <c>HunterProfile</c>, parce que c'est de
    /// ce côté-là qu'elle est facultative : un compte existe dès l'inscription et vit sans profil
    /// jusqu'à l'onboarding, là où un profil sans compte n'a aucun sens. Poser la clé sur le
    /// profil aurait imposé une colonne obligatoire à une relation qui ne l'est pas encore.</para>
    ///
    /// <para>C'est aussi ce qui rend les endpoints sûrs : le profil visé se déduit du jeton, et
    /// n'est jamais annoncé par l'appelant — sans quoi n'importe quel Chasseur authentifié
    /// agirait sur les habitudes et les tâches d'un autre en changeant un identifiant dans le
    /// corps de la requête.</para>
    /// </summary>
    public Guid? HunterProfileId { get; private set; }

    /// <summary>
    /// Rattache le profil fraîchement éveillé à ce compte.
    /// </summary>
    /// <exception cref="ArgumentException">Le profil n'a pas d'identifiant.</exception>
    /// <exception cref="InvalidOperationException">
    /// Le compte porte déjà un profil. Un second éveil ne se produit pas dans le parcours normal
    /// — le laisser passer écraserait silencieusement toute la progression du Chasseur.
    /// </exception>
    public void RattacherLeProfil(Guid hunterProfileId)
    {
        if (hunterProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Le profil rattaché doit être identifié.", nameof(hunterProfileId));
        }

        if (HunterProfileId is { } dejaRattache)
        {
            // Idempotent sur le même profil : un renvoi réseau de l'onboarding ne doit pas
            // échouer. Sur un profil différent, en revanche, c'est un défaut à faire remonter.
            if (dejaRattache == hunterProfileId)
            {
                return;
            }

            throw new InvalidOperationException(
                "Ce compte porte déjà un profil de Chasseur.");
        }

        HunterProfileId = hunterProfileId;
    }

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
