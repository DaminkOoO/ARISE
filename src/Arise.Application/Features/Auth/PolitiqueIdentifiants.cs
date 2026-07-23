namespace Arise.Application.Features.Auth;

/// <summary>
/// Les bornes que l'inscription et la connexion doivent lire au même endroit.
///
/// <para>Le plafond du mot de passe est la raison d'être de ce type : il vaut pour les deux
/// commandes, et deux valeurs qui divergeraient laisseraient un mot de passe inscriptible
/// mais plus utilisable pour se connecter. Le faire vivre chez l'une des deux obligerait
/// l'autre à venir l'y chercher.</para>
/// </summary>
public static class PolitiqueIdentifiants
{
    public const int LongueurMinimaleNom = 3;
    public const int LongueurMaximaleNom = 32;

    /// <summary>
    /// Plafond du nom <b>avant rognage</b>.
    ///
    /// <para>Les bornes ci-dessus portent sur le nom rogné, qui est celui du compte. Sans ce
    /// plafond-ci, un nom précédé de 25 Mo d'espaces traverse la validation au vert : chaque
    /// <c>Trim()</c> du chemin — validator, handler, entité — en alloue une copie, sur une
    /// route ouverte sans authentification. C'est l'argument qui plafonne déjà le mot de
    /// passe, appliqué au nom.</para>
    ///
    /// <para>La marge est large à dessein : quelques espaces de bordure sont une saisie
    /// ordinaire, pas une attaque. Ce plafond n'est pas une règle de nommage, c'est une
    /// borne d'allocation — d'où l'écart avec <see cref="LongueurMaximaleNom"/>.</para>
    /// </summary>
    public const int LongueurMaximaleNomBrut = LongueurMaximaleNom + 64;

    /// <summary>Exigée à l'inscription seulement — la connexion ne rejoue pas la politique.</summary>
    public const int LongueurMinimaleMotDePasse = 8;

    /// <summary>
    /// Hacher, comme vérifier, est coûteux par construction. Les deux routes sont ouvertes
    /// sans authentification : sans plafond, un mot de passe démesuré fait payer le serveur.
    /// </summary>
    public const int LongueurMaximaleMotDePasse = 128;
}
