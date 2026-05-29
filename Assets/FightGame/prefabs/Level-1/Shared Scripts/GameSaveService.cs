using UnityEngine;

/// <summary>Прогресс кампании по слотам: старт уровня с начала, без mid-scene состояния.</summary>
public enum SaveProgressStage
{
    None = 0,
    Tutorial = 1,
    Level1 = 2,
    Level2 = 3,
    Level3 = 4,
}

public static class GameSaveService
{
    public const int SlotCount = 3;

    const string HasSaveKeyPrefix = "FightGame_Slot_HasSave_";
    const string StageKeyPrefix = "FightGame_Slot_Stage_";
    const string ActiveSlotKey = "FightGame_ActiveSlot";
    const string LastUsedSlotKey = "FightGame_LastUsedSlot";

    public static int ActiveSlot { get; private set; } = -1;

    public static bool HasSave(int slotIndex) => GetStage(slotIndex) != SaveProgressStage.None;

    public static bool HasAnySave()
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            if (HasSave(i))
                return true;
        }

        return false;
    }

    public static SaveProgressStage GetStage(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return SaveProgressStage.None;

        if (PlayerPrefs.GetInt(HasSaveKeyPrefix + slotIndex, 0) == 0)
            return SaveProgressStage.None;

        return (SaveProgressStage)PlayerPrefs.GetInt(StageKeyPrefix + slotIndex, (int)SaveProgressStage.Tutorial);
    }

    public static void CreateNewGame(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return;

        ActiveSlot = slotIndex;
        WriteSlot(slotIndex, SaveProgressStage.Tutorial);
    }

    public static void SetActiveSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex) || !HasSave(slotIndex))
            return;

        ActiveSlot = slotIndex;
        PlayerPrefs.SetInt(ActiveSlotKey, slotIndex);
        PlayerPrefs.SetInt(LastUsedSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    public static void RestoreActiveSlotFromPrefs()
    {
        int slot = PlayerPrefs.GetInt(ActiveSlotKey, -1);
        if (IsValidSlot(slot) && HasSave(slot))
            ActiveSlot = slot;
        else
            ActiveSlot = -1;
    }

    /// <summary>Последний использованный слот (новая игра, загрузка, прогресс).</summary>
    public static int GetLastUsedSlot()
    {
        if (IsValidSlot(ActiveSlot) && HasSave(ActiveSlot))
            return ActiveSlot;

        int fromPrefs = PlayerPrefs.GetInt(LastUsedSlotKey, -1);
        if (IsValidSlot(fromPrefs) && HasSave(fromPrefs))
            return fromPrefs;

        fromPrefs = PlayerPrefs.GetInt(ActiveSlotKey, -1);
        if (IsValidSlot(fromPrefs) && HasSave(fromPrefs))
            return fromPrefs;

        for (int i = 1; i <= SlotCount; i++)
        {
            if (HasSave(i))
                return i;
        }

        return -1;
    }

    public static void AdvanceStage(SaveProgressStage stage)
    {
        if (!IsValidSlot(ActiveSlot) || stage == SaveProgressStage.None)
            return;

        WriteSlot(ActiveSlot, stage);
    }

    public static SaveProgressStage GetNextStageAfterScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return SaveProgressStage.None;

        if (sceneName == "EducationDemo")
            return SaveProgressStage.Level1;

        if (sceneName == "battle")
            return SaveProgressStage.Level2;

        if (sceneName == "Level-2")
            return SaveProgressStage.Level3;

        return SaveProgressStage.None;
    }

    public static string GetSceneForStage(SaveProgressStage stage)
    {
        switch (stage)
        {
            case SaveProgressStage.Tutorial:
                return "EducationDemo";
            case SaveProgressStage.Level1:
                return "battle";
            case SaveProgressStage.Level2:
                return "Level-2";
            case SaveProgressStage.Level3:
                return "Level-3";
            default:
                return null;
        }
    }

    public static bool ShouldPlayStoryIntro(SaveProgressStage stage, string sceneName = null)
    {
        if (stage == SaveProgressStage.Tutorial)
            return true;

        return !string.IsNullOrEmpty(sceneName)
            && sceneName == "EducationDemo";
    }

    public static string GetStageDisplayName(SaveProgressStage stage)
    {
        switch (stage)
        {
            case SaveProgressStage.Tutorial:
                return "Обучение";
            case SaveProgressStage.Level1:
                return "Уровень 1";
            case SaveProgressStage.Level2:
                return "Уровень 2";
            case SaveProgressStage.Level3:
                return "Уровень 3";
            default:
                return string.Empty;
        }
    }

    public static void DeleteAllSaves()
    {
        for (int i = 1; i <= SlotCount; i++)
            ClearSlot(i);

        ActiveSlot = -1;
        PlayerPrefs.DeleteKey(ActiveSlotKey);
        PlayerPrefs.DeleteKey(LastUsedSlotKey);
        PlayerPrefs.Save();
    }

    static void MarkSlotUsed(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return;

        ActiveSlot = slotIndex;
        PlayerPrefs.SetInt(ActiveSlotKey, slotIndex);
        PlayerPrefs.SetInt(LastUsedSlotKey, slotIndex);
    }

    static void WriteSlot(int slotIndex, SaveProgressStage stage)
    {
        PlayerPrefs.SetInt(HasSaveKeyPrefix + slotIndex, 1);
        PlayerPrefs.SetInt(StageKeyPrefix + slotIndex, (int)stage);
        MarkSlotUsed(slotIndex);
        PlayerPrefs.Save();
    }

    static void ClearSlot(int slotIndex)
    {
        PlayerPrefs.DeleteKey(HasSaveKeyPrefix + slotIndex);
        PlayerPrefs.DeleteKey(StageKeyPrefix + slotIndex);
    }

    static bool IsValidSlot(int slotIndex) => slotIndex >= 1 && slotIndex <= SlotCount;
}
