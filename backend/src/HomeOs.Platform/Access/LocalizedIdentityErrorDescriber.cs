using HomeOs.Platform.Localization;
using Microsoft.AspNetCore.Identity;

namespace HomeOs.Platform.Access;

/// <summary>
/// Translates the ASP.NET Core Identity messages users actually hit (duplicate email, weak/incorrect
/// password, bad token) into the request's language via <see cref="IAppText"/>. Anything not overridden
/// falls back to the framework's English default.
/// </summary>
public sealed class LocalizedIdentityErrorDescriber(IAppText text) : IdentityErrorDescriber
{
    private IdentityError Make(string code, string key, params object[] args) =>
        new() { Code = code, Description = text.T($"identity.{key}", args) };

    /// <inheritdoc />
    public override IdentityError DuplicateEmail(string email) => Make(nameof(DuplicateEmail), "DuplicateEmail", email);

    /// <inheritdoc />
    public override IdentityError DuplicateUserName(string userName) => Make(nameof(DuplicateUserName), "DuplicateUserName", userName);

    /// <inheritdoc />
    public override IdentityError InvalidEmail(string? email) => Make(nameof(InvalidEmail), "InvalidEmail");

    /// <inheritdoc />
    public override IdentityError PasswordTooShort(int length) => Make(nameof(PasswordTooShort), "PasswordTooShort", length);

    /// <inheritdoc />
    public override IdentityError PasswordRequiresDigit() => Make(nameof(PasswordRequiresDigit), "PasswordRequiresDigit");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresLower() => Make(nameof(PasswordRequiresLower), "PasswordRequiresLower");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUpper() => Make(nameof(PasswordRequiresUpper), "PasswordRequiresUpper");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresNonAlphanumeric() => Make(nameof(PasswordRequiresNonAlphanumeric), "PasswordRequiresNonAlphanumeric");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Make(nameof(PasswordRequiresUniqueChars), "PasswordRequiresUniqueChars", uniqueChars);

    /// <inheritdoc />
    public override IdentityError PasswordMismatch() => Make(nameof(PasswordMismatch), "PasswordMismatch");

    /// <inheritdoc />
    public override IdentityError InvalidToken() => Make(nameof(InvalidToken), "InvalidToken");
}
