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
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var bitmap = qrCode.GetGraphic(20))
                    {
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            return Convert.ToBase64String(stream.ToArray());
                        }
                    }
                }
            }
        }
    }
}
