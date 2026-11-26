using System.Net;

namespace Gratia.Infrastructure.Dns
{
    // How domain verification (#61) asks DNS, from the "DomainVerification" section
    public sealed class DomainVerificationOptions
    {
        public const string SectionName = "DomainVerification";

        // The DNS servers to ask, as "1.1.1.1" or "1.1.1.1:53". Empty: the machine's own. Public resolvers answer with what
        // the domain's servers say now, where a local resolver may still remember that the record wasn't there.
        public string[] NameServers { get; set; } = [];

        // How long to wait for one answer. Each lookup tries once more after a timeout.
        public int TimeoutSeconds { get; set; } = 5;

        public bool IsValid() => TimeoutSeconds is > 0 and <= 30 && NameServers.All(s => TryParse(s, out _));

        public static bool TryParse(string nameServer, out IPEndPoint endPoint)
        {
            if (IPEndPoint.TryParse(nameServer.Trim(), out var parsed))
            {
                endPoint = parsed.Port == 0 ? new IPEndPoint(parsed.Address, 53) : parsed;
                return true;
            }

            endPoint = null!;
            return false;
        }
    }
}
