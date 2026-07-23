namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Hachage des mots de passe. L'algorithme vit dans la couche Infrastructure — la couche
/// Application ne sait pas lequel, et n'a donc aucun moyen de le contourner.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Empreinte d'un mot de passe en clair, sel compris.</summary>
    string Hash(string password);

    /// <summary>
    /// Le mot de passe en clair correspond-il à cette empreinte ? Renvoie <c>false</c> sur
    /// une empreinte illisible plutôt que de lever : une empreinte corrompue en base est un
    /// échec d'authentification, pas une panne du serveur.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
