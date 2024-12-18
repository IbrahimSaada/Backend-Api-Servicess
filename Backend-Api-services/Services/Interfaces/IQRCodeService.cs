// QRCodeService.cs
using QRCoder;

namespace Backend_Api_services.Services.Interfaces
{
    public interface IQRCodeService
    {
        string GenerateQRCodeBase64(string text);
    }

    public class QRCodeService : IQRCodeService
    {
        public string GenerateQRCodeBase64(string text)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = pngQrCode.GetGraphic(20);
            return Convert.ToBase64String(qrCodeBytes);
        }
    }
}
