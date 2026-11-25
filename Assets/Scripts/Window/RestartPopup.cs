using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class RestartPopup : BaseWindow
{
    [SerializeField] private BaseButton _nextButton;
    [SerializeField] private BaseButton _exitButton;
    [SerializeField] private RectTransform _leaderboardRoot;
    [SerializeField] private Font _leaderboardFont;

    public override async UniTask OnOpen()
    {
        Debug.Log("On Open RestartPopup");
        PlayerLeaderboard.RecordResult(
            Gameplay.UserName,
            Gameplay.RunCorrectAnswers,
            Gameplay.RunWrongAnswers,
            Gameplay.RunLosses);
        BuildLeaderboardTable();

        _nextButton.OnButtonClick += OnNextButton;
        _exitButton.OnButtonClick += OnExitButton;

        MouseManager.AddClickable(_nextButton);
        MouseManager.AddClickable(_exitButton);
    }

    private void BuildLeaderboardTable()
    {
        for (var index = _leaderboardRoot.childCount - 1; index >= 0; index--)
            Destroy(_leaderboardRoot.GetChild(index).gameObject);

        var background = _leaderboardRoot.GetComponent<Image>();
        if (background == null)
            background = _leaderboardRoot.gameObject.AddComponent<Image>();
        background.color = new Color32(63, 43, 31, 235);
        background.raycastTarget = false;

        var layout = _leaderboardRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = _leaderboardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateTitle();
        CreateRow(new[] { "№", "Ник", "Верно", "Ошибки", "Поражения" }, true, 0);

        var players = PlayerLeaderboard.GetTopFive();
        for (var index = 0; index < 5; index++)
        {
            if (index < players.Count)
            {
                var player = players[index];
                CreateRow(new[]
                {
                    (index + 1).ToString(),
                    player.Nickname,
                    player.CorrectAnswers.ToString(),
                    player.WrongAnswers.ToString(),
                    player.Losses.ToString()
                }, false, index);
            }
            else
            {
                CreateRow(new[] { (index + 1).ToString(), "—", "—", "—", "—" }, false, index);
            }
        }
    }

    private void CreateTitle()
    {
        var title = CreateText("Топ 5 учеников Энзо", _leaderboardRoot, 30, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.color = new Color32(255, 218, 151, 255);

        var element = title.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 48;
    }

    private void CreateRow(string[] values, bool isHeader, int rowIndex)
    {
        var rowObject = new GameObject(
            isHeader ? "TableHeader" : $"PlayerRow{rowIndex + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        rowObject.transform.SetParent(_leaderboardRoot, false);

        var rowImage = rowObject.GetComponent<Image>();
        rowImage.color = isHeader
            ? new Color32(143, 93, 52, 255)
            : rowIndex % 2 == 0
                ? new Color32(93, 67, 49, 255)
                : new Color32(77, 55, 42, 255);
        rowImage.raycastTarget = false;

        var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(12, 12, 4, 4);
        rowLayout.spacing = 8;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        var rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.preferredHeight = isHeader ? 48 : 52;

        for (var column = 0; column < values.Length; column++)
        {
            var cell = CreateText(values[column], rowObject.transform, isHeader ? 20 : 21,
                column == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            cell.fontStyle = isHeader ? FontStyle.Bold : FontStyle.Normal;

            var cellElement = cell.gameObject.AddComponent<LayoutElement>();
            if (column == 0)
                cellElement.preferredWidth = 42;
            else
                cellElement.flexibleWidth = column == 1 ? 4 : 1;

            if (!isHeader && column == 2)
                cell.color = new Color32(167, 235, 159, 255);
            else if (!isHeader && (column == 3 || column == 4))
                cell.color = new Color32(255, 174, 155, 255);
            else
                cell.color = new Color32(255, 239, 218, 255);

            if (!isHeader && column == 0 && rowIndex == 0)
                cell.color = new Color32(255, 218, 151, 255);
        }
    }

    private Text CreateText(string value, Transform parent, int fontSize, TextAnchor alignment)
    {
        var textObject = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<Text>();
        text.font = _leaderboardFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private void OnNextButton(BaseButton button)
    {
        PlayerLeaderboard.ResetProgressPreservingLeaderboard();
        OnExitButton(null);
    }

    private void OnExitButton(BaseButton button)
    {
        SceneHandler.LoadScene(SceneHandler.MainScene);
        WindowManager.Open<MenuWindow>();
    }

    public override async UniTask OnClose()
    {
        _nextButton.OnButtonClick -= OnNextButton;
        _exitButton.OnButtonClick -= OnExitButton;

        MouseManager.RemoveClickable(_nextButton);
        MouseManager.RemoveClickable(_exitButton);
    }

}

[Serializable]
public sealed class PlayerResult
{
    public string Nickname;
    public int CorrectAnswers;
    public int WrongAnswers;
    public int Losses;
}

public static class PlayerLeaderboard
{
    private const string StorageKey = "PlayerLeaderboard";
    private const int DisplayLimit = 5;

    [Serializable]
    private sealed class LeaderboardData
    {
        public List<PlayerResult> Players = new List<PlayerResult>();
    }

    public static void RecordResult(string nickname, int correctAnswers, int wrongAnswers, int losses)
    {
        var normalizedNickname = nickname?.Trim();
        if (string.IsNullOrEmpty(normalizedNickname))
            throw new ArgumentException("A player nickname is required.", nameof(nickname));

        var data = Load();
        var existingResult = data.Players.FirstOrDefault(player =>
            string.Equals(player.Nickname, normalizedNickname, StringComparison.OrdinalIgnoreCase));

        if (existingResult == null)
        {
            existingResult = new PlayerResult();
            data.Players.Add(existingResult);
        }

        existingResult.Nickname = normalizedNickname;
        existingResult.CorrectAnswers = correctAnswers;
        existingResult.WrongAnswers = wrongAnswers;
        existingResult.Losses = losses;

        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static List<PlayerResult> GetTopFive()
    {
        return Load().Players
            .OrderByDescending(player => player.CorrectAnswers)
            .ThenBy(player => player.WrongAnswers)
            .ThenBy(player => player.Losses)
            .ThenBy(player => player.Nickname, StringComparer.OrdinalIgnoreCase)
            .Take(DisplayLimit)
            .ToList();
    }

    public static void ResetProgressPreservingLeaderboard()
    {
        var leaderboard = PlayerPrefs.GetString(StorageKey, string.Empty);
        PlayerPrefs.DeleteAll();

        if (!string.IsNullOrEmpty(leaderboard))
            PlayerPrefs.SetString(StorageKey, leaderboard);

        PlayerPrefs.Save();
    }

    private static LeaderboardData Load()
    {
        if (!PlayerPrefs.HasKey(StorageKey))
            return new LeaderboardData();

        var data = JsonUtility.FromJson<LeaderboardData>(PlayerPrefs.GetString(StorageKey));
        if (data == null || data.Players == null)
            throw new InvalidOperationException("The saved player leaderboard is invalid.");

        return data;
    }
}

