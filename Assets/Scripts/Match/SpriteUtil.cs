using UnityEngine;

// Runtime sprite helpers for effects that need a plain shape rather than a unit's art.
public static class SpriteUtil
{
    static Sprite square;

    // A white square one world unit across, for streaks, bars and markers drawn at runtime.
    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                square = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width);
            }
            return square;
        }
    }
}
