using Microsoft.Extensions.Configuration;
using Taim.Connectors.Sdk;

namespace Taim.Connectors.Email;

public sealed class EmailConnector(IConfiguration config) : McpStdioConnector
{
    public override string ConnectorId => "email";
    public override string DisplayName => "Email";
    public override string Description => "Sends and reads emails via SMTP/IMAP.";

    protected override string Command => "node";
    protected override IEnumerable<string> Arguments => ["mcp-servers/email/index.js"];

    protected override IDictionary<string, string?>? Environment => new Dictionary<string, string?>
    {
        ["SMTP_HOST"] = config["Email:SmtpHost"],
        ["SMTP_PORT"] = config["Email:SmtpPort"],
        ["SMTP_USER"] = config["Email:SmtpUser"],
        ["SMTP_PASS"] = config["Email:SmtpPass"],
        ["FROM_ADDRESS"] = config["Email:FromAddress"],
    };
}
