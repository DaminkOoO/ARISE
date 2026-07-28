namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le compte porte déjà un profil de Chasseur : l'éveil a déjà eu lieu.
///
/// <para>Ce n'est pas un reproche (règle n°5) — c'est un renvoi vers ce que le Chasseur cherche
/// probablement.</para>
/// </summary>
public sealed class HunterAlreadyAwakenedException()
    : Exception("Tu es déjà éveillé, Chasseur. Ton profil t'attend.");
