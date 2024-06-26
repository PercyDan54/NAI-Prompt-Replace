namespace NAIPromptReplace.Models;

public class SubscriptionInfo
{
    private DateTimeOffset expiresAt;
    public SubscriptionTier Tier { get; set; }
    public bool Active { get; set; }

    public long ExpiresAt
    {
        get => expiresAt.ToUnixTimeSeconds();
        set => expiresAt = DateTimeOffset.UnixEpoch.AddSeconds(value).ToLocalTime();
    }

    public TrainingSteps TrainingStepsLeft { get; set; } = new TrainingSteps();

    public int TotalTrainingStepsLeft => TrainingStepsLeft.FixedTrainingStepsLeft + TrainingStepsLeft.PurchasedTrainingSteps;

    public override string ToString() => $"Active: {Active}, Tier: {Tier}, Expire{(Active ? "s" : "d")} at: {expiresAt}";
}

public class TrainingSteps
{
    public int FixedTrainingStepsLeft { get; set; }
    public int PurchasedTrainingSteps { get; set; }
}

public enum SubscriptionTier
{
    Paper,
    Tablet,
    Scroll,
    Opus
}
