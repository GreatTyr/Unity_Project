using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Faction
{
    public string shortName;  // PRT-1
    public string fullName;   // Pirates of 1st circle
    // —юда легко добавить: public string honorific; // "господин"
}

[CreateAssetMenu(fileName = "FactionDatabase", menuName = "Game/Faction Database")]
public class FactionDatabase : ScriptableObject
{
    private static FactionDatabase _instance;

    public static FactionDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<FactionDatabase>("FactionDatabase");
            return _instance;
        }
    }

    public List<Faction> factions = new();

    public Faction Get(string shortName) =>
        factions.Find(f => f.shortName == shortName);

    public string GetFullName(string shortName) =>
        Get(shortName)?.fullName ?? shortName;
}