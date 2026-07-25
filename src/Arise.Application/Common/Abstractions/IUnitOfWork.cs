namespace Arise.Application.Common.Abstractions;

/// <summary>
/// La transaction, vue de la couche Application : commencer, valider, annuler — et rien de
/// l'outil qui l'exécute. Implémentée dans Infrastructure par-dessus EF Core, comme les
/// repositories : ni <c>DbContext</c> ni <c>Microsoft.EntityFrameworkCore</c> ne remontent ici.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// <see langword="true"/> lorsqu'une transaction est déjà ouverte sur ce scope.
    ///
    /// <para>C'est ce que lit <c>TransactionBehavior</c> pour savoir s'il est la commande la
    /// plus externe. L'implémentation vit à l'échelle du scope de la requête — même durée de
    /// vie que le <c>DbContext</c> — donc une commande imbriquée, envoyée par MediatR dans ce
    /// même scope, interroge bien la même instance et voit la transaction de sa parente.</para>
    /// </summary>
    bool TransactionEnCours { get; }

    Task CommencerAsync(CancellationToken cancellationToken);

    Task ValiderAsync(CancellationToken cancellationToken);

    Task AnnulerAsync(CancellationToken cancellationToken);
}
