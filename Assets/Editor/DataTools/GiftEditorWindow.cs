using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits the gifts shops sell: icon, price and approval values, and the captains who like or
// dislike each one (set on the captain).
public class GiftEditorWindow : DataEditorWindow<GiftData>
{
    const string Title = "Gifts";

    protected override string WindowTitle => Title;
    const float PreviewSize = 64f;

    List<CaptainData> captains;

    [MenuItem("Babel/Data/Gifts", priority = 25)]
    static void ShowWindow() => Open<GiftEditorWindow>(Title);

    public static void OpenFor(GiftData gift) => Open<GiftEditorWindow>(Title, gift);

    protected override string DefaultFolder => "Assets/Data/Gifts";

    protected override string[] TableColumns => new[]
    {
        nameof(GiftData.cost), nameof(GiftData.neutralApproval), nameof(GiftData.likedApproval), nameof(GiftData.dislikedApproval),
    };

    protected override void OnAssetsChanged() => captains = null;

    protected override void DrawIcon(Rect rect, GiftData gift) =>
        DataToolsGUI.DrawSprite(rect, gift.icon, gift.icon == null ? Color.gray : Color.white);

    protected override void DrawEditor(SerializedObject so)
    {
        var gift = (GiftData)so.targetObject;
        using (new EditorGUILayout.HorizontalScope())
        {
            DataToolsGUI.SpritePreview(gift.icon, gift.icon == null ? Color.gray : Color.white, PreviewSize);
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Field(so, nameof(GiftData.displayName));
                DataToolsGUI.Field(so, nameof(GiftData.icon));
                DataToolsGUI.Field(so, nameof(GiftData.cost));
            }
        }
        DataToolsGUI.Field(so, nameof(GiftData.description));
        DataToolsGUI.Field(so, nameof(GiftData.neutralApproval));
        DataToolsGUI.Field(so, nameof(GiftData.likedApproval));
        DataToolsGUI.Field(so, nameof(GiftData.dislikedApproval));

        DataToolsGUI.Section("Opinions");
        captains = DataToolsGUI.LoadAll(captains);
        DataToolsGUI.AssetButtons("Liked pool of", captains.Where(c => c != null && c.likedGiftPool.Contains(gift)), CaptainEditorWindow.OpenFor);
        DataToolsGUI.AssetButtons("Disliked pool of", captains.Where(c => c != null && c.dislikedGiftPool.Contains(gift)), CaptainEditorWindow.OpenFor);
    }
}
