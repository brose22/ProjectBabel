using UnityEditor;
using UnityEngine;

// Editor helpers for the saved captain bonds, mostly for testing the relationship system.
public static class BondTools
{
    [MenuItem("Babel/Reset Captain Bonds")]
    static void ResetBonds()
    {
        if (!EditorUtility.DisplayDialog("Reset captain bonds",
                "Set every captain's approval back to 0 and bond rank back to D?", "Reset", "Cancel"))
            return;
        ApprovalStore.ResetAll();
        Debug.Log("Captain bonds reset.");
    }

    [MenuItem("Babel/Reveal Bonds Save File")]
    static void RevealSaveFile() => EditorUtility.RevealInFinder(Application.persistentDataPath);
}
