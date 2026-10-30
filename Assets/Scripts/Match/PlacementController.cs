using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Mouse and keyboard control for the match: place towers on free tiles, select them,
// change their Gold draw rate, move them and sell them.
public class PlacementController : MonoBehaviour
{
    [SerializeField] MatchManager match;
    [SerializeField] GridMap grid;
    [SerializeField] MatchHUD hud;
    [SerializeField] SpriteRenderer hoverMarker;
    [SerializeField] Color validColor = new(0.3f, 1f, 0.3f, 0.45f);
    [SerializeField] Color invalidColor = new(1f, 0.3f, 0.3f, 0.45f);

    public Tower Selected { get; private set; }
    // While moving, clicks pick a new tile for the selected tower instead of placing or selecting.
    public bool IsMoving { get; private set; }

    Camera cam;
    PlacementGhosts ghosts;

    void Awake()
    {
        cam = Camera.main;
        ghosts = new PlacementGhosts(hoverMarker);
    }

    void Update()
    {
        HandleKeys();

        Mouse mouse = Mouse.current;
        Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool ended = match.State is MatchState.Won or MatchState.Lost;
        if (IsMoving && !match.CanMove(Selected)) IsMoving = false;
        if (mouse == null || ended || hud.IsPointerOverHud(screen))
        {
            hoverMarker.enabled = false;
            ghosts.Hide();
            return;
        }

        Vector2Int cell = grid.WorldToCell(cam.ScreenToWorldPoint(screen));
        if (IsMoving)
        {
            UpdateMove(mouse, cell);
            return;
        }

        Tower hovered = grid.GetOccupant(cell);
        bool canBuild = match.CanPlaceOn(cell);

        hoverMarker.enabled = grid.InBounds(cell);
        hoverMarker.transform.position = grid.CellCenter(cell);
        hoverMarker.color = hovered != null || canBuild ? validColor : invalidColor;
        if (canBuild) ShowGhosts(cell, match.NextCaptain, match.SlotClassesFor(match.NextCaptain));
        else ghosts.Hide();

        // Placing leaves the new captain unselected, so its widget only opens when clicked.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (hovered != null) Select(hovered);
            else
            {
                if (canBuild) match.PlaceTower(cell);
                Select(null);
            }
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            Select(null);
        }
    }

    // Consumes every click while moving so it can't also select or place.
    void UpdateMove(Mouse mouse, Vector2Int cell)
    {
        bool valid = match.CanMoveTo(Selected, cell) && match.CanAffordMove(Selected);
        hoverMarker.enabled = grid.InBounds(cell);
        hoverMarker.transform.position = grid.CellCenter(cell);
        hoverMarker.color = valid ? validColor : invalidColor;
        if (valid) ShowGhosts(cell, Selected.Captain, Selected.SlotClasses(), Selected);
        else ghosts.Hide();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (valid && match.MoveTower(Selected, cell)) IsMoving = false;
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelMove();
        }
    }

    void ShowGhosts(Vector2Int cell, CaptainData captain, IReadOnlyList<SoldierClassData> classes, Tower moving = null) =>
        ghosts.Show(grid, cell, captain, Tower.PickHomes(grid, cell, classes, moving), classes);

    void HandleKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame) match.StartNextWave();
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (IsMoving) CancelMove();
            else Select(null);
        }
        if (Selected == null) return;

        if (keyboard.mKey.wasPressedThisFrame)
        {
            if (IsMoving) CancelMove();
            else BeginMove();
        }
        // Gold draw follows captain requests; the keys only set it while the F2 debug panel is open.
        if (hud.IsDebugPanelOpen)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) Selected.SetDrawRate(DrawRate.Low);
            if (keyboard.digit2Key.wasPressedThisFrame) Selected.SetDrawRate(DrawRate.Normal);
            if (keyboard.digit3Key.wasPressedThisFrame) Selected.SetDrawRate(DrawRate.High);
        }
        if (keyboard.xKey.wasPressedThisFrame) SellSelected();
    }

    public void Select(Tower tower)
    {
        IsMoving = false;
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
    }

    public void BeginMove()
    {
        if (!match.CanMove(Selected) || !match.CanAffordMove(Selected)) return;
        IsMoving = true;
    }

    public void CancelMove() => IsMoving = false;
}
