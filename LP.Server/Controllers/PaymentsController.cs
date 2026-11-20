using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PaymentsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("tinkoff-qr")]
        public IActionResult GetTinkoffQr([FromQuery] int size = 10)
        {
            var payload = _configuration["Payments:TinkoffQrPayload"];
            if (string.IsNullOrWhiteSpace(payload))
            {
                payload = "https://www.tinkoff.ru/";
            }

            var pixelsPerModule = Math.Clamp(size, 4, 40);

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(data);
            var png = qrCode.GetGraphic(pixelsPerModule);

            Response.Headers.CacheControl = "public, max-age=3600";
            return File(png, "image/png");
        }
    }
}
