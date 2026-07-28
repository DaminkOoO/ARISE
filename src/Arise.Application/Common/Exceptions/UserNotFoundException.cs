namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le compte visé n'existe pas.
///
/// <para>Sur un endpoint protégé, cela signifie qu'un jeton parfaitement valide désigne un compte
/// supprimé depuis son émission — le cas est rare mais réel, et il vaut mieux le dire que de
/// laisser remonter un 500.</para>
/// </summary>
public sealed class UserNotFoundException()
    : Exception("Ce compte de Chasseur est introuvable.");
