using System;
using UnityEngine;

public class Gameplay : MonoBehaviour
{
    private static Gameplay _instance;
    private static int _levelCorrectAnswers;
    private static int _pendingQuizCount;
    public static readonly string DefaultUserName = "Лев";
    private static string _userName;

    public static Action Started;
    public static Action Finished;
    public static Action AnswerStatisticsChanged;
    public static Action<int> QuizRequested;
    public static Action AllQuizzesResolved;

    public static Transform Transform => _instance.transform;

    public static WireSystem WireSystem { get; private set; }
    public static TimerSystem TimerSystem { get; private set; }
    public static DialogSystem DialogSystem { get; private set; }
    public static QuizSystem QuizSystem { get; private set; }
    public static int PendingQuizCount => _pendingQuizCount;

    public static string UserName
    {
        get
        {
            if (string.IsNullOrEmpty(_userName))
                _userName = PlayerPrefs.GetString("UserName", DefaultUserName);

            return _userName;
        }
        set
        {
            if (_userName == value)
                return;

            _userName = value;
            PlayerPrefs.SetString("UserName", value);
        }
    }

    public static int CorrectAnswers
    {
        get => PlayerPrefs.GetInt("CorrectAnswers", 0);
        private set => PlayerPrefs.SetInt("CorrectAnswers", value);
    }

    public static int WrongAnswers
    {
        get => PlayerPrefs.GetInt("WrongAnswers", 0);
        private set => PlayerPrefs.SetInt("WrongAnswers", value);
    }

    public static int RunCorrectAnswers
    {
        get => PlayerPrefs.GetInt("RunCorrectAnswers", 0);
        private set => PlayerPrefs.SetInt("RunCorrectAnswers", value);
    }

    public static int RunWrongAnswers
    {
        get => PlayerPrefs.GetInt("RunWrongAnswers", 0);
        private set => PlayerPrefs.SetInt("RunWrongAnswers", value);
    }

    public static int RunLosses
    {
        get => PlayerPrefs.GetInt("RunLosses", 0);
        private set => PlayerPrefs.SetInt("RunLosses", value);
    }

    public static int Score
    {
        get => PlayerPrefs.GetInt("Score", 0);
        private set
        {
            PlayerPrefs.SetInt("Score", value);
        }
    }

    public static bool IsAllWin => Score >= DialogSystem.QuestionsCount / 2;

    void Awake()
    {
        if (!_instance)
        {
            _instance = this;

            WireSystem = new WireSystem();
            DialogSystem = new DialogSystem();
            QuizSystem = new QuizSystem();
        }
        else
            Destroy(gameObject);
    }

    void Start()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
    }

    public static void BeginRun()
    {
        RunCorrectAnswers = 0;
        RunWrongAnswers = 0;
        RunLosses = 0;
        PlayerPrefs.Save();
    }

    public static void Play()
    {
        _levelCorrectAnswers = 0;

        TimerSystem = new TimerSystem();

        _instance.gameObject.SetActive(true);
        TimerSystem.IsRunning = true;
        TimerSystem.Duration = LevelManager.LastTimerDuration;

        Started?.Invoke();
    }

    public static void Finish()
    {
        _levelCorrectAnswers = 0;
        _pendingQuizCount = 0;
        WireSystem.Reset();

        TimerSystem.IntervalElapsed = null;
        TimerSystem.TimerElapsed = null;
        TimerSystem.IsRunning = false;
        TimerSystem = null;

        Started = null;
        Finished?.Invoke();
    }

    public static void RecordAnswer(bool isCorrect)
    {
        var levelCorrectAnswersKey = $"LevelCorrectAnswers{LevelManager.LastId}";
        var levelWrongAnswersKey = $"LevelWrongAnswers{LevelManager.LastId}";
        if (isCorrect)
        {
            CorrectAnswers++;
            RunCorrectAnswers++;
            _levelCorrectAnswers++;
            PlayerPrefs.SetInt(
                levelCorrectAnswersKey,
                PlayerPrefs.GetInt(levelCorrectAnswersKey, 0) + 1);
        }
        else
        {
            WrongAnswers++;
            RunWrongAnswers++;
            PlayerPrefs.SetInt(
                levelWrongAnswersKey,
                PlayerPrefs.GetInt(levelWrongAnswersKey, 0) + 1);
        }

        PlayerPrefs.Save();
        AnswerStatisticsChanged?.Invoke();
    }

    public static void ClearLevelAttemptQuizResults(int levelId)
    {
        var levelCorrectAnswersKey = $"LevelCorrectAnswers{levelId}";
        var levelWrongAnswersKey = $"LevelWrongAnswers{levelId}";
        var levelCorrectAnswers = PlayerPrefs.GetInt(levelCorrectAnswersKey, 0);
        var levelWrongAnswers = PlayerPrefs.GetInt(levelWrongAnswersKey, 0);

        CorrectAnswers = Math.Max(0, CorrectAnswers - levelCorrectAnswers);
        WrongAnswers = Math.Max(0, WrongAnswers - levelWrongAnswers);
        RunCorrectAnswers = Math.Max(0, RunCorrectAnswers - levelCorrectAnswers);
        RunWrongAnswers = Math.Max(0, RunWrongAnswers - levelWrongAnswers);

        PlayerPrefs.DeleteKey(levelCorrectAnswersKey);
        PlayerPrefs.DeleteKey(levelWrongAnswersKey);
        PlayerPrefs.Save();
        AnswerStatisticsChanged?.Invoke();
    }

    public static void RecordLoss()
    {
        RunLosses++;
        PlayerPrefs.Save();
    }

    public static void RequestQuiz(int nodeId)
    {
        _pendingQuizCount++;
        QuizRequested?.Invoke(nodeId);
    }

    public static void ResolveQuiz()
    {
        if (_pendingQuizCount <= 0)
        {
            Debug.LogError("Cannot resolve a quiz when no quizzes are pending.");
            return;
        }

        _pendingQuizCount--;
        if (_pendingQuizCount == 0)
            AllQuizzesResolved?.Invoke();
    }

    public static void StartQuiz(int nodeId)
    {
        QuizSystem = new QuizSystem();
        QuizSystem.Start(nodeId);
    }

    public static void Win()
    {
        Score += _levelCorrectAnswers;
        _levelCorrectAnswers = 0;
    }
}
