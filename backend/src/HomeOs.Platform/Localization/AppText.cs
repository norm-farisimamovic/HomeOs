using System.Globalization;

namespace HomeOs.Platform.Localization;

/// <summary>
/// Lightweight localization for server-produced text — API error titles and outgoing emails.
/// Two entry points: the indexer/<see cref="T(string,object[])"/> resolve in the <em>current request</em>
/// culture (set by the request-localization middleware from the frontend's <c>Accept-Language</c>),
/// while <see cref="T(string,string,object[])"/> resolves in an explicit culture — used for emails,
/// which must be written in the <em>recipient's</em> language, not the sender's.
/// </summary>
public interface IAppText
{
    /// <summary>Resolves <paramref name="key"/> in the current request culture.</summary>
    string this[string key] { get; }

    /// <summary>Resolves <paramref name="key"/> in the current request culture, formatting <paramref name="args"/>.</summary>
    string T(string key, params object[] args);

    /// <summary>Resolves <paramref name="key"/> in a specific <paramref name="culture"/> (e.g. an email recipient's).</summary>
    string T(string culture, string key, params object[] args);

    /// <summary>Wraps localized content in the branded Home OS email layout (inline styles, email-client safe).</summary>
    string EmailHtml(string culture, string greeting, string bodyLine, string ctaLabel, string ctaUrl, bool showRawLink);
}

/// <inheritdoc />
public sealed class AppText : IAppText
{
    private const string DefaultCulture = "bs";

    /// <inheritdoc />
    public string this[string key] => T(key);

    /// <inheritdoc />
    public string T(string key, params object[] args) =>
        T(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, key, args);

    /// <inheritdoc />
    public string T(string culture, string key, params object[] args)
    {
        var lang = Strings.ContainsKey(culture) ? culture : DefaultCulture;
        // Prefer the requested language; fall back to the default language, then to the raw key.
        var value = Lookup(lang, key) ?? Lookup(DefaultCulture, key) ?? key;
        return args.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, args);
    }

    /// <inheritdoc />
    public string EmailHtml(string culture, string greeting, string bodyLine, string ctaLabel, string ctaUrl, bool showRawLink)
    {
        var rawLink = showRawLink
            ? $"<p style=\"margin:22px 0 0;color:#8a938e;font-size:12.5px;\">{T(culture, "email.fallbackLine")}<br>" +
              $"<a href=\"{ctaUrl}\" style=\"color:#24685A;word-break:break-all;\">{ctaUrl}</a></p>"
            : string.Empty;

        return
            "<div style=\"background:#f4f6f5;padding:24px 0;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;\">" +
              "<div style=\"max-width:520px;margin:0 auto;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #e5e9e8;\">" +
                "<div style=\"background:#24685A;padding:18px 28px;\">" +
                  "<span style=\"color:#ffffff;font-size:18px;font-weight:700;letter-spacing:.2px;\">Home OS</span>" +
                "</div>" +
                "<div style=\"padding:28px;color:#1f2723;font-size:15px;line-height:1.6;\">" +
                  $"<p style=\"margin:0 0 12px;font-weight:600;\">{greeting}</p>" +
                  $"<p style=\"margin:0 0 22px;color:#42504a;\">{bodyLine}</p>" +
                  $"<a href=\"{ctaUrl}\" style=\"display:inline-block;background:#24685A;color:#ffffff;text-decoration:none;padding:11px 20px;border-radius:9px;font-weight:600;font-size:14px;\">{ctaLabel}</a>" +
                  rawLink +
                "</div>" +
                $"<div style=\"padding:16px 28px;border-top:1px solid #eef1f0;color:#8a938e;font-size:12px;\">{T(culture, "email.footer")}</div>" +
              "</div>" +
            "</div>";
    }

    private static string? Lookup(string culture, string key) =>
        Strings.TryGetValue(culture, out var map) && map.TryGetValue(key, out var value) ? value : null;

    // Server-side strings. Keep in sync conceptually with the frontend locale files, but only what the
    // server actually emits lives here (errors + emails + Identity messages). Placeholders use {0}, {1}…
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["bs"] = new Dictionary<string, string>
            {
                // Auth / validation error titles
                ["error.register.required"] = "Email, lozinka, ime i naziv domaćinstva su obavezni.",
                ["error.confirm.invalid"] = "Ovaj link za potvrdu nije važeći.",
                ["error.confirm.expired"] = "Link za potvrdu nije važeći ili je istekao.",
                ["error.login.unconfirmed"] = "Potvrdite svoj email prije prijave.",
                ["error.login.invalid"] = "Pogrešan email ili lozinka.",
                ["error.auth.unauthorized"] = "Niste prijavljeni. Prijavite se i pokušajte ponovo.",
                ["error.auth.forbidden"] = "Nemate dozvolu za ovu radnju.",
                ["error.app.disabled"] = "Ova aplikacija je isključena za vaše domaćinstvo.",
                ["error.app.capabilityDenied"] = "Ova aplikacija nema dozvolu za tu radnju.",
                ["error.app.core"] = "Osnovne aplikacije se ne mogu isključiti.",
                ["error.profile.nameRequired"] = "Ime je obavezno.",
                ["error.avatar.tooLarge"] = "Slika je prevelika (maksimalno 2 MB).",
                ["error.avatar.notImage"] = "Fajl mora biti slika.",
                ["error.attachment.tooLarge"] = "Fajl je prevelik (maksimalno 10 MB).",
                ["error.invite.emailRequired"] = "Email i ime su obavezni.",
                ["error.invite.unknownRole"] = "Nepoznata uloga.",
                ["error.invite.exists"] = "Ta osoba je već član domaćinstva.",
                ["error.member.removeSelf"] = "Ne možete ukloniti sami sebe.",
                ["error.member.removeOwner"] = "Ne možete ukloniti vlasnika domaćinstva.",
                ["error.invite.accountExists"] = "Nalog s ovim emailom već postoji.",
                ["error.tasks.titleRequired"] = "Naslov je obavezan.",
                ["error.tasks.titleTooLong"] = "Naslov mora imati manje od 200 znakova.",
                ["error.tasks.detailsTooLong"] = "Detalji moraju imati manje od 2000 znakova.",
                ["error.finance.txRequired"] = "Iznos (veći od 0) i kategorija su obavezni.",
                ["error.finance.billRequired"] = "Naziv i iznos (veći od 0) su obavezni.",
                ["error.calendar.required"] = "Naziv i datum su obavezni.",
                ["error.reminder.required"] = "Naziv i datum su obavezni.",
                ["error.note.required"] = "Naziv je obavezan.",
                ["error.life.required"] = "Naziv je obavezan.",
                ["reminder.expiry"] = "Ističe uskoro: {0}",
                ["reminder.tomorrow"] = "Sutra",
                ["reminder.inDays"] = "Za {0} dana",
                ["bill.dueSoon"] = "Račun uskoro dospijeva: {0}",
                ["bill.dueToday"] = "{0} dospijeva danas",
                ["bill.dueTomorrow"] = "{0} dospijeva sutra",
                ["bill.dueInDays"] = "{0} dospijeva za {1} dana",
                ["digest.subject"] = "Vaš pregled — šta vas čeka",
                ["digest.greeting"] = "Zdravo {0} 👋",
                ["digest.intro"] = "Evo šta vas čeka u narednim danima ({0}):",
                ["digest.cta"] = "Otvori Home OS",
                ["digest.kind.task"] = "Zadatak",
                ["digest.kind.bill"] = "Račun",
                ["digest.kind.reminder"] = "Podsjetnik",
                ["shared.title"] = "{0} je s vama podijelio/la: {1}",
                ["chat.mentionTitle"] = "{0} vas je spomenuo/la u chatu",
                ["chat.assistantOff"] = "AI pomoćnik još nije podešen. Vlasnik domaćinstva ga može uključiti dodavanjem besplatnog ključa (Groq ili Gemini) u postavke.",

                // Identity (account/password) messages
                ["identity.DuplicateEmail"] = "Email „{0}“ je već zauzet.",
                ["identity.DuplicateUserName"] = "Korisnik „{0}“ već postoji.",
                ["identity.InvalidEmail"] = "Email nije ispravan.",
                ["identity.PasswordTooShort"] = "Lozinka mora imati najmanje {0} znakova.",
                ["identity.PasswordRequiresDigit"] = "Lozinka mora sadržavati barem jednu cifru.",
                ["identity.PasswordRequiresLower"] = "Lozinka mora sadržavati barem jedno malo slovo.",
                ["identity.PasswordRequiresUpper"] = "Lozinka mora sadržavati barem jedno veliko slovo.",
                ["identity.PasswordRequiresNonAlphanumeric"] = "Lozinka mora sadržavati barem jedan poseban znak.",
                ["identity.PasswordRequiresUniqueChars"] = "Lozinka mora sadržavati barem {0} različitih znakova.",
                ["identity.PasswordMismatch"] = "Trenutna lozinka nije tačna.",
                ["identity.InvalidToken"] = "Kôd nije ispravan ili je istekao.",

                // Emails — shared
                ["email.footer"] = "Home OS — životna administracija vašeg doma na jednom mjestu.",
                ["email.fallbackLine"] = "Ako dugme ne radi, otvorite ovaj link:",

                // Email — email confirmation
                ["email.confirm.subject"] = "Potvrdite svoj Home OS email",
                ["email.confirm.greeting"] = "Zdravo {0},",
                ["email.confirm.line"] = "Potvrdite svoj email da aktivirate svoje Home OS domaćinstvo.",
                ["email.confirm.cta"] = "Potvrdi email",

                // Email — generic notification
                ["email.notify.greeting"] = "Zdravo {0},",
                ["email.notify.cta"] = "Otvori Home OS",

                // Email — password reset
                ["email.reset.subject"] = "Resetovanje lozinke — Home OS",
                ["email.reset.greeting"] = "Zdravo {0},",
                ["email.reset.line"] = "Zatražili ste promjenu lozinke. Kliknite ispod da postavite novu (ako to niste bili vi, zanemarite ovaj email).",
                ["email.reset.cta"] = "Postavi novu lozinku",

                // Email — household invite
                ["email.invite.subject"] = "Pozvani ste u domaćinstvo {0} na Home OS",
                ["email.invite.greeting"] = "Zdravo {0},",
                ["email.invite.line"] = "Pozvani ste da se pridružite domaćinstvu {0} na Home OS-u.",
                ["email.invite.cta"] = "Prihvati pozivnicu",

                // Email — task assigned
                ["email.task.subject"] = "Novi zadatak: {0}",
                ["email.task.greeting"] = "Zdravo {0},",
                ["email.task.line"] = "Dodijeljen vam je novi zadatak: {0}.",
                ["email.task.due"] = "Rok je {0}.",
                ["email.task.cta"] = "Otvori zadatke",
            },
            ["en"] = new Dictionary<string, string>
            {
                ["error.register.required"] = "Email, password, name and household name are required.",
                ["error.confirm.invalid"] = "This confirmation link isn't valid.",
                ["error.confirm.expired"] = "This confirmation link is invalid or has expired.",
                ["error.login.unconfirmed"] = "Please confirm your email before signing in.",
                ["error.login.invalid"] = "Incorrect email or password.",
                ["error.auth.unauthorized"] = "You're not signed in. Please sign in and try again.",
                ["error.auth.forbidden"] = "You don't have permission to do this.",
                ["error.app.disabled"] = "This app is turned off for your household.",
                ["error.app.capabilityDenied"] = "This app doesn't have permission for that action.",
                ["error.app.core"] = "Core apps can't be turned off.",
                ["error.profile.nameRequired"] = "Name is required.",
                ["error.avatar.tooLarge"] = "The image is too large (max 2 MB).",
                ["error.avatar.notImage"] = "The file must be an image.",
                ["error.attachment.tooLarge"] = "The file is too large (max 10 MB).",
                ["error.invite.emailRequired"] = "Email and name are required.",
                ["error.invite.unknownRole"] = "Unknown role.",
                ["error.invite.exists"] = "That person is already a member of the household.",
                ["error.member.removeSelf"] = "You can't remove yourself.",
                ["error.member.removeOwner"] = "You can't remove the household owner.",
                ["error.invite.accountExists"] = "An account with that email already exists.",
                ["error.tasks.titleRequired"] = "A title is required.",
                ["error.tasks.titleTooLong"] = "Keep the title under 200 characters.",
                ["error.tasks.detailsTooLong"] = "Keep details under 2000 characters.",
                ["error.finance.txRequired"] = "Amount (greater than 0) and a category are required.",
                ["error.finance.billRequired"] = "A name and amount (greater than 0) are required.",
                ["error.calendar.required"] = "A title and date are required.",
                ["error.reminder.required"] = "A title and date are required.",
                ["error.note.required"] = "A title is required.",
                ["error.life.required"] = "A title is required.",
                ["reminder.expiry"] = "Expiring soon: {0}",
                ["reminder.tomorrow"] = "Tomorrow",
                ["reminder.inDays"] = "In {0} days",
                ["bill.dueSoon"] = "Bill due soon: {0}",
                ["bill.dueToday"] = "{0} is due today",
                ["bill.dueTomorrow"] = "{0} is due tomorrow",
                ["bill.dueInDays"] = "{0} is due in {1} days",
                ["digest.subject"] = "Your digest — what's coming up",
                ["digest.greeting"] = "Hi {0} 👋",
                ["digest.intro"] = "Here's what's coming up over the next few days ({0}):",
                ["digest.cta"] = "Open Home OS",
                ["digest.kind.task"] = "Task",
                ["digest.kind.bill"] = "Bill",
                ["digest.kind.reminder"] = "Reminder",
                ["shared.title"] = "{0} shared with you: {1}",
                ["chat.mentionTitle"] = "{0} mentioned you in chat",
                ["chat.assistantOff"] = "The AI assistant isn't set up yet. A household owner can enable it by adding a free key (Groq or Gemini) in settings.",

                ["identity.DuplicateEmail"] = "Email “{0}” is already taken.",
                ["identity.DuplicateUserName"] = "User “{0}” already exists.",
                ["identity.InvalidEmail"] = "That email address isn't valid.",
                ["identity.PasswordTooShort"] = "Password must be at least {0} characters.",
                ["identity.PasswordRequiresDigit"] = "Password must contain at least one digit.",
                ["identity.PasswordRequiresLower"] = "Password must contain at least one lowercase letter.",
                ["identity.PasswordRequiresUpper"] = "Password must contain at least one uppercase letter.",
                ["identity.PasswordRequiresNonAlphanumeric"] = "Password must contain at least one special character.",
                ["identity.PasswordRequiresUniqueChars"] = "Password must use at least {0} different characters.",
                ["identity.PasswordMismatch"] = "Your current password is incorrect.",
                ["identity.InvalidToken"] = "That code is invalid or has expired.",

                ["email.footer"] = "Home OS — your household's life admin in one place.",
                ["email.fallbackLine"] = "If the button doesn't work, open this link:",

                ["email.confirm.subject"] = "Confirm your Home OS email",
                ["email.confirm.greeting"] = "Hi {0},",
                ["email.confirm.line"] = "Confirm your email to activate your Home OS household.",
                ["email.confirm.cta"] = "Confirm my email",

                ["email.notify.greeting"] = "Hi {0},",
                ["email.notify.cta"] = "Open Home OS",

                ["email.reset.subject"] = "Reset your Home OS password",
                ["email.reset.greeting"] = "Hi {0},",
                ["email.reset.line"] = "You asked to reset your password. Click below to set a new one (if this wasn't you, just ignore this email).",
                ["email.reset.cta"] = "Set a new password",

                ["email.invite.subject"] = "You're invited to join {0} on Home OS",
                ["email.invite.greeting"] = "Hi {0},",
                ["email.invite.line"] = "You've been invited to join the {0} household on Home OS.",
                ["email.invite.cta"] = "Accept the invitation",

                ["email.task.subject"] = "New task: {0}",
                ["email.task.greeting"] = "Hi {0},",
                ["email.task.line"] = "A new task has been assigned to you: {0}.",
                ["email.task.due"] = "It's due {0}.",
                ["email.task.cta"] = "Open Tasks",
            },
        };
}
