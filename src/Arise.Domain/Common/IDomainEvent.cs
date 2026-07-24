namespace Arise.Domain.Common;

/// <summary>
/// Marqueur pour un fait qui s'est produit dans le domaine (ex. un passage de rang). Le
/// Domain ne référence rien, donc ce marqueur ne dépend pas de MediatR — c'est à la couche
/// Application de publier les événements accumulés sur une entité, pas au domaine de savoir
/// comment ils voyagent.
/// </summary>
public interface IDomainEvent
{
}
