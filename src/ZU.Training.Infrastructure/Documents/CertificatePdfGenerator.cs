using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZU.Training.Application.Training;

namespace ZU.Training.Infrastructure.Documents;

public sealed record CertificateLayout(string FontFamily = "Tahoma", string? InstitutionArabic = null,
    string? InstitutionEnglish = null, string? LogoPath = null);

/// <summary>
/// مولد ملف الشهادة: يرسم التصميم المعتمد ويطبع البيانات المتغيرة ورمز QR في مواضعها.
/// يحاول المحرك الأساسي (QuestPDF مع دعم العربية RTL)، وإن تعذر يستخدم كاتب PDF المبسط
/// حتى لا تتعطل عملية الإصدار.
/// </summary>
public sealed class CertificatePdfGenerator(CertificateLayout layout) : ICertificatePdfGenerator
{
    private const string Navy = "#022C5A";
    private const string Muted = "#64748B";

    static CertificatePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderAsync(CertificateDocumentData data, CancellationToken ct)
    {
        try
        {
            return Task.FromResult(RenderWithQuestPdf(data));
        }
        catch (Exception)
        {
            var lines = new List<(string, string)>
            {
                ("Recipient", Latin(data.RecipientNameEn ?? data.RecipientNameAr)),
                ("Course", Latin(data.CourseTitleEn ?? data.CourseTitleAr)),
                ("Instructor", Latin(data.InstructorNameEn ?? data.InstructorNameAr)),
                ("Faculty", Latin(data.FacultyNameEn ?? data.FacultyNameAr ?? "-")),
                ("Hours", data.PlannedHours.ToString("0.##", CultureInfo.InvariantCulture)),
                ("Period", data.CourseStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " - " +
                    data.CourseEndUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            };
            return Task.FromResult(SimplePdfWriter.BuildCertificate(
                Latin(layout.InstitutionEnglish ?? "Zarqa University"), lines, data.ReferenceNumber,
                data.VerificationCode, data.VerificationUrl, data.IssuedAtUtc));
        }
    }

    private byte[] RenderWithQuestPdf(CertificateDocumentData data)
    {
        var qr = QrImage.Png(data.VerificationUrl);
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0);
                page.DefaultTextStyle(style => style.FontFamily(layout.FontFamily).FontSize(12).FontColor(Navy));
                page.Content().Padding(18).Border(2).BorderColor(Navy).Padding(10).Element(frame =>
                {
                    frame.Column(column =>
                    {
                        column.Item().Background(Navy).Padding(16).Row(row =>
                        {
                            row.RelativeItem().Column(info =>
                            {
                                info.Item().Text(layout.InstitutionArabic ?? "جامعة الزرقاء")
                                    .FontSize(17).Bold().FontColor("#FFFFFF");
                                info.Item().Text(layout.InstitutionEnglish ?? "Zarqa University")
                                    .FontSize(11).FontColor("#FFFFFF");
                            });
                            row.ConstantItem(230).AlignLeft().AlignMiddle()
                                .Text("منصة التدريب والتطوير المهني").FontSize(11).FontColor("#FFFFFF");
                        });

                        column.Item().PaddingTop(18).AlignCenter()
                            .Text("شهادة إتمام دورة تدريبية").FontSize(46).Bold();
                        column.Item().AlignCenter()
                            .Text("Certificate of Course Completion").FontSize(15).FontColor(Muted);

                        column.Item().PaddingTop(22).AlignCenter().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(13).FontColor(Muted));
                            text.Span("تشهد جامعة الزرقاء بأن ");
                            text.Span(Name(data.RecipientNameAr, data.RecipientNameEn)).FontSize(30).Bold()
                                .FontColor(Navy);
                            text.Span(" قد أتم بنجاح الدورة التدريبية");
                        });

                        column.Item().PaddingTop(10).AlignCenter()
                            .Text(Name(data.CourseTitleAr, data.CourseTitleEn)).FontSize(26).Bold()
                            .FontColor("#06467E");

                        column.Item().PaddingTop(10).AlignCenter()
                            .Text($"{data.PlannedHours:0.##} ساعة تدريبية — {data.CourseStartUtc:yyyy/MM/dd} إلى {data.CourseEndUtc:yyyy/MM/dd}")
                            .FontSize(13).FontColor(Muted);

                        column.Item().PaddingTop(6).AlignCenter()
                            .Text($"المحاضر: {Name(data.InstructorNameAr, data.InstructorNameEn)}")
                            .FontSize(14).Bold();

                        column.Item().PaddingTop(6).AlignCenter()
                            .Text($"الكلية: {Name(data.FacultyNameAr ?? "-", data.FacultyNameEn)}")
                            .FontSize(11).FontColor(Muted);

                        column.Item().PaddingTop(20).Row(row =>
                        {
                            row.ConstantItem(150).Column(left =>
                            {
                                left.Item().Text("الرقم المرجعي").FontSize(9).FontColor(Muted);
                                left.Item().Text(data.ReferenceNumber).Bold().FontSize(12);
                                left.Item().Text("رمز التحقق").FontSize(9).FontColor(Muted);
                                left.Item().Text(data.VerificationCode).Bold().FontSize(12);
                            });
                            row.RelativeItem().AlignCenter().Column(middle =>
                            {
                                middle.Item().Text("تاريخ الإصدار").FontSize(9).FontColor(Muted).AlignCenter();
                                middle.Item().Text(data.IssuedAtUtc.ToString("yyyy/MM/dd"))
                                    .FontSize(12).AlignCenter();
                            });
                            row.ConstantItem(96).Column(right =>
                            {
                                right.Item().Width(92).Height(92).Image(qr);
                            });
                        });

                        column.Item().PaddingTop(6).AlignCenter()
                            .Text(data.VerificationUrl).FontSize(8).FontColor(Muted);
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string Name(string arabic, string? english)
        => string.IsNullOrWhiteSpace(arabic) ? english ?? string.Empty : arabic;

    private static string Latin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var builder = new System.Text.StringBuilder();
        foreach (var character in value)
            builder.Append(character <= 127 ? character : '?');
        return builder.ToString();
    }
}
