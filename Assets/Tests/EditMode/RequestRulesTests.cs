using NUnit.Framework;

public class RequestRulesTests
{
    [TestCase(RequestOutcome.Refused, -3f)]
    [TestCase(RequestOutcome.Partial, 1f)]
    [TestCase(RequestOutcome.Granted, 2f)]
    [TestCase(RequestOutcome.Expired, 0f)]
    public void ApprovalChange_ByOutcome(RequestOutcome outcome, float expected) =>
        Assert.That(RequestRules.ApprovalChange(outcome, 2f, 3f), Is.EqualTo(expected));
}
