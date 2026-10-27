using System.Net;
using System.Net.Mail;
using ZU.Training.Application.Communication;

namespace ZU.Training.Infrastructure.Communication;

public sealed record SmtpOptions(string Host, int Port, bool EnableSsl, string? UserName, string? Password,
    string FromAddress, string FromName)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>
/// إرسال البريد عبر SMTP. عند غياب الإعدادات تُعاد حالة فشل واضحة دون إسقاط الطلب،
/// ويُسجَّل آخر خطأ على المستخدم بحيث لا يُفترض النجاح بلا دليل.
/// </summary>
public sealed class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task<EmailOutcome> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (!options.IsConfigured) return EmailOutcome.Failed;
        if (string.IsNullOrWhiteSpace(message.To) || !message.To.Contains('@')) return EmailOutcome.Failed;

        try
        {
            using var client = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
            };
            if (!string.IsNullOrWhiteSpace(options.UserName))
                client.Credentials = new NetworkCredential(options.UserName, options.Password);

            using var mail = new MailMessage
            {
                From = new MailAddress(options.FromAddress, options.FromName, System.Text.Encoding.UTF8),
                Subject = message.Subject,
                SubjectEncoding = System.Text.Encoding.UTF8,
                Body = message.HtmlBody,
                BodyEncoding = System.Text.Encoding.UTF8,
                IsBodyHtml = true
            };
            mail.To.Add(message.To);
            if (!string.IsNullOrWhiteSpace(message.PlainText))
                mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.PlainText,
                    System.Text.Encoding.UTF8, "text/plain"));

            await client.SendMailAsync(mail, ct);
            return EmailOutcome.Sent;
        }
        catch (OperationCanceledException)
        {
            return EmailOutcome.Uncertain;
        }
        catch (SmtpFailedRecipientException)
        {
            return EmailOutcome.Failed;
        }
        catch (SmtpException exception) when (exception.StatusCode == SmtpStatusCode.GeneralFailure)
        {
            return EmailOutcome.Uncertain;
        }
        catch (Exception)
        {
            return EmailOutcome.Failed;
        }
    }
}
