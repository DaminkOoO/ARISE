using Arise.Application.Common.Abstractions;
using IdentityHasher = Microsoft.AspNetCore.Identity.PasswordHasher<object>;
using PasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Arise.Infrastructure.Auth;

/// <summary>
/// Hachage salé bâti sur <c>PasswordHasher&lt;T&gt;</c> d'ASP.NET Core Identity — le hacheur
/// autonome voulu par CLAUDE.md, sans fournisseur d'identité externe.
///
/// <para>Identity paramètre son empreinte par un <typeparamref>TUser</typeparamref> que son
/// implémentation par défaut n'inspecte jamais : un jeton unique et immuable suffit, aucun
/// Chasseur réel n'a à être fabriqué ici.</para>
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    // Le compte passé à Identity n'entre pas dans le calcul de l'empreinte par défaut ; une
    // sentinelle partagée évite d'en allouer une par appel.
    private static readonly object Compte = new();

    private readonly IdentityHasher hasher = new();

    public string Hash(string password) => hasher.HashPassword(Compte, password);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            // SuccessRehashNeeded compte aussi comme un succès : le mot de passe est bon, seul
            // le format de stockage a vieilli.
            return hasher.VerifyHashedPassword(Compte, passwordHash, password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Empreinte au base64 malformé : Identity lève ici. Une empreinte illisible en
            // base est un échec d'authentification, pas une panne serveur (contrat).
            return false;
        }
    }
}
