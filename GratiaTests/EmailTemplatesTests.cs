using Gratia.Application.Interfaces;
using Gratia.Infrastructure.Email;

namespace GratiaTests
{
    public class EmailTemplatesTests
    {
        [Fact]
        public void Names_from_users_are_html_encoded()
        {
            var email = EmailTemplates.Invitation(
                companyName: "<script>alert(1)</script>",
                inviterName: "Eve <img src=x onerror=alert(1)>",
                inviterEmail: "eve@acme.test",
                acceptUrl: "http://localhost:5173/accept-invitation/abc");

            Assert.DoesNotContain("<script>", email.HtmlBody);
            Assert.DoesNotContain("<img", email.HtmlBody);
            Assert.Contains("&lt;script&gt;", email.HtmlBody);
            Assert.Contains("href=\"http://localhost:5173/accept-invitation/abc\"", email.HtmlBody);
        }

        [Fact]
        public void The_account_emails_encode_what_users_typed()
        {
            const string evil = "<img src=x onerror=alert(1)>";

            foreach (var email in new[]
            {
                EmailTemplates.PasswordReset(evil, "http://localhost:5173/reset-password/abc"),
                EmailTemplates.PasswordChanged(evil, "http://localhost:5173/forgot-password"),
                EmailTemplates.EmailChanged(evil, evil),
                EmailTemplates.InvitationAccountElsewhere(evil, evil, evil, "http://localhost:5173/login"),
                EmailTemplates.WorkspaceExists(evil, evil, "http://localhost:5173/claim-workspace/abc"),
                EmailTemplates.DomainVerified(evil, evil, evil, DomainVerifiedBy.MemberMadeAdmin),
            })
            {
                Assert.DoesNotContain("<img", email.HtmlBody);
                Assert.Contains("&lt;img", email.HtmlBody);
            }
        }

        [Fact]
        public void Subjects_stay_on_one_line()
        {
            var email = EmailTemplates.CompanySetup("Peter", "Initech\r\nBcc: someone@else.test", "http://localhost:5173/accept-invitation/abc");

            Assert.DoesNotContain("\r", email.Subject);
            Assert.DoesNotContain("\n", email.Subject);
        }

        [Fact]
        public void The_domain_verified_notice_says_what_it_did()
        {
            Assert.DoesNotContain("admin of", EmailTemplates.DomainVerified("Acme", "acme.test", "ivy@acme.test", DomainVerifiedBy.Admin).HtmlBody);
            Assert.Contains("is now an admin of Acme",
                EmailTemplates.DomainVerified("Acme", "acme.test", "ivy@acme.test", DomainVerifiedBy.MemberMadeAdmin).HtmlBody);
            var invited = EmailTemplates.DomainVerified("Acme", "acme.test", "ivy@acme.test", DomainVerifiedBy.InvitedAsAdmin);
            Assert.Contains("invited to join Acme on Gratia as an admin", invited.HtmlBody);
            Assert.Equal("ivy@acme.test was invited to Acme as an admin", invited.Subject);
        }

        [Fact]
        public void The_welcome_email_states_the_monthly_allowance()
        {
            var email = EmailTemplates.Welcome("Peter", "Initech", 25, "http://localhost:5173/login");

            Assert.Contains("25 points to give", email.HtmlBody);
        }
    }
}
