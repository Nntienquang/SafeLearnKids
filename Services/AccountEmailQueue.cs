using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace SafeLearnKids.Web.Services;

// SMTP latency must not reveal whether an email address has an account.
public sealed class AccountEmailQueue(
    IOptions<AccountEmailOptions> options,
    IWebHostEnvironment environment,
    ILogger<AccountEmailQueue> logger) : BackgroundService
{
    private readonly Channel<(string Recipient, string Subject, string Body)> requests =
        Channel.CreateBounded<(string, string, string)>(new BoundedChannelOptions(100)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });

    public void Enqueue(string recipient, string subject, string body)
    {
        if (!requests.Writer.TryWrite((recipient, subject, body)))
            logger.LogError("Account email queue is full. Retry delivery through the account recovery forms.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in requests.Reader.ReadAllAsync(stoppingToken))
                await SendAsync(message.Recipient, message.Subject, message.Body, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            using var client = new SmtpClient { UseDefaultCredentials = false };
            if (settings.DeliveryMethod == "Pickup" && environment.IsDevelopment())
            {
                var directory = Path.GetFullPath(settings.PickupDirectory ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SafeLearnKids", "Mail"));
                // Tokens must never be saved in the repository or a web-served directory.
                var relative = Path.GetRelativePath(environment.ContentRootPath, directory);
                if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar))
                    throw new InvalidOperationException("Email pickup directory must be outside the application repository.");
                if (environment.WebRootPath is { } webRoot)
                {
                    var webRelative = Path.GetRelativePath(webRoot, directory);
                    if (!Path.IsPathRooted(webRelative) && webRelative != ".." && !webRelative.StartsWith(".." + Path.DirectorySeparatorChar))
                        throw new InvalidOperationException("Email pickup directory must be outside the web root.");
                }
                Directory.CreateDirectory(directory);
                client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                client.PickupDirectoryLocation = directory;
            }
            else
            {
                client.Host = settings.Host;
                client.Port = settings.Port;
                client.EnableSsl = true; // Require STARTTLS for network SMTP, including development.
                if (!string.IsNullOrWhiteSpace(settings.UserName))
                    client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
            }

            using var message = new MailMessage(settings.FromAddress, recipient, subject, body)
            {
                BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8, IsBodyHtml = false
            };
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            // SMTP exceptions may contain recipient addresses or URLs. Do not log their details.
            logger.LogError("Account email delivery failed. Check the private email provider configuration.");
        }
    }
}
