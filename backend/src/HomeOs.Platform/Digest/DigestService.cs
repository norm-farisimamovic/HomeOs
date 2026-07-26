using System.Net;
using System.Text;
using HomeOs.Platform.Assistant;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeOs.Platform.Digest;

/// <summary>Builds and sends the "what's coming up" digest email for a member.</summary>
public interface IDigestService
{
    /// <summary>
    /// Sends the digest covering the next <paramref name="days"/> days to the member, in their own language.
    /// Returns <c>true</c> if an email went out (false when there's nothing upcoming or the address is a
    /// demo/dev sink). Stamps <c>DigestLastSentUtc</c> when it sends.
    /// </summary>
    Task<bool> SendToMemberAsync(Guid householdId, Guid memberId, int days, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class DigestService(
    PlatformDbContext db, IEnumerable<IUpcomingProvider> providers, IEmailSender email, IAppText text,
    IAssistant assistant, IConfiguration config)
    : IDigestService
{
    /// <inheritdoc />
    public async Task<bool> SendToMemberAsync(Guid householdId, Guid memberId, int days, CancellationToken ct = default)
    {
        var member = await db.Users.FirstOrDefaultAsync(u => u.Id == memberId, ct);
        if (member?.Email is not { Length: > 0 } address) return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = today.AddDays(Math.Max(1, days));

        var items = new List<UpcomingItem>();
        foreach (var provider in providers)
            items.AddRange(await provider.GetUpcomingAsync(householdId, memberId, today, to, ct));

        member.DigestLastSentUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (items.Count == 0) return false;
        // Don't send real mail to the demo/dev sink addresses (they'd just bounce).
        if (address.EndsWith("@homeos.local", StringComparison.OrdinalIgnoreCase)) return false;

        var lang = member.PreferredCulture;
        var baseUrl = config["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

        var ordered = items.OrderBy(i => i.Date).ThenBy(i => i.Title, StringComparer.CurrentCulture).ToList();
        var rows = new StringBuilder();
        foreach (var it in ordered)
        {
            var when = it.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd, d MMM", Culture(lang));
            var kind = text.T(lang, $"digest.kind.{it.Kind}");
            rows.Append(
                $"<tr><td style=\"padding:6px 0;color:#8a938e;font-size:12.5px;white-space:nowrap;\">{WebUtility.HtmlEncode(when)}</td>" +
                $"<td style=\"padding:6px 0 6px 14px;font-size:13.5px;\"><b>{WebUtility.HtmlEncode(it.Title)}</b> " +
                $"<span style=\"color:#8a938e;\">· {WebUtility.HtmlEncode(kind)}</span></td></tr>");
        }

        var intro = text.T(lang, "digest.intro", items.Count);

        // AI intro (best-effort): a short, warm summary of what's ahead. Skipped silently if no key / on error.
        var aiParagraph = string.Empty;
        var aiSummary = await assistant.SummarizeAsync(lang, string.Join('\n', ordered.Select(i =>
            $"- {i.Date:yyyy-MM-dd} [{text.T(lang, $"digest.kind.{i.Kind}")}] {i.Title}")), ct);
        if (!string.IsNullOrWhiteSpace(aiSummary))
            aiParagraph = $"<p style=\"margin:0 0 14px;color:#42504a;font-size:14px;line-height:1.6;\">{WebUtility.HtmlEncode(aiSummary)}</p>";

        var body = $"{aiParagraph}{WebUtility.HtmlEncode(intro)}<table style=\"width:100%;border-collapse:collapse;margin-top:12px;\">{rows}</table>";
        var html = text.EmailHtml(lang,
            text.T(lang, "digest.greeting", WebUtility.HtmlEncode(member.DisplayName)),
            body, text.T(lang, "digest.cta"), baseUrl + "/", showRawLink: false);

        await email.SendAsync(new EmailMessage(address, text.T(lang, "digest.subject"), html), ct);
        return true;
    }

    private static System.Globalization.CultureInfo Culture(string lang) =>
        System.Globalization.CultureInfo.GetCultureInfo(lang == "en" ? "en-GB" : "bs-Latn-BA");
}
