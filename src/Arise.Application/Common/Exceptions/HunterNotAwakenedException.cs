namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le compte n'a pas encore de profil de Chasseur : l'éveil n'a pas eu lieu.
///
/// <para>Levée par les endpoints protégés qui ont besoin d'un profil — habitudes, tâches,
/// sport — quand le jeton désigne un compte inscrit mais pas encore éveillé. Le message dit quoi
/// faire, plutôt que de constater un manque.</para>
/// </summary>
public sealed class HunterNotAwakenedException()
    : Exception("Ton éveil n'a pas encore eu lieu. Termine-le pour accéder au Système.");
