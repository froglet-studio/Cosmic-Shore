using System;
using System.Collections.Generic;


public enum ContentTypes
{
    Ship = 1,
    Game = 2,
}

public enum UnlockCondition
{
    HighScore = 1,
    PlayAsShip = 2,
    PlayGameMode = 3,
}

public struct UnlockRequirement
{
    // Play as ship # of times -> ship type, count
    // High score in game mode -> game mode, score
    // Play a game # of times -> game mode, count
    public Type ContentType;
    public int ContentID;
    public UnlockCondition Condition;
    public int UnlockThreshold;
    
    public UnlockRequirement(UnlockCondition condition, int unlockThreshold, Type contentType, int contentID)
    {
        Condition = condition;
        UnlockThreshold = unlockThreshold;
        ContentType = contentType;
        ContentID = contentID;
    }
}

public class ContentUnlockRequirements
{
    public Dictionary<ShipTypes, UnlockRequirement> ShipUnlockRequirements = new()
    {
        { ShipTypes.GunManta, new UnlockRequirement(UnlockCondition.PlayAsShip, 1, typeof(ShipTypes), (int) ShipTypes.Dolphin) },
    };
    public Dictionary<MiniGames, UnlockRequirement> GameUnlockRequirements;
    public Dictionary<int, UnlockRequirement> GameDifficultyUnlockRequirements;
}

[System.Serializable]
public class UnlockedContent
{
    public bool ShipUnlockedIndicatorActive;
    public List<ShipTypes> unlockedShipIndicators;
    public List<ShipTypes> unlockedShips;
    public List<MiniGames> unlockedGames;
    public Dictionary<MiniGames, List<int>> unlockedGameDifficulties;

    public void ClearUnlockedShipIndicator(ShipTypes ship)
    {
        if (unlockedShipIndicators != null)
            unlockedShipIndicators.Remove(ship);
    }

    public bool IsShipUnlocked(ShipTypes ship)
    {
        return unlockedShips != null && unlockedShips.Contains(ship);
    }

    public bool IsGameUnlocked(MiniGames game)
    {
        return unlockedGames != null && unlockedGames.Contains(game);
    }

    public bool IsGameDifficultyUnlocked(MiniGames game, int difficulty)
    {
        return unlockedGameDifficulties != null && 
               unlockedGameDifficulties.ContainsKey(game) && 
               unlockedGameDifficulties[game].Contains(difficulty);
    }

    public bool IsShipUnlockIndicatorActive(ShipTypes ship)
    {
        return unlockedShipIndicators != null && unlockedShipIndicators.Contains(ship);
    }

    public void UnlockShip(ShipTypes shipType)
    {
        unlockedShips ??= new List<ShipTypes>();

        if (!unlockedShips.Contains(shipType))
            unlockedShips.Add(shipType);
    }

    public void UnlockGame(MiniGames game)
    {
        unlockedGames ??= new List<MiniGames>();

        if (!unlockedGames.Contains(game))
            unlockedGames.Add(game);

        UnlockGameDifficulty(game, 0);
    }

    public void UnlockGameDifficulty(MiniGames game, int difficulty)
    {
        unlockedGameDifficulties ??= new();

        if (!unlockedGameDifficulties.ContainsKey(game))
            unlockedGameDifficulties.Add(game, new List<int>(difficulty));
        else if (!unlockedGameDifficulties[game].Contains(difficulty))
            unlockedGameDifficulties[game].Add(difficulty);
    }
}

public class UnlockableContentDataAccessor
{
    static readonly string SaveFileName = "Content.data";

    //public static Dictionary<ShipTypes, Dictionary<MiniGames, bool>> Unlocked

    public static UnlockedContent UnlockedContent;

    public static void Save()
    {
        if (UnlockedContent == null)
            Load();

        //if (Leaderboard.ContainsKey(mode))
        //    Leaderboard[mode] = leaderboard;
        //else
        //    Leaderboard.Add(mode, leaderboard);

        DataAccessor dataAccessor = new DataAccessor(SaveFileName);
        dataAccessor.Save(UnlockedContent);
    }

    public static UnlockedContent Load()
    {
        DataAccessor dataAccessor = new DataAccessor(SaveFileName);
        UnlockedContent = dataAccessor.Load<UnlockedContent>();
        return UnlockedContent;
    }

    public static UnlockedContent UnlockableContentDefault = new()
    {
        
    };
}