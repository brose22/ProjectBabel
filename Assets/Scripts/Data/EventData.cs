using System.Collections.Generic;
using UnityEngine;

// A world map event: a short scene and two or three choices. Event nodes pick one from the match
// data's pool.
[CreateAssetMenu(menuName = "Babel/Event", fileName = "Event")]
public class EventData : ScriptableObject
{
    public string title = "Event";
    [TextArea] public string text = "";
    public List<EventChoice> choices = new();
}
