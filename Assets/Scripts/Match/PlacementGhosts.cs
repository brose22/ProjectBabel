using System.Collections.Generic;
using UnityEngine;

// A see-through captain and soldiers showing how a tower would stand on a tile, drawn while the
// player picks one. Sprites are made on demand and reused.
public class PlacementGhosts
{
    const float Opacity = 0.55f;
    // Art-less captains and soldiers are squares at their prefab sizes.
    const float CaptainSquareScale = 0.8f;
    const float SoldierSquareScale = 0.3f;

    readonly SpriteRenderer template;
    readonly SpriteRenderer captainGhost;
    readonly List<SpriteRenderer> soldierGhosts = new();

    // Ghosts draw just above the template, with its material and fallback square.
    public PlacementGhosts(SpriteRenderer template)
    {
        this.template = template;
        captainGhost = MakeGhost("Captain Ghost");
    }

    public void Show(GridMap grid, Vector2Int cell, CaptainData captain, IReadOnlyList<Vector2Int> homes, IReadOnlyList<SoldierClassData> classes)
    {
        Color captainColor = captain.sprite == null || captain.tintSprite ? captain.color : Color.white;
        Draw(captainGhost, captain.sprite, captainColor, grid.CellCenter(cell), CaptainSquareScale);

        while (soldierGhosts.Count < homes.Count) soldierGhosts.Add(MakeGhost("Soldier Ghost"));
        for (int i = 0; i < soldierGhosts.Count; i++)
        {
            if (i >= homes.Count)
            {
                soldierGhosts[i].enabled = false;
                continue;
            }
            SoldierClassData soldierClass = classes[i];
            Color color = soldierClass.sprite == null ? soldierClass.color : Color.white;
            Draw(soldierGhosts[i], soldierClass.sprite, color, grid.CellCenter(homes[i]), SoldierSquareScale);
        }
    }

    public void Hide()
    {
        captainGhost.enabled = false;
        foreach (SpriteRenderer ghost in soldierGhosts)
            ghost.enabled = false;
    }

    void Draw(SpriteRenderer ghost, Sprite art, Color color, Vector3 position, float squareScale)
    {
        color.a = Opacity;
        ghost.enabled = true;
        ghost.sprite = art != null ? art : template.sprite;
        ghost.color = color;
        ghost.transform.position = position;
        ghost.transform.localScale = Vector3.one * (art != null ? 1f : squareScale);
    }

    SpriteRenderer MakeGhost(string name)
    {
        var ghost = new GameObject(name).AddComponent<SpriteRenderer>();
        ghost.transform.SetParent(template.transform.parent, false);
        ghost.sharedMaterial = template.sharedMaterial;
        ghost.sortingLayerID = template.sortingLayerID;
        ghost.sortingOrder = template.sortingOrder + 1;
        ghost.enabled = false;
        return ghost;
    }
}
