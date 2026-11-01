// How a captain's request ends. Expired is a request still open when its wave ended: it is dismissed
// without a verdict, so it neither pleases nor angers the captain.
public enum RequestOutcome { Refused, Granted, Expired }

public static class RequestRules
{
    // The approval change for an outcome, given the reward for a full yes and the refusal penalty.
    public static float ApprovalChange(RequestOutcome outcome, float reward, float refusalPenalty) => outcome switch
    {
        RequestOutcome.Refused => -refusalPenalty,
        RequestOutcome.Granted => reward,
        _ => 0f,
    };
}
