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

    /// <summary>Exigée à l'inscription seulement — la connexion ne rejoue pas la politique.</summary>
    public const int LongueurMinimaleMotDePasse = 8;

    /// <summary>
    /// Hacher, comme vérifier, est coûteux par construction. Les deux routes sont ouvertes
    /// sans authentification : sans plafond, un mot de passe démesuré fait payer le serveur.
    /// </summary>
    public const int LongueurMaximaleMotDePasse = 128;
}
