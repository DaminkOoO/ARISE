using Arise.Domain.Users;

namespace Arise.Application.Features.Auth;

/// <summary>
/// Les bornes que l'inscription et la connexion doivent lire au même endroit.
///
/// <para>Les bornes du <b>nom</b> ne sont pas ici : ce sont des invariantes de l'entité, et
/// elles vivent sur <see cref="User"/> (<see cref="User.LongueurMinimaleNom"/>,
/// <see cref="User.LongueurMaximaleNom"/>) — c'est le Domain qui les fait respecter, sur
/// tout chemin d'écriture et pas seulement à travers ce pipeline. Ce type ne garde que ce
/// qui est réellement une politique de couche Application.</para>
///
/// <para>Le mot de passe, lui, n'atteint jamais le Domain : celui-ci ne connaît que
/// l'empreinte. Ses bornes n'ont donc aucun autre endroit où vivre.</para>
/// </summary>
public static class PolitiqueIdentifiants
{
    /// <summary>Exigée à l'inscription seulement — la connexion ne rejoue pas la politique.</summary>
    public const int LongueurMinimaleMotDePasse = 8;

    /// <summary>
    /// Hacher, comme vérifier, est coûteux par construction. Les deux routes sont ouvertes
    /// sans authentification : sans plafond, un mot de passe démesuré fait payer le serveur.
    /// </summary>
    public const int LongueurMaximaleMotDePasse = 128;

    /// <summary>
    /// Plafond du nom <b>avant rognage</b>.
    ///
    /// <para>Les bornes de <see cref="User"/> portent sur le nom rogné, qui est celui du
    /// compte. Sans ce plafond-ci, un nom précédé de 25 Mo d'espaces traverse la validation
    /// au vert : chaque <c>Trim()</c> du chemin — validator, handler, entité — en alloue une
    /// copie, sur une route ouverte sans authentification.</para>
    ///
    /// <para>La marge est large à dessein : quelques espaces de bordure sont une saisie
    /// ordinaire, pas une attaque. Ce plafond n'est pas une règle de nommage — c'est une
    /// borne d'allocation, et c'est pourquoi il vit ici et non sur l'entité.</para>
    /// </summary>
    public const int LongueurMaximaleNomBrut = User.LongueurMaximaleNom + 64;
}
