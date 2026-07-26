using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HomeOs.Platform.Notifications;

/// <summary>An email to send.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>Platform email capability. Apps never build their own — they send through this.</summary>
public interface IEmailSender
{
    /// <summary>Sends (or, in dev without SMTP configured, logs) the message.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Dev fallback: logs the email (incl. any links) instead of sending. Used when SMTP isn't configured.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("📧 [dev email] To: {To} · Subject: {Subject}\n{Body}",
            message.To, message.Subject, message.HtmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>Sends via SMTP using the member's own mailbox (config <c>Email:Smtp:*</c>).</summary>
public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = config.GetSection("Email:Smtp");
        var from = config["Email:From"] ?? smtp["User"] ?? "no-reply@homeos.local";

        using var client = new SmtpClient(smtp["Host"], int.TryParse(smtp["Port"], out var p) ? p : 587)
        {
            EnableSsl = !bool.TryParse(smtp["UseSsl"], out var ssl) || ssl,
            Credentials = string.IsNullOrEmpty(smtp["User"]) ? null : new NetworkCredential(smtp["User"], smtp["Password"]),
        };

        using var mail = new MailMessage(from, message.To, message.Subject, message.HtmlBody) { IsBodyHtml = true };
        try
        {
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}.", message.To);
        }
    }
}

/// <summary>DI for email.</summary>
public static class EmailRegistration
{
    /// <summary>Registers SMTP when <c>Email:Smtp:Host</c> is set, otherwise the dev logging sender.</summary>
    public static IServiceCollection AddHomeOsEmail(this IServiceCollection services, IConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config["Email:Smtp:Host"]))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        return services;
    }
}
