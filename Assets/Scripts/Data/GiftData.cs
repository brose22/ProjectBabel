using UnityEngine;

public enum GiftReaction { Neutral, Liked, Disliked }

// An item bought at a shop and given to a captain from the party's details window. Items last for
// the run only. Which captains like or dislike an item is set on the captain (CaptainData).
[CreateAssetMenu(menuName = "Babel/Gift", fileName = "Gift")]
public class GiftData : ScriptableObject
{
    public string displayName = "Gift";
    [TextArea] public string description;
    public Sprite icon;
    [Tooltip("Gold the shop charges for one.")]
    public int cost = 75;

    [Header("Approval (runs -100 to 100)")]
    [Tooltip("Added when the captain has no opinion of it.")]
    public float neutralApproval = 5f;
    [Tooltip("Added when the captain likes it.")]
    public float likedApproval = 15f;
    [Tooltip("Added (use a negative number) when the captain dislikes it.")]
    public float dislikedApproval = -10f;

    public float ApprovalFor(GiftReaction reaction) => reaction switch
    {
        GiftReaction.Liked => likedApproval,
        GiftReaction.Disliked => dislikedApproval,
        _ => neutralApproval,
    };
}
