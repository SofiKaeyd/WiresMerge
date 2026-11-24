using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class RestartPopup : BaseWindow
{
    [SerializeField] private BaseButton _nextButton;
    [SerializeField] private BaseButton _exitButton;
    [SerializeField] private Text _leaderboardText;

    public override async UniTask OnOpen()
    {
        Debug.Log("On Open RestartPopup");
        PlayerLeaderboard.RecordResult(
            Gameplay.UserName,
            Gameplay.RunCorrectAnswers,
            Gameplay.RunWrongAnswers,
            Gameplay.RunLosses);
        _leaderboardText.text = PlayerLeaderboard.GetTopFiveText();

        _nextButton.OnButtonClick += OnNextButton;
        _exitButton.OnButtonClick += OnExitButton;

        MouseManager.AddClickable(_nextButton);
        MouseManager.AddClickable(_exitButton);
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

    public static string GetTopFiveText()
    {
        var topPlayers = Load().Players
            .OrderByDescending(player => player.CorrectAnswers)
            .ThenBy(player => player.WrongAnswers)
            .ThenBy(player => player.Losses)
            .ThenBy(player => player.Nickname, StringComparer.OrdinalIgnoreCase)
            .Take(DisplayLimit)
            .ToList();

        var text = new StringBuilder();
        text.AppendLine("Топ 5 учеников Энзо");
        text.AppendLine("Ник — верно / неверно / поражения");

        for (var index = 0; index < topPlayers.Count; index++)
        {
            var player = topPlayers[index];
            text.Append(index + 1)
                .Append(". ")
                .Append(player.Nickname)
                .Append(" — ")
                .Append(player.CorrectAnswers)
                .Append(" / ")
                .Append(player.WrongAnswers)
                .Append(" / ")
                .AppendLine(player.Losses.ToString());
        }

        return text.ToString();
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

