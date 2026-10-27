using ZU.Training.Application.Training;

namespace ZU.Training.Infrastructure.Documents;

public sealed class QrCodeRenderer : IQrCodeRenderer
{
    public byte[] RenderPng(string content) => QrImage.Png(content, 10);
}
