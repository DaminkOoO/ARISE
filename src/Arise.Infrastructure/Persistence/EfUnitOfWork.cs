using Arise.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// L'unité de travail d'Application, posée sur la transaction du <see cref="AriseDbContext"/>.
/// Rien de plus : la transaction est celle d'EF Core, et les <c>SaveChangesAsync</c> que les
/// repositories exécutent entre-temps la rejoignent d'eux-mêmes — ils partagent la connexion.
///
/// <para>C'est aussi ce qui protège le rejeu de concurrence des handlers : quand une
/// transaction est déjà ouverte, EF Core pose un <b>point de sauvegarde</b> avant chaque
/// <c>SaveChangesAsync</c> et y revient si l'écriture échoue. Une violation d'unicité — qui,
/// elle, abandonne bel et bien la transaction PostgreSQL — est donc défaite jusqu'à ce point
/// plutôt que de rendre la transaction inutilisable pour la suite du handler.
/// <c>ConflitsEnTransactionSurPostgresTests</c> tient les deux cas sous surveillance.</para>
/// </summary>
internal sealed class EfUnitOfWork(AriseDbContext context) : IUnitOfWork
{
    // Lu sur le DbContext plutôt que tenu dans un drapeau de cette classe : la transaction
    // appartient à EF Core, et un drapeau parallèle finirait par diverger de la réalité — un
    // rollback déclenché ailleurs, et l'unité de travail continuerait de croire à sa
    // transaction.
    public bool TransactionEnCours => context.Database.CurrentTransaction is not null;

    public Task CommencerAsync(CancellationToken cancellationToken) =>
        context.Database.BeginTransactionAsync(cancellationToken);

    public Task ValiderAsync(CancellationToken cancellationToken) =>
        context.Database.CommitTransactionAsync(cancellationToken);

    public Task AnnulerAsync(CancellationToken cancellationToken) =>
        context.Database.RollbackTransactionAsync(cancellationToken);
}
