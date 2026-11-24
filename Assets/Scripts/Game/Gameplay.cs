using System;
using UnityEngine;

public class Gameplay : MonoBehaviour
{
    private static Gameplay _instance;
    private static int _levelCorrectAnswers;
    public static readonly string DefaultUserName = "Лев";
    private static string _userName;

    public static Action Started;
    public static Action Finished;
    public static Action AnswerStatisticsChanged;

    public static Transform Transform => _instance.transform;

    public static WireSystem WireSystem { get; private set; }
    public static TimerSystem TimerSystem { get; private set; }
    public static DialogSystem DialogSystem { get; private set; }
    public static QuizSystem QuizSystem { get; private set; }

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

    public static int CorrectAnswers { get; private set; }
    public static int WrongAnswers { get; private set; }

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
        if (isCorrect)
        {
            CorrectAnswers++;
            _levelCorrectAnswers++;
        }
        else
            WrongAnswers++;

        AnswerStatisticsChanged?.Invoke();
    }

    public static void ResetAnswerStatistics()
    {
        CorrectAnswers = 0;
        WrongAnswers = 0;
        _levelCorrectAnswers = 0;
        AnswerStatisticsChanged?.Invoke();
    }

    public static void Win()
    {
        Score += _levelCorrectAnswers;
        _levelCorrectAnswers = 0;
    }
}
