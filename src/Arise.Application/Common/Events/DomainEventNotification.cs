using Arise.Domain.Common;
using MediatR;

namespace Arise.Application.Common.Events;

/// <summary>
/// Le pont entre un <see cref="IDomainEvent"/> (Domain, qui ne référence pas MediatR) et une
/// notification MediatR publiable (Application, qui le référence). Un événement de domaine
/// accumulé sur une entité est enveloppé dans cette notification au moment de la publication
/// — l'entité elle-même ne sait jamais comment ses faits voyagent.
/// </summary>
public sealed record DomainEventNotification(IDomainEvent DomainEvent) : INotification;
