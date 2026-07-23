namespace Arise.Application.Common.Exceptions;

/// <summary>
/// La connexion a échoué. Le message ne dit délibérément pas <b>lequel</b> des deux champs
/// est en cause : deux messages distincts diraient à qui essaie des noms au hasard lesquels
/// correspondent à un compte.
///
/// <para>Le ton reste neutre — un échec de connexion est une faute de frappe bien plus
/// souvent qu'une intrusion.</para>
/// </summary>
public sealed class InvalidCredentialsException()
    : Exception("Nom de Chasseur ou mot de passe incorrect.");
