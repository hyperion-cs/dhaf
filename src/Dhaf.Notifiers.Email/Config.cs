using Dhaf.Core;

namespace Dhaf.Notifiers.Email
{
    public class Config : INotifierConfig
    {
        public string ExtensionName => "email";
        public string Name { get; set; }
        public SmtpConfig Smtp { get; set; }

        public string From { get; set; }
        public string To { get; set; }
    }

    public class SmtpConfig
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string Security { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
