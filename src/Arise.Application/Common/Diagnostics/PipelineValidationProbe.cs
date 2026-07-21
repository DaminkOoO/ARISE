using FluentValidation;
using MediatR;

namespace Arise.Application.Common.Diagnostics;

/// <summary>
/// Requête sonde, sans aucun effet métier, dont l'unique rôle est de rendre observable la
/// couture « découverte des validators + pipeline MediatR » câblée par
/// <see cref="DependencyInjection.AddApplication"/>.
///
/// <para>Pourquoi ici et non dans le projet de tests : c'est l'assembly Application que
/// <c>AddValidatorsFromAssembly</c> et <c>RegisterServicesFromAssembly</c> balaient. Une
/// sonde déclarée ailleurs ne prouverait rien de la découverte — elle passerait au vert
/// même si le balayage ne trouvait aucun validator, qui est précisément le mode de panne
/// silencieux à couvrir.</para>
///
/// <para>Le trio est <c>internal</c> : invisible hors de l'assembly, il n'est atteignable
/// par aucun endpoint et ne pèse pas sur la surface publique de la couche Application.
/// Le projet de tests y accède via <c>InternalsVisibleTo</c>.</para>
/// </summary>
internal sealed record PipelineValidationProbe(string Valeur) : IRequest<string>;

internal sealed class PipelineValidationProbeHandler
    : IRequestHandler<PipelineValidationProbe, string>
{
    internal const string Reponse = "sonde traversée";

    public Task<string> Handle(
        PipelineValidationProbe request,
        CancellationToken cancellationToken) => Task.FromResult(Reponse);
}

internal sealed class PipelineValidationProbeValidator
    : AbstractValidator<PipelineValidationProbe>
{
    internal const string MessageValeurObligatoire = "La valeur de la sonde est obligatoire.";

    public PipelineValidationProbeValidator() =>
        RuleFor(p => p.Valeur).NotEmpty().WithMessage(MessageValeurObligatoire);
}
