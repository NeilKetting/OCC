using System.Threading.Tasks;

namespace OCC.API.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toString, string subject, string body);
    }
}
