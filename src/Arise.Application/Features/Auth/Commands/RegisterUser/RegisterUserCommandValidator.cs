using System.Text.RegularExpressions;
using Arise.Domain.Users;
using FluentValidation;

namespace Arise.Application.Features.Auth.Commands.RegisterUser;

/// <summary>
/// Contrôles de forme de l'inscription. Les messages sont écrits explicitement plutôt que
/// laissés aux gabarits par défaut : ils remontent jusqu'à l'écran (règle n°7) et doivent
/// dire quoi corriger, pas seulement que c'est refusé.
/// </summary>
public sealed partial class RegisterUserCommandValidator
    : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        // Sans Stop, un nom vide échoue à la fois sur NotEmpty et sur la longueur minimale,
        // et l'écran affiche deux reproches pour une seule case à remplir. Stop garantit
        // aussi aux prédicats suivants que le nom n'est ni null ni blanc.
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Les contrôles portent sur le nom rogné, car c'est celui que le handler
        // enregistrera : mesurer le brut accepterait « ab  » pour un compte « ab » trop
        // court, et refuserait « sung  » que le handler aurait accepté.
        RuleFor(commande => commande.Username)
            .Must(nom => !string.IsNullOrWhiteSpace(nom))
                .WithMessage("Le nom de Chasseur est obligatoire.")
            // Avant tout Trim() : c'est ce contrôle qui empêche d'allouer une copie d'un nom
            // démesuré, il ne doit donc rien allouer lui-même.
            .Must(nom => nom.Length <= PolitiqueIdentifiants.LongueurMaximaleNomBrut)
                .WithMessage(
                    "Le nom de Chasseur ne peut pas dépasser "
                    + $"{User.LongueurMaximaleNom} caractères.")
            .Must(nom => nom.Trim().Length >= User.LongueurMinimaleNom)
                .WithMessage(
                    "Le nom de Chasseur doit contenir au moins "
                    + $"{User.LongueurMinimaleNom} caractères.")
            .Must(nom => nom.Trim().Length <= User.LongueurMaximaleNom)
                .WithMessage(
                    "Le nom de Chasseur ne peut pas dépasser "
                    + $"{User.LongueurMaximaleNom} caractères.")
            .Must(nom => NomAutorise().IsMatch(nom.Trim()))
                .WithMessage(
                    "Le nom de Chasseur ne peut contenir que des lettres, des chiffres, "
                    + "des tirets et des tirets bas.");

        // Pas de rognage ici : les espaces peuvent être délibérés dans une phrase de passe,
        // les rogner amputerait le secret sans le dire, et la connexion échouerait ensuite
        // sans explication.
        RuleFor(commande => commande.Password)
            .NotEmpty()
                .WithMessage("Le mot de passe est obligatoire.")
            .MinimumLength(PolitiqueIdentifiants.LongueurMinimaleMotDePasse)
                .WithMessage(
                    "Le mot de passe doit contenir au moins "
                    + $"{PolitiqueIdentifiants.LongueurMinimaleMotDePasse} caractères.")
            .MaximumLength(PolitiqueIdentifiants.LongueurMaximaleMotDePasse)
                .WithMessage(
                    "Le mot de passe ne peut pas dépasser "
                    + $"{PolitiqueIdentifiants.LongueurMaximaleMotDePasse} caractères.");
    }

    /// <summary>
    /// <c>\p{L}</c> plutôt que <c>a-z</c> : l'app est française, « Séraphin » est un nom
    /// légitime. L'espace reste exclu — il rend deux noms indiscernables à l'œil dans une
    /// liste, et le nom sert d'identifiant de connexion.
    /// </summary>
    [GeneratedRegex(@"^[\p{L}\p{N}_-]+$")]
    private static partial Regex NomAutorise();
}
