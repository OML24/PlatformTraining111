using System.Net;

namespace ZU.Training.Application.Communication;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? PlainText = null);

public enum EmailOutcome
{
    Sent = 0,
    Failed = 1,
    Uncertain = 2
}

public interface IEmailSender
{
    Task<EmailOutcome> SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>قوالب البريد العربية/الإنجليزية (بيانات دخول، استعادة كلمة المرور، إشعارات).</summary>
public static class EmailTemplates
{
    private const string Header = """
        <div style="background:#022C5A;color:#fff;padding:22px 26px;border-radius:14px 14px 0 0">
          <div style="font-size:19px;font-weight:700">جامعة الزرقاء</div>
          <div style="font-size:13px;opacity:.85">منصة التدريب والتطوير المهني</div>
        </div>
        """;

    public static EmailMessage InitialCredentials(string to, string nameAr, string loginName, string password,
        string portalUrl)
        => new(to, "بيانات الدخول إلى منصة التدريب والتطوير المهني — جامعة الزرقاء",
            Wrap($"""
                <p style="font-size:16px">مرحباً {Escape(nameAr)}،</p>
                <p>تم إنشاء حسابك على منصة التدريب والتطوير المهني في جامعة الزرقاء. بيانات الدخول:</p>
                <table style="width:100%;border-collapse:collapse;margin:18px 0;font-size:16px">
                  <tr><td style="padding:10px;border:1px solid #DAE4EF;background:#F8FAFC">الرقم الوظيفي</td>
                      <td style="padding:10px;border:1px solid #DAE4EF;font-weight:700">{Escape(loginName)}</td></tr>
                  <tr><td style="padding:10px;border:1px solid #DAE4EF;background:#F8FAFC">كلمة المرور الأولية</td>
                      <td style="padding:10px;border:1px solid #DAE4EF;font-weight:700;font-family:monospace">{Escape(password)}</td></tr>
                </table>
                <p>يُطلب منك تعيين كلمة مرور جديدة عند أول دخول. لا تشارك هذه البيانات مع أي شخص.</p>
                <p><a href="{Escape(portalUrl)}" style="background:#022C5A;color:#fff;padding:12px 22px;border-radius:10px;text-decoration:none;display:inline-block">الدخول إلى المنصة</a></p>
                """));

    public static EmailMessage PasswordReminder(string to, string nameAr, string loginName, string password)
        => new(to, "كلمة المرور الحالية — منصة التدريب جامعة الزرقاء",
            Wrap($"""
                <p style="font-size:16px">مرحباً {Escape(nameAr)}،</p>
                <p>وردنا طلب استعادة كلمة المرور لحسابك. كلمة المرور الحالية:</p>
                <p style="font-family:monospace;font-size:16px;padding:12px;border:1px solid #DAE4EF;background:#F8FAFC">{Escape(password)}</p>
                <p style="color:#64748b;font-size:13px">اسم الدخول: {Escape(loginName)}. إن لم تطلب هذا، أبلغ مسؤول النظام فوراً.</p>
                """));

    public static EmailMessage ExternalTrainerAccount(string to, string nameAr, string loginName,
        string password, string portalUrl)
        => new(to, "بيانات الدخول — حساب محاضر خارجي",
            Wrap($"""
                <p style="font-size:16px">مرحباً {Escape(nameAr)}،</p>
                <p>تم إنشاء حساب محاضر خارجي لك على منصة التدريب والتطوير المهني في جامعة الزرقاء.</p>
                <table style="width:100%;border-collapse:collapse;margin:18px 0;font-size:16px">
                  <tr><td style="padding:10px;border:1px solid #DAE4EF;background:#F8FAFC">اسم الدخول</td>
                      <td style="padding:10px;border:1px solid #DAE4EF;font-weight:700">{Escape(loginName)}</td></tr>
                  <tr><td style="padding:10px;border:1px solid #DAE4EF;background:#F8FAFC">كلمة المرور</td>
                      <td style="padding:10px;border:1px solid #DAE4EF;font-weight:700;font-family:monospace">{Escape(password)}</td></tr>
                </table>
                <p>لتغيير كلمة المرور تواصل مع مسؤول النظام.</p>
                <p><a href="{Escape(portalUrl)}" style="background:#022C5A;color:#fff;padding:12px 22px;border-radius:10px;text-decoration:none;display:inline-block">الدخول إلى المنصة</a></p>
                """));

    private static string Wrap(string body) => $"""
        <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif;background:#F8FAFC;padding:24px;color:#022C5A">
          <div style="max-width:600px;margin:0 auto;background:#fff;border:1px solid #DAE4EF;border-radius:14px">
            {Header}
            <div style="padding:26px;line-height:1.9">{body}</div>
            <div style="padding:16px 26px;border-top:1px solid #DAE4EF;color:#64748b;font-size:12px">
              هذه رسالة آلية من منصة التدريب والتطوير المهني — جامعة الزرقاء.
            </div>
          </div>
        </div>
        """;

    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
