namespace Arise.Application.Common.Exceptions;

/// <summary>
/// L'habitude visée n'existe pas — ou appartient à un autre Chasseur, ce qui doit se dire de la
/// même façon : distinguer les deux révélerait l'existence de l'habitude d'autrui.
/// </summary>
public sealed class HabitNotFoundException()
    : Exception("Cette habitude est introuvable dans ta liste.");
