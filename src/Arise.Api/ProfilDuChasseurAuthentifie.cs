using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;

namespace Arise.Api;

/// <summary>
/// Résout le profil de Chasseur à partir du <b>jeton</b>, et de rien d'autre.
///
/// <para>C'est le cœur de la sécurité des endpoints de domaine. Les commandes portent toutes un
/// <c>HunterProfileId</c>, et il serait tentant de le lier depuis le corps de la requête comme
/// <c>/auth/register</c> lie le sien. Ce serait une escalade horizontale immédiate : un Chasseur
/// authentifié changerait un identifiant dans le JSON et journaliserait les habitudes d'un
/// autre, cocherait ses tâches, lirait sa liste. Les contrôles d'appartenance des handlers ne
/// protègent de rien si c'est l'appelant qui déclare à qui il est.</para>
///
/// <para>Le jeton porte l'identifiant du <b>compte</b> ; c'est le compte qui dit quel profil il
/// possède (<c>User.HunterProfileId</c>).</para>
/// </summary>
internal static class ProfilDuChasseurAuthentifie
{
    /// <summary>
    /// L'identifiant du profil du Chasseur authentifié.
    /// </summary>
    /// <exception cref="UserNotFoundException">
    /// Le jeton est valide mais désigne un compte qui n'existe plus.
    /// </exception>
    /// <exception cref="HunterNotAwakenedException">
    /// Le compte est inscrit mais pas encore éveillé : il n'a pas de profil à viser.
    /// </exception>
    public static async Task<Guid> ResoudreAsync(
        ClaimsPrincipal chasseur,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        // Le claim est écrit par JwtTokenGenerator et relu sous son nom exact — MapInboundClaims
        // est désactivé côté middleware, les claims ne sont donc pas remappés vers les URI.
        var idDuCompte = Guid.Parse(chasseur.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var compte = await users.GetByIdAsync(idDuCompte, cancellationToken)
            ?? throw new UserNotFoundException();

        return compte.HunterProfileId ?? throw new HunterNotAwakenedException();
    }
}
