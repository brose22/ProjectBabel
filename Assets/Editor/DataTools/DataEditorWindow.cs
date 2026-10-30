using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Shared tool window for one kind of data asset: a searchable list of every asset of that type on the
// left and an editor for the selected one on the right, or a table of every asset for comparing numbers.
// Edits go through SerializedObject, so Undo/Redo, dirty-marking and saving work as in the Inspector.
public abstract class DataEditorWindow<T> : EditorWindow where T : ScriptableObject
{
    const float RowHeight = 22f;
    const float MinListWidth = 150f;
    const float NameColumnWidth = 130f;
    const float CellWidth = 86f;

    // Where New puts an asset when nothing is selected.
    protected abstract string DefaultFolder { get; }
    // The tab title, set on enable so a window opened without one (or restored by a layout) is named.
    protected abstract string WindowTitle { get; }
    // Serialized fields shown as columns in table mode, in order.
    protected abstract string[] TableColumns { get; }
    // Draws the selected asset's fields. Update and Apply are handled by the caller.
    protected abstract void DrawEditor(SerializedObject so);
    // A small picture for the list row and table, such as a sprite or a colour swatch.
    protected virtual void DrawIcon(Rect rect, T asset) { }
    // Gives a freshly created asset better defaults than the class field initialisers.
    protected virtual void OnCreated(T asset) { }
    // Extra text the search box matches, beyond the file and display names.
    protected virtual string SearchText(T asset) => "";
    // Called when assets anywhere in the project change, for windows that cache lookups.
    protected virtual void OnAssetsChanged() { }

    [SerializeField] string search = "";
    [SerializeField] bool tableMode;
    [SerializeField] string selectedGuid;
    [SerializeField] float listWidth = 220f;
    [SerializeField] string sortColumn;
    [SerializeField] bool sortDescending;

    readonly List<T> assets = new();
    readonly Dictionary<T, SerializedObject> serialized = new();
    Vector2 listScroll, editorScroll, tableScroll;
    bool draggingSplitter;
    bool renaming;
    string renameText;
    double lastNameClick;

    protected T Selected { get; private set; }

    // Opens the window and shows the given asset in it.
    public static TWindow Open<TWindow>(string title, T asset = null) where TWindow : DataEditorWindow<T>
    {
        var window = GetWindow<TWindow>(title);
        window.minSize = new Vector2(520f, 300f);
        if (asset != null)
        {
            window.tableMode = false;
            window.Select(asset);
        }
        return window;
    }

    protected virtual void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
        Undo.undoRedoPerformed += Repaint;
        RefreshAssets();
        if (!string.IsNullOrEmpty(selectedGuid))
            Select(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(selectedGuid)));
    }

    protected virtual void OnDisable() => Undo.undoRedoPerformed -= Repaint;

    // New, moved, renamed and deleted assets show up without reopening the window.
    void OnProjectChange()
    {
        RefreshAssets();
        if (Selected == null) Select(null);
        OnAssetsChanged();
        Repaint();
    }

    // Picking one of these assets in the Project window picks it here too.
    void OnSelectionChange()
    {
        if (Selection.activeObject is T asset && asset != Selected)
        {
            Select(asset);
            Repaint();
        }
    }

    void RefreshAssets()
    {
        assets.Clear();
        assets.AddRange(DataToolsGUI.LoadAll<T>());
        foreach (T gone in serialized.Keys.Where(k => k == null || !assets.Contains(k)).ToList())
        {
            serialized[gone]?.Dispose();
            serialized.Remove(gone);
        }
    }

    protected SerializedObject SerializedFor(T asset)
    {
        if (!serialized.TryGetValue(asset, out SerializedObject so) || so.targetObject == null)
            serialized[asset] = so = new SerializedObject(asset);
        so.UpdateIfRequiredOrScript();
        return so;
    }

    public void Select(T asset)
    {
        Selected = asset;
        selectedGuid = asset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)) : null;
        renaming = false;
        GUI.FocusControl(null);
    }

    IEnumerable<T> Filtered()
    {
        if (string.IsNullOrWhiteSpace(search)) return assets;
        string[] words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return assets.Where(a =>
        {
            string text = $"{a.name} {DisplayName(a)} {SearchText(a)}";
            return words.All(w => text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
        });
    }

    string DisplayName(T asset) => SerializedFor(asset).FindProperty("displayName")?.stringValue ?? "";

    void OnGUI()
    {
        DrawToolbar();
        if (tableMode)
            DrawTable();
        else
            DrawListAndEditor();
    }

    // ---------- Toolbar and asset actions ----------

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            bool table = GUILayout.Toggle(tableMode, tableMode ? "Table" : "List", EditorStyles.toolbarButton, GUILayout.Width(50f));
            if (table != tableMode)
            {
                tableMode = table;
                Relayout();
            }
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80f), GUILayout.MaxWidth(260f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New", EditorStyles.toolbarButton)) { CreateAsset(); Relayout(); }
            using (new EditorGUI.DisabledScope(Selected == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton)) { DuplicateSelected(); Relayout(); }
                if (GUILayout.Button("Rename", EditorStyles.toolbarButton)) { StartRename(); Relayout(); }
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton)) { DeleteSelected(); Relayout(); }
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(Selected);
            }
            if (GUILayout.Button(new GUIContent("Export", "Write every data asset to an Excel workbook."), EditorStyles.toolbarButton)) { DataWorkbook.ExportWithPrompt(); Relayout(); }
            if (GUILayout.Button(new GUIContent("Import", "Update the data assets from an Excel workbook. Results go to the Console."), EditorStyles.toolbarButton)) { DataWorkbook.ImportWithPrompt(); Relayout(); }
        }
    }

    // Ends this GUI pass after an action that changes what the window draws, so the layout and the
    // controls drawn for the same event can't disagree.
    void Relayout()
    {
        Repaint();
        GUIUtility.ExitGUI();
    }

    string TargetFolder()
    {
        string folder = Selected != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(Selected))?.Replace('\\', '/') : DefaultFolder;
        if (!AssetDatabase.IsValidFolder(folder)) folder = "Assets";
        return folder;
    }

    void CreateAsset()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath($"{TargetFolder()}/New{typeof(T).Name.Replace("Data", "")}.asset");
        var asset = CreateInstance<T>();
        OnCreated(asset);
        AssetDatabase.CreateAsset(asset, path);
        SetDisplayName(asset, asset.name);
        AssetDatabase.SaveAssets();
        AfterAdd(asset);
    }

    void DuplicateSelected()
    {
        string path = AssetDatabase.GetAssetPath(Selected);
        string copyPath = AssetDatabase.GenerateUniqueAssetPath(path);
        if (!AssetDatabase.CopyAsset(path, copyPath)) return;
        AfterAdd(AssetDatabase.LoadAssetAtPath<T>(copyPath));
    }

    void AfterAdd(T asset)
    {
        RefreshAssets();
        tableMode = false;
        Select(asset);
        StartRename();
    }

    void StartRename()
    {
        tableMode = false;
        renaming = true;
        renameText = Selected.name;
        EditorApplication.delayCall += () => EditorGUI.FocusTextInControl("RenameField");
    }

    void ApplyRename()
    {
        renaming = false;
        string oldName = Selected.name;
        string newName = renameText.Trim();
        if (newName.Length == 0 || newName == oldName) return;
        string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(Selected), newName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Rename failed", error, "OK");
            return;
        }
        // A display name that only mirrored the file name follows it.
        if (DisplayName(Selected) == oldName) SetDisplayName(Selected, newName);
        RefreshAssets();
    }

    void SetDisplayName(T asset, string value)
    {
        SerializedObject so = SerializedFor(asset);
        so.Update();
        SerializedProperty property = so.FindProperty("displayName");
        if (property == null) return;
        property.stringValue = value;
        so.ApplyModifiedProperties();
    }

    void DeleteSelected()
    {
        string path = AssetDatabase.GetAssetPath(Selected);
        List<UnityEngine.Object> users = DataToolsGUI.FindUsers(Selected);
        string usedBy = users.Count > 0
            ? $"\n\nStill used by: {string.Join(", ", users.Select(u => u.name))}. Those references will become empty."
            : "";
        if (!EditorUtility.DisplayDialog($"Delete {Selected.name}?", $"Move {path} to the trash?{usedBy}", "Delete", "Cancel"))
            return;
        int index = Filtered().ToList().IndexOf(Selected);
        AssetDatabase.MoveAssetToTrash(path);
        RefreshAssets();
        List<T> remaining = Filtered().ToList();
        Select(remaining.Count > 0 ? remaining[Mathf.Clamp(index, 0, remaining.Count - 1)] : null);
    }

    // ---------- List and editor ----------

    void DrawListAndEditor()
    {
        Rect area = new(0f, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight);
        listWidth = Mathf.Clamp(listWidth, MinListWidth, Mathf.Max(MinListWidth, area.width - 250f));
        Rect listRect = new(area.x, area.y, listWidth, area.height);
        Rect splitter = new(listRect.xMax, area.y, 4f, area.height);
        Rect editorRect = new(splitter.xMax, area.y, area.width - splitter.xMax, area.height);

        DrawList(listRect);
        DrawSplitter(splitter);
        GUILayout.BeginArea(new Rect(editorRect.x + 6f, editorRect.y + 4f, editorRect.width - 10f, editorRect.height - 4f));
        DrawSelectedEditor();
        GUILayout.EndArea();
    }

    void DrawList(Rect rect)
    {
        List<T> shown = Filtered().ToList();
        HandleListKeys(shown);
        Rect view = new(0f, 0f, rect.width - 14f, shown.Count * RowHeight);
        listScroll = GUI.BeginScrollView(rect, listScroll, view);
        for (int i = 0; i < shown.Count; i++)
        {
            T asset = shown[i];
            Rect row = new(0f, i * RowHeight, Mathf.Max(view.width, rect.width), RowHeight);
            if (asset == Selected)
                EditorGUI.DrawRect(row, DataToolsGUI.SelectionColor);
            else if (i % 2 == 1)
                EditorGUI.DrawRect(row, DataToolsGUI.StripeColor);
            DrawIcon(new Rect(row.x + 4f, row.y + 2f, RowHeight - 4f, RowHeight - 4f), asset);
            string displayName = DisplayName(asset);
            string label = displayName.Length == 0 || displayName == asset.name ? asset.name : $"{asset.name}  <color=#888>{displayName}</color>";
            GUI.Label(new Rect(row.x + RowHeight + 4f, row.y + 2f, row.width - RowHeight - 6f, RowHeight - 4f), label, DataToolsGUI.RichLabel);
            if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
            {
                Select(asset);
                if (Event.current.clickCount == 2) EditorGUIUtility.PingObject(asset);
                Event.current.Use();
                Relayout();
            }
        }
        if (shown.Count == 0)
            GUI.Label(new Rect(6f, 4f, rect.width - 12f, 40f), assets.Count == 0 ? $"No {typeof(T).Name} assets yet." : "Nothing matches.", EditorStyles.wordWrappedMiniLabel);
        GUI.EndScrollView();
    }

    // Up and down step through the list while no text field has focus.
    void HandleListKeys(List<T> shown)
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField || shown.Count == 0) return;
        int step = e.keyCode switch { KeyCode.UpArrow => -1, KeyCode.DownArrow => 1, _ => 0 };
        if (step == 0) return;
        int index = Mathf.Clamp(shown.IndexOf(Selected) + step, 0, shown.Count - 1);
        Select(shown[index]);
        listScroll.y = Mathf.Clamp(listScroll.y, (index + 1) * RowHeight - position.height + 40f, index * RowHeight);
        e.Use();
        Relayout();
    }

    void DrawSplitter(Rect rect)
    {
        EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y, 1f, rect.height), DataToolsGUI.LineColor);
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            draggingSplitter = true;
            e.Use();
        }
        else if (draggingSplitter && e.type == EventType.MouseDrag)
        {
            listWidth = e.mousePosition.x;
            Repaint();
            e.Use();
        }
        else if (e.rawType == EventType.MouseUp)
            draggingSplitter = false;
    }

    void DrawSelectedEditor()
    {
        if (Selected == null)
        {
            EditorGUILayout.HelpBox($"Pick a {typeof(T).Name} on the left, or press New.", MessageType.Info);
            return;
        }

        DrawHeader();
        EditorGUILayout.Space(4f);
        SerializedObject so = SerializedFor(Selected);
        so.Update();
        editorScroll = EditorGUILayout.BeginScrollView(editorScroll);
        DrawEditor(so);
        EditorGUILayout.EndScrollView();
        so.ApplyModifiedProperties();
    }

    void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (renaming)
            {
                // Enter and Escape are read before the text field eats them.
                Event e = Event.current;
                if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "RenameField")
                {
                    if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) { ApplyRename(); e.Use(); Relayout(); }
                    else if (e.keyCode == KeyCode.Escape) { renaming = false; e.Use(); Relayout(); }
                }
                GUI.SetNextControlName("RenameField");
                renameText = EditorGUILayout.TextField(renameText);
                if (GUILayout.Button("Apply", GUILayout.Width(50f))) { ApplyRename(); Relayout(); }
                if (GUILayout.Button("Cancel", GUILayout.Width(55f))) { renaming = false; Relayout(); }
            }
            else
            {
                GUILayout.Label(Selected.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(AssetDatabase.GetAssetPath(Selected), EditorStyles.miniLabel);
            }
        }
    }

    // ---------- Table ----------

    void DrawTable()
    {
        List<T> shown = Filtered().ToList();
        SortRows(shown);
        string[] columns = TableColumns;
        float iconWidth = RowHeight;
        float width = iconWidth + NameColumnWidth + columns.Length * CellWidth;
        Rect area = new(0f, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight);

        // The header scrolls sideways with the rows but stays put vertically.
        Rect headerRect = new(area.x, area.y, area.width, RowHeight + 14f);
        GUI.BeginClip(headerRect);
        float x = -tableScroll.x + iconWidth;
        DrawHeaderCell(new Rect(x, 0f, NameColumnWidth, headerRect.height), "Name", null);
        x += NameColumnWidth;
        SerializedObject sample = shown.Count > 0 ? SerializedFor(shown[0]) : null;
        foreach (string column in columns)
        {
            string label = sample?.FindProperty(column)?.displayName ?? ObjectNames.NicifyVariableName(column);
            DrawHeaderCell(new Rect(x, 0f, CellWidth, headerRect.height), label, column);
            x += CellWidth;
        }
        GUI.EndClip();

        Rect body = new(area.x, headerRect.yMax, area.width, area.height - headerRect.height);
        tableScroll = GUI.BeginScrollView(body, tableScroll, new Rect(0f, 0f, width, shown.Count * RowHeight));
        for (int i = 0; i < shown.Count; i++)
        {
            T asset = shown[i];
            Rect row = new(0f, i * RowHeight, Mathf.Max(width, body.width), RowHeight);
            if (asset == Selected)
                EditorGUI.DrawRect(row, DataToolsGUI.SelectionColor);
            else if (i % 2 == 1)
                EditorGUI.DrawRect(row, DataToolsGUI.StripeColor);
            DrawIcon(new Rect(2f, row.y + 2f, RowHeight - 4f, RowHeight - 4f), asset);
            Rect nameRect = new(iconWidth, row.y + 2f, NameColumnWidth - 4f, RowHeight - 4f);
            // One click picks the row, two open it in the editor.
            if (GUI.Button(nameRect, asset.name, EditorStyles.label))
            {
                if (asset == Selected && EditorApplication.timeSinceStartup - lastNameClick < 0.4)
                    tableMode = false;
                Select(asset);
                lastNameClick = EditorApplication.timeSinceStartup;
                Relayout();
            }

            SerializedObject so = SerializedFor(asset);
            so.Update();
            x = iconWidth + NameColumnWidth;
            foreach (string column in columns)
            {
                SerializedProperty property = so.FindProperty(column);
                if (property != null)
                    EditorGUI.PropertyField(new Rect(x + 2f, row.y + 2f, CellWidth - 4f, RowHeight - 4f), property, GUIContent.none);
                x += CellWidth;
            }
            so.ApplyModifiedProperties();
        }
        GUI.EndScrollView();
    }

    void DrawHeaderCell(Rect rect, string label, string column)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), DataToolsGUI.LineColor);
        bool sorted = sortColumn == column;
        string text = sorted ? $"{label} {(sortDescending ? "▼" : "▲")}" : label;
        if (GUI.Button(new Rect(rect.x + 2f, rect.y, rect.width - 4f, rect.height), new GUIContent(text, label), DataToolsGUI.HeaderLabel))
        {
            // Clicking a header sorts by it, again flips it, a third time goes back to name order.
            if (!sorted) { sortColumn = column; sortDescending = false; }
            else if (!sortDescending) sortDescending = true;
            else sortColumn = null;
        }
    }

    void SortRows(List<T> rows)
    {
        if (string.IsNullOrEmpty(sortColumn)) return;
        var keys = rows.ToDictionary(r => r, r => DataToolsGUI.SortKey(SerializedFor(r).FindProperty(sortColumn)));
        rows.Sort((a, b) =>
        {
            int order = Comparer<IComparable>.Default.Compare(keys[a], keys[b]);
            return sortDescending ? -order : order;
        });
    }
}
