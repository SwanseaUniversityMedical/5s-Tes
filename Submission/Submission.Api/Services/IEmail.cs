using FiveSafesTes.Core.Models;
using NETCore.MailKit.Core;

namespace Submission.Api.Services
{
    public interface IDareEmailService
    {
        Task EmailTo(string emailTo, string Subject, string body, bool IsHtml);
    }

    public class DareEmailService : IDareEmailService
    {
        private IEmailService _IEmailService;

        private EmailSettings _EmailSettings;

        public DareEmailService(IEmailService IEmailService,
           EmailSettings EmailSettings) { 
        
            _IEmailService = IEmailService;
            _EmailSettings = EmailSettings;

        }

        public async Task EmailTo(string emailTo, string Subject, string body, bool IsHtml)
        {
            if (_EmailSettings.Enabled)
            {
                await _IEmailService.SendAsync(emailTo, Subject, body, IsHtml);
            }
        }
    }
}
