using Arise.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace Arise.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    // Requête factice : le comportement testé est celui du pipeline, pas celui d'une
    // commande réelle du domaine.
    public sealed record RequeteFactice(string Nom) : IRequest<string>;

    private const string ReponseDuHandler = "réponse du handler";

    private static ValidationBehavior<RequeteFactice, string> Behavior(
        params IValidator<RequeteFactice>[] validators) => new(validators);

    [Fact]
    public async Task Appelle_le_handler_suivant_quand_aucun_validator_n_est_enregistre()
    {
        var behavior = Behavior();

        var resultat = await behavior.Handle(
            new RequeteFactice("Sung Jin-Woo"),
            _ => Task.FromResult(ReponseDuHandler),
            CancellationToken.None);

        resultat.Should().Be(ReponseDuHandler);
    }

    [Fact]
    public async Task Appelle_le_handler_suivant_quand_la_validation_passe()
    {
        var behavior = Behavior(new ValidatorQuiPasse());

        var resultat = await behavior.Handle(
            new RequeteFactice("Sung Jin-Woo"),
            _ => Task.FromResult(ReponseDuHandler),
            CancellationToken.None);

        resultat.Should().Be(ReponseDuHandler);
    }

    [Fact]
    public async Task Leve_une_ValidationException_quand_la_validation_echoue()
    {
        var behavior = Behavior(new ValidatorQuiEchoue());

        var acte = async () => await behavior.Handle(
            new RequeteFactice(""),
            _ => Task.FromResult(ReponseDuHandler),
            CancellationToken.None);

        await acte.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task N_appelle_pas_le_handler_suivant_quand_la_validation_echoue()
    {
        var behavior = Behavior(new ValidatorQuiEchoue());
        var handlerAppele = false;

        var acte = async () => await behavior.Handle(
            new RequeteFactice(""),
            _ =>
            {
                handlerAppele = true;
                return Task.FromResult(ReponseDuHandler);
            },
            CancellationToken.None);

        await acte.Should().ThrowAsync<ValidationException>();
        handlerAppele.Should().BeFalse();
    }

    [Fact]
    public async Task Agrege_les_erreurs_de_tous_les_validators_enregistres()
    {
        var behavior = Behavior(new ValidatorQuiEchoue(), new AutreValidatorQuiEchoue());

        var acte = async () => await behavior.Handle(
            new RequeteFactice(""),
            _ => Task.FromResult(ReponseDuHandler),
            CancellationToken.None);

        var exception = await acte.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Remonte_le_message_d_erreur_du_validator()
    {
        var behavior = Behavior(new ValidatorQuiEchoue());

        var acte = async () => await behavior.Handle(
            new RequeteFactice(""),
            _ => Task.FromResult(ReponseDuHandler),
            CancellationToken.None);

        var exception = await acte.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(ValidatorQuiEchoue.Message);
    }

    private sealed class ValidatorQuiPasse : AbstractValidator<RequeteFactice>
    {
        public ValidatorQuiPasse() => RuleFor(r => r.Nom).NotEmpty();
    }

    private sealed class ValidatorQuiEchoue : AbstractValidator<RequeteFactice>
    {
        public const string Message = "Le nom du Chasseur est obligatoire.";

        public ValidatorQuiEchoue() => RuleFor(r => r.Nom).NotEmpty().WithMessage(Message);
    }

    private sealed class AutreValidatorQuiEchoue : AbstractValidator<RequeteFactice>
    {
        public AutreValidatorQuiEchoue() =>
            RuleFor(r => r.Nom).MinimumLength(3)
                .WithMessage("Le nom du Chasseur doit contenir au moins 3 caractères.");
    }
}
