using System.IO;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    public class MockEmailService : IEmailService
    {
        private readonly string _emailDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Emails");

        public MockEmailService()
        {
            if (!Directory.Exists(_emailDirectory))
            {
                Directory.CreateDirectory(_emailDirectory);
            }
        }

        public async Task SendEmailAsync(string toString, string subject, string body)
        {
            var fileName = $"Email_{System.DateTime.Now:yyyyMMdd_HHmmss}_{subject.Replace(" ", "_")}.html";
            var filePath = Path.Combine(_emailDirectory, fileName);

            var htmlContent = $@"
            <html>
                <head>
                    <title>{subject}</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }}
                        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                        h1 {{ color: #333; }}
                        .button {{ display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
                        .footer {{ margin-top: 20px; font-size: 12px; color: #888; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>{subject}</h1>
                        <p>To: {toString}</p>
                        <hr/>
                        {body}
                        <div class='footer'>
                            <p>This is a simulated email saved to your local disk.</p>
                        </div>
                    </div>
                </body>
            </html>";

            await File.WriteAllTextAsync(filePath, htmlContent);
        }
    }
}
