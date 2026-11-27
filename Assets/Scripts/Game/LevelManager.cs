using System;
using System.Linq;
using UnityEngine;

public static class LevelManager
{
    private static LevelsConfig _config;
    private static LevelConfig _currentConfig;
    private static int _remainingHints;

    public static event Action<int> HintsChanged;
    public static int RemainingHints => _remainingHints;

    private static LevelsConfig Config 
    {
        get 
        { 
            if (_config == null)
                _config = Resources.Load<LevelsConfig>("Levels/LevelsConfig"); 

            return _config;
        }
    }

    public static int LastId
    {
        get
        {
            return PlayerPrefs.GetInt("LastLevel", 0);
        }
        private set
        {
            PlayerPrefs.SetInt("LastLevel", value);
        }
    }

    public static bool IsNextExist => LastId < Config.Levels.Count;

    public static int LastDialogNodeId
    {
        get 
        {
            return PlayerPrefs.GetInt($"LastDialogNodeId{LastId}", -1);
        }
        set
        {
            PlayerPrefs.SetInt($"LastDialogNodeId{LastId}", value);
        }
    }

    private static string LevelBeginNodeKey => $"LevelBeginNodeId{LastId}";
    private static string LevelBeginDarknessKey => $"LevelBeginDarkness{LastId}";
    private static string LevelBeginLightKey => $"LevelBeginLight{LastId}";
    private static string StoryDarknessKey => $"StoryDarkness{LastId}";
    private static string StoryLightKey => $"StoryLight{LastId}";

    public static bool HasLevelBegin => PlayerPrefs.GetInt(LevelBeginNodeKey, -1) >= 0;
    public static int LevelBeginNodeId => PlayerPrefs.GetInt(LevelBeginNodeKey, -1);
    public static bool LevelBeginHasDarkness => PlayerPrefs.GetInt(LevelBeginDarknessKey, 0) != 0;
    public static bool LevelBeginHasLight => PlayerPrefs.GetInt(LevelBeginLightKey, 0) != 0;
    public static bool ShouldRestoreLevelBeginVisuals { get; private set; }

    public static float LastTimerDuration { get; private set; }

    public static bool IsTrainingLevel =>
        _currentConfig != null &&
        _currentConfig.LevelId != null &&
        _currentConfig.LevelId.StartsWith("0_", StringComparison.Ordinal);

    public static string CurrentLevelLabel
    {
        get
        {
            if (_currentConfig == null)
                throw new InvalidOperationException("A level must be loaded before its label can be displayed.");

            var separator = _currentConfig.LevelId?.IndexOf('_') ?? -1;
            if (separator <= 0 ||
                !int.TryParse(_currentConfig.LevelId.Substring(separator + 1), out var levelNumber))
            {
                throw new InvalidOperationException(
                    $"Level ID '{_currentConfig.LevelId}' must use the '<group>_<number>' format.");
            }

            var group = _currentConfig.LevelId.Substring(0, separator);
            if (group != "0" && group != "1")
                throw new InvalidOperationException($"Unknown level group '{group}' in ID '{_currentConfig.LevelId}'.");

            var groupCount = Config.Levels.Count(level =>
                level.LevelId.StartsWith(group + "_", StringComparison.Ordinal));
            if (groupCount == 0 || levelNumber < 1)
                throw new InvalidOperationException($"Invalid level number in ID '{_currentConfig.LevelId}'.");

            return group == "0"
                ? $"Обучающая схема {levelNumber}/{groupCount}"
                : $"Рабочая схема {levelNumber}/{groupCount}";
        }
    }

    public static void LoadLevel(int id, bool restartAtLevelBegin = false)
    {
        //ShouldRestoreLevelBeginVisuals = false;
        var previousId = LastId;
        if (previousId == id)
            Gameplay.ClearLevelAttemptQuizResults(id);

        LastId = id;
        if (Config.Levels.Count > id)
        {
            _currentConfig = Config.Levels[id];
            var hasRestartCheckpoint = restartAtLevelBegin && HasLevelBegin;
            if (previousId != id || !hasRestartCheckpoint)
                ClearDialogRestartState();

            ShouldRestoreLevelBeginVisuals = hasRestartCheckpoint;
            LastDialogNodeId = ShouldRestoreLevelBeginVisuals
                ? LevelBeginNodeId
                : _currentConfig.DialogNodeId;
            LastTimerDuration = _currentConfig.TimerDuration;
            _remainingHints = Mathf.Max(0, _currentConfig.HintCount);
        }
        else
        {
            // GameObject finish dialog
            LastDialogNodeId = 127;
            _remainingHints = 0;
        }

        HintsChanged?.Invoke(_remainingHints);
    }

    public static bool TryConsumeHint()
    {
        if (_remainingHints <= 0)
            return false;

        _remainingHints--;
        HintsChanged?.Invoke(_remainingHints);
        return true;
    }

    public static void RecordDialogNode(DialogNode node)
    {
        if (node == null)
            return;

        if (node.HasParameter(DialogNode.LevelBeginParameter))
        {
            if (LevelBeginNodeId == node.Id)
                return;

            PlayerPrefs.SetInt(LevelBeginNodeKey, node.Id);
            PlayerPrefs.SetInt(LevelBeginDarknessKey, PlayerPrefs.GetInt(StoryDarknessKey, 0));
            PlayerPrefs.SetInt(LevelBeginLightKey, PlayerPrefs.GetInt(StoryLightKey, 0));
            PlayerPrefs.Save();
        }
    }

    public static void RecordStoryAnimation(string parameter)
    {
        if (parameter == DialogNode.StartAnimation1Parameter)
        {
            PlayerPrefs.SetInt(StoryLightKey, 1);
        }
        else if (parameter == DialogNode.StartAnimation2Parameter)
        {
            PlayerPrefs.SetInt(StoryDarknessKey, 1);
            PlayerPrefs.SetInt(StoryLightKey, 0);
        }

        PlayerPrefs.Save();
    }

    public static bool ConsumeLevelBeginVisualRestore()
    {
        var shouldRestore = ShouldRestoreLevelBeginVisuals;
        ShouldRestoreLevelBeginVisuals = false;
        return shouldRestore;
    }

    private static void ClearDialogRestartState()
    {
        PlayerPrefs.DeleteKey(LevelBeginNodeKey);
        PlayerPrefs.DeleteKey(LevelBeginDarknessKey);
        PlayerPrefs.DeleteKey(LevelBeginLightKey);
        PlayerPrefs.DeleteKey(StoryDarknessKey);
        PlayerPrefs.DeleteKey(StoryLightKey);
    }

    public static bool SkipTraining()
    {
        var lastTrainingLevelIndex = -1;
        for (var index = 0; index < Config.Levels.Count; index++)
        {
            var levelId = Config.Levels[index].LevelId;
            if (levelId != null &&
                levelId.StartsWith("0_", StringComparison.Ordinal))
                lastTrainingLevelIndex = index;
        }

        if (lastTrainingLevelIndex < 0)
            return false;

        LoadLevel(lastTrainingLevelIndex);
        return true;
    }

    public static void UnloadLevel()
    {
        

        // unload \ wirecell \ метод очищает ячейки \ вызвать \ не забывать о таймере \ обнулить вайрсистем
    }

    public static void ShowLevel()
    {
        foreach (var cell in _currentConfig.WireCells)
        {
            var cellObj = Pool.Get<WireCell>(Gameplay.Transform);
            cellObj.Set(cell);
            Gameplay.Started += cellObj.Init;
        }
        Gameplay.Play();
    }

}
