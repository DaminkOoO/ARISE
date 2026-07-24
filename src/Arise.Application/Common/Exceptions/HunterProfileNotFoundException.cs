namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le profil de Chasseur ciblé n'existe pas — l'identifiant fourni ne correspond à aucun
/// profil connu.
/// </summary>
public sealed class HunterProfileNotFoundException()
    : Exception("Ce profil de Chasseur est introuvable.");
