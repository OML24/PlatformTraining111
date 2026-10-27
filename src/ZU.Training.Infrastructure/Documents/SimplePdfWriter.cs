using System.Globalization;
using System.Text;
using QRCoder;

namespace ZU.Training.Infrastructure.Documents;

/// <summary>توليد رمز QR للتحقق الرقمي.</summary>
public static class QrImage
{
    public static byte[] Png(string content, int pixelsPerModule = 12)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public static bool[,] Matrix(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var size = data.ModuleMatrix.Count;
        var matrix = new bool[size, size];
        for (var y = 0; y < size; y++)
        {
            var row = data.ModuleMatrix[y];
            for (var x = 0; x < size; x++)
                matrix[y, x] = x < row.Count && row[x];
        }
        return matrix;
    }
}

/// <summary>
/// كاتب PDF مستقل بلا مكتبات خارجية، يُستخدم كخطة بديلة إذا تعذر محرك QuestPDF.
/// يرسم الإطار والبيانات اللاتينية ورمز QR (كمربعات) دون الحاجة لمحرف عربي مضمّن.
/// </summary>
public static class SimplePdfWriter
{
    public static byte[] BuildCertificate(string titleLine, IReadOnlyList<(string Label, string Value)> lines,
        string referenceNumber, string verificationCode, string verificationUrl, DateTimeOffset issuedAt)
    {
        var width = 842;
        var height = 595;
        var content = new StringBuilder();

        void Rect(double x, double y, double w, double h, double r, double g, double b)
        {
            content.Append(CultureInfo.InvariantCulture,
                $"{r:0.###} {g:0.###} {b:0.###} rg {x:0.##} {y:0.##} {w:0.##} {h:0.##} re f\n");
        }
        void Text(string text, double x, double y, double size, bool bold, double r, double g, double b)
        {
            content.Append(CultureInfo.InvariantCulture,
                $"BT {r:0.###} {g:0.###} {b:0.###} rg /{(bold ? "F2" : "F1")} {size:0.##} Tf 1 0 0 1 {x:0.##} {y:0.##} Tm ({Escape(text)}) Tj ET\n");
        }

        Rect(0, 0, width, height, 1, 1, 1);
        Rect(28, 28, width - 56, height - 56, 0.008, 0.173, 0.353);
        Rect(34, 34, width - 68, height - 68, 1, 1, 1);
        Rect(34, height - 96, width - 68, 62, 0.008, 0.173, 0.353);

        Text(titleLine, 60, height - 78, 16, true, 1, 1, 1);
        Text("CERTIFICATE OF COMPLETION", width / 2 - 150, height - 140, 15, true, 0.008, 0.173, 0.353);

        var y = height - 190;
        foreach (var (label, value) in lines)
        {
            Text(label + ": " + value, 70, y, 13, false, 0.1, 0.16, 0.24);
            y -= 26;
        }

        Text("Reference: " + referenceNumber, 70, 96, 12, true, 0.008, 0.173, 0.353);
        Text("Verification code: " + verificationCode, 70, 78, 11, false, 0.35, 0.42, 0.5);
        Text("Issued: " + issuedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 70, 60, 11, false, 0.35, 0.42, 0.5);
        Text(verificationUrl, 70, 42, 9, false, 0.35, 0.42, 0.5);

        // QR as filled rectangles (no image XObject required).
        var matrix = QrImage.Matrix(verificationUrl);
        var modules = matrix.GetLength(0);
        var box = 132.0;
        var module = box / modules;
        var originX = width - 70 - box;
        var originY = 60;
        Rect(originX - 4, originY - 4, box + 8, box + 8, 1, 1, 1);
        for (var row = 0; row < modules; row++)
        for (var column = 0; column < modules; column++)
            if (matrix[row, column])
                Rect(originX + column * module, originY + (modules - 1 - row) * module, module, module,
                    0, 0, 0);

        var stream = content.ToString();
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            builder.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        builder.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            switch (character)
            {
                case '(' or ')' or '\\': builder.Append('\\').Append(character); break;
                case '\n' or '\r': builder.Append(' '); break;
                default:
                    if (character is > (char)126 or < (char)32) builder.Append('?');
                    else builder.Append(character);
                    break;
            }
        }
        return builder.ToString();
    }
}
