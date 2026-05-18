using Microsoft.Extensions.AI;

namespace Taim.Agents.Domain;

public sealed class MarketingAgent(IChatClient chatClient) : DomainAgentBase(chatClient)
{
    protected override string RoleTitle => "Marketing Specialist";

    protected override string SpecialtyDescription => """
        You are a marketing specialist. You specialize in:
        - Writing marketing copy: emails, landing pages, social posts, ad copy
        - Designing content calendars and campaign plans
        - Analyzing target audience and positioning
        - Creating SEO-optimized content outlines
        - Measuring campaign effectiveness via defined KPIs
        - A/B test design and result interpretation
        Produce all marketing content as final-draft quality, ready to publish or deploy.
        """;
}
