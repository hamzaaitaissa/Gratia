using DnsClient;
using DnsClient.Protocol;
using Gratia.Application.Interfaces;
using Gratia.Infrastructure.Dns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GratiaTests
{
    // How DnsDomainVerifier reads DNS answers (#61), with DnsClient's query mocked: the tests never ask a real DNS server
    public class DnsDomainVerifierTests
    {
        private const string Domain = "acme.test";
        private const string Value = "gratia-verify=0123456789abcdef0123456789abcdef";

        private readonly Mock<IDnsQuery> _dns = new();

        private DnsDomainVerifier Verifier => new(_dns.Object, NullLogger<DnsDomainVerifier>.Instance);

        [Fact]
        public async Task A_record_among_others_is_found_ignoring_case_and_spaces()
        {
            Answer(Txt("v=spf1 -all"), Txt(" " + Value.ToUpperInvariant() + " "));

            Assert.Equal(DnsLookupResult.Found, await Verifier.HasTxtRecordAsync(Domain, Value));
        }

        [Fact]
        public async Task A_value_split_into_several_strings_counts_as_one()
        {
            Answer(Txt("gratia-verify=0123456789abcdef", "0123456789abcdef"));

            Assert.Equal(DnsLookupResult.Found, await Verifier.HasTxtRecordAsync(Domain, Value));
        }

        [Fact]
        public async Task Another_value_or_none_is_not_found()
        {
            Answer(Txt("gratia-verify=ffffffffffffffffffffffffffffffff"), Txt(Value + "0"));
            Assert.Equal(DnsLookupResult.NotFound, await Verifier.HasTxtRecordAsync(Domain, Value));

            Answer();
            Assert.Equal(DnsLookupResult.NotFound, await Verifier.HasTxtRecordAsync(Domain, Value));
        }

        [Fact]
        public async Task A_domain_that_does_not_exist_is_not_found_but_a_server_failure_is_a_failure()
        {
            Error(DnsHeaderResponseCode.NotExistentDomain);
            Assert.Equal(DnsLookupResult.NotFound, await Verifier.HasTxtRecordAsync(Domain, Value));

            Error(DnsHeaderResponseCode.ServerFailure);
            Assert.Equal(DnsLookupResult.Failed, await Verifier.HasTxtRecordAsync(Domain, Value));
        }

        [Fact]
        public async Task A_timeout_is_a_failure()
        {
            _dns.Setup(d => d.QueryAsync(Domain, QueryType.TXT, QueryClass.IN, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DnsResponseException(DnsResponseCode.ConnectionTimeout, "No connection could be established"));

            Assert.Equal(DnsLookupResult.Failed, await Verifier.HasTxtRecordAsync(Domain, Value));
        }

        [Fact]
        public async Task Cancelling_is_not_a_failure()
        {
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            _dns.Setup(d => d.QueryAsync(Domain, QueryType.TXT, QueryClass.IN, cancelled.Token))
                .ThrowsAsync(new OperationCanceledException(cancelled.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Verifier.HasTxtRecordAsync(Domain, Value, cancelled.Token));
        }

        [Theory]
        [InlineData("localhost")]
        [InlineData("127.0.0.1")]
        [InlineData("[::1]")]
        [InlineData("not a domain")]
        public async Task Only_a_real_host_name_is_looked_up(string domain)
        {
            Assert.Equal(DnsLookupResult.NotFound, await Verifier.HasTxtRecordAsync(domain, Value));
            _dns.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("1.1.1.1", "1.1.1.1:53")]
        [InlineData(" 127.0.0.1:5353 ", "127.0.0.1:5353")]
        [InlineData("2606:4700:4700::1111", "[2606:4700:4700::1111]:53")]
        public void A_name_server_is_an_address_with_port_53_unless_given(string setting, string expected)
        {
            Assert.True(DomainVerificationOptions.TryParse(setting, out var endPoint));
            Assert.Equal(expected, endPoint.ToString());
        }

        private static TxtRecord Txt(params string[] strings) =>
            new(new ResourceRecordInfo(Domain + ".", ResourceRecordType.TXT, QueryClass.IN, 300, 0), strings, strings);

        private void Answer(params DnsResourceRecord[] records)
        {
            var response = new Mock<IDnsQueryResponse>();
            response.Setup(r => r.HasError).Returns(false);
            response.Setup(r => r.Answers).Returns(records);
            _dns.Setup(d => d.QueryAsync(Domain, QueryType.TXT, QueryClass.IN, It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        }

        private void Error(DnsHeaderResponseCode code)
        {
            var response = new Mock<IDnsQueryResponse>();
            response.Setup(r => r.HasError).Returns(true);
            response.Setup(r => r.ErrorMessage).Returns(code.ToString());
            // A response's flags: QR, RD and RA set, and the response code in the last four bits
            response.Setup(r => r.Header).Returns(new DnsResponseHeader(1, (ushort)(0x8180 | (int)code), 1, 0, 0, 0));
            response.Setup(r => r.Answers).Returns([]);
            _dns.Setup(d => d.QueryAsync(Domain, QueryType.TXT, QueryClass.IN, It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        }
    }
}
