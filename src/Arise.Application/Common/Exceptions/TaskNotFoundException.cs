namespace Arise.Application.Common.Exceptions;

/// <summary>
/// La tâche visée n'existe pas — ou appartient à un autre Chasseur, ce qui doit se dire de la
/// même façon : distinguer les deux révélerait l'existence de la tâche d'autrui.
/// </summary>
public sealed class TaskNotFoundException()
    : Exception("Cette tâche est introuvable dans ta liste.");
