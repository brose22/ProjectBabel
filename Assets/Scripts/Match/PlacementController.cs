using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Mouse, keyboard and gamepad control for the match: place towers on free tiles, select them,
// change their Gold draw rate, move them and sell them. The gamepad works a tile cursor where the
// mouse points; both feed the same place, select and move logic.
public class PlacementController : MonoBehaviour
{
    [SerializeField] MatchManager match;
    [SerializeField] GridMap grid;
    [SerializeField] MatchHUD hud;
    [SerializeField] SpriteRenderer hoverMarker;
    [Tooltip("Tile frame drawn on a tile the player can use (place, select or move to).")]
    [SerializeField] Sprite validSprite;
    [Tooltip("Tile frame drawn on a tile the player cannot use.")]
    [SerializeField] Sprite invalidSprite;

    public Tower Selected { get; private set; }
    // While moving, clicks pick a new tile for the selected tower instead of placing or selecting.
    public bool IsMoving { get; private set; }
    // Whether gamepad focus is in the selected captain's widget, whose buttons the tile cursor can't reach.
    public bool WidgetFocused { get; set; }

    const float CursorRepeatDelay = 0.25f;
    const float CursorRepeatInterval = 0.09f;

    readonly StepRepeat cursorRepeat = new(CursorRepeatDelay, CursorRepeatInterval);
    Camera cam;
    CameraPan cameraPan;
    PlacementGhosts ghosts;
    Vector2Int cursorCell = new(-1, -1);
    CaptainRequests requests;

    // Added to the match at runtime, so it is looked up rather than wired in the scene.
    CaptainRequests Requests => requests != null ? requests : requests = match.GetComponent<CaptainRequests>();
    // Added to the camera at runtime too.
    CameraPan CameraPan => cameraPan != null ? cameraPan : cameraPan = cam.GetComponent<CameraPan>();

    // The confirm and cancel buttons answer an open request instead of acting on the map.
    bool IsAsked => Requests != null && Requests.Active != null;

    void Awake()
    {
        cam = Camera.main;
        ghosts = new PlacementGhosts(hoverMarker);
    }

    void Update()
    {
        HandleKeys();

        bool ended = match.State is MatchState.Won or MatchState.Lost;
        if (IsMoving && !match.CanMove(Selected)) IsMoving = false;
        bool pad = GameInput.UsingGamepad;
        Mouse mouse = Mouse.current;
        Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        // A hidden mouse's position means nothing, so the gamepad only asks whether a menu is up.
        bool blocked = pad ? match.IsOver || match.IsBetweenMatches || match.IsPaused || UIFocusScope.Top != null || IsAsked
            : mouse == null || hud.IsPointerOverHud(screen);
        if (ended || blocked)
        {
            hoverMarker.enabled = false;
            ghosts.Hide();
            return;
        }

        // The gamepad's cancel button goes through HandleKeys, like Esc.
        Vector2Int cell = pad ? StepCursor() : grid.WorldToCell(cam.ScreenToWorldPoint(screen));
        bool confirm = pad ? GameInput.ConfirmTile : mouse.leftButton.wasPressedThisFrame;
        bool back = !pad && mouse.rightButton.wasPressedThisFrame;
        if (IsMoving)
        {
            UpdateMove(cell, confirm, back);
            return;
        }

        Tower hovered = grid.GetOccupant(cell);
        bool canBuild = match.CanPlaceOn(cell);

        ShowMarker(cell, hovered != null || canBuild);
        if (canBuild) ShowGhosts(cell, match.NextCaptain, match.SlotClassesFor(match.NextCaptain));
        else ghosts.Hide();

        // Placing leaves the new captain unselected, so its widget only opens when clicked.
        if (confirm)
        {
            // The widget's buttons have no keys on the gamepad, so confirming again goes into it.
            if (pad && hovered != null && hovered == Selected) WidgetFocused = true;
            else if (hovered != null) Select(hovered);
            else
            {
                if (canBuild) match.PlaceTower(cell);
                Select(null);
            }
        }
        else if (back)
        {
            Select(null);
        }
    }

    // Moves the gamepad's tile cursor, which starts mid-map, and keeps it on screen.
    Vector2Int StepCursor()
    {
        if (!grid.InBounds(cursorCell)) cursorCell = grid.Size / 2;
        Vector2Int step = cursorRepeat.Update(PadMath.ToStep(GameInput.PadMove), Time.unscaledTime);
        if (step == Vector2Int.zero) return cursorCell;
        if (grid.InBounds(cursorCell + step)) cursorCell += step;
        CameraPan.KeepInView(grid.CellCenter(cursorCell));
        return cursorCell;
    }

    // Puts the tile cursor on a tile, for jumping to a captain from the party bar.
    public void JumpCursor(Vector2Int cell)
    {
        cursorCell = cell;
        CameraPan.KeepInView(grid.CellCenter(cell));
    }

    // Consumes every click while moving so it can't also select or place.
    void UpdateMove(Vector2Int cell, bool confirm, bool back)
    {
        bool valid = match.CanMoveTo(Selected, cell) && match.CanAffordMove(Selected);
        ShowMarker(cell, valid);
        if (valid) ShowGhosts(cell, Selected.Captain, Selected.SlotClasses(), Selected);
        else ghosts.Hide();

        if (confirm)
        {
            // Deselecting keeps the widget closed while the captain walks.
            if (valid && match.MoveTower(Selected, cell)) Select(null);
        }
        else if (back)
        {
            CancelMove();
        }
    }

    // Frames the hovered tile with the valid or invalid art; out-of-bounds tiles get no frame.
    void ShowMarker(Vector2Int cell, bool valid)
    {
        hoverMarker.enabled = grid.InBounds(cell);
        hoverMarker.transform.position = grid.CellCenter(cell);
        hoverMarker.sprite = valid ? validSprite : invalidSprite;
        hoverMarker.color = Color.white;
    }

    void ShowGhosts(Vector2Int cell, CaptainData captain, IReadOnlyList<SoldierClassData> classes, Tower moving = null) =>
        ghosts.Show(grid, cell, captain, Tower.PickHomes(grid, cell, classes, moving), classes);

    void HandleKeys()
    {
        // The pause menu owns the input while it's up; its own Resume handling closes it.
        if (match.IsPaused) return;
        if (GameInput.StartWave) match.StartNextWave();
        if (GameInput.Cancel)
        {
            if (IsMoving) CancelMove();
            else Select(null);
        }
        if (Selected == null) return;

        if (GameInput.MoveTower)
        {
            if (IsMoving) CancelMove();
            else BeginMove();
        }
        // Gold draw follows captain requests; the keys only set it while the F2 debug panel is open.
        if (hud.IsDebugPanelOpen)
        {
            if (GameInput.KeyDown(Key.Digit1)) Selected.SetDrawRate(DrawRate.Low);
            if (GameInput.KeyDown(Key.Digit2)) Selected.SetDrawRate(DrawRate.Normal);
            if (GameInput.KeyDown(Key.Digit3)) Selected.SetDrawRate(DrawRate.High);
        }
        if (GameInput.SellTower) SellSelected();
    }

    public void Select(Tower tower)
    {
        IsMoving = false;
        WidgetFocused = false;
        if (Selected != null) Selected.SetSelected(false);
        Selected = tower;
        if (Selected != null) Selected.SetSelected(true);
    }

    public void SellSelected()
    {
        if (Selected == null) return;
        match.SellTower(Selected);
        Selected = null;
        IsMoving = false;
        WidgetFocused = false;
    }

    public void BeginMove()
    {
        if (!match.CanMove(Selected) || !match.CanAffordMove(Selected)) return;
        IsMoving = true;
        WidgetFocused = false;
    }

    public void CancelMove() => IsMoving = false;
}
