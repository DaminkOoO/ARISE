namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le nom de Chasseur demandé à l'inscription est déjà porté par un compte.
///
/// <para>Le message est affichable tel quel : il est en français (règle n°7) et dit quoi
/// faire ensuite, sans reprocher au Chasseur d'avoir mal choisi.</para>
/// </summary>
public sealed class UsernameAlreadyTakenException()
    : Exception("Ce nom de Chasseur est déjà pris. Choisis-en un autre.");
