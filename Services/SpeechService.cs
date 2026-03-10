using System.Speech.Recognition;
using System.Text.RegularExpressions;

namespace MoodyClone.Services;

public class SpeechService : IDisposable
{
    private SpeechRecognitionEngine? _engine;
    private string[] _words = Array.Empty<string>();
    private int _currentWordIndex;
    private DateTime _lastRecognitionTime;
    private int _wordsRecognizedCount;
    private double _speakingPace; // words per second

    public event Action<int>? WordRecognized;
    public event Action? SpeechPaused;

    public int CurrentWordIndex => _currentWordIndex;
    public double SpeakingPace => _speakingPace;
    public string[] Words => _words;
    public bool IsRunning { get; private set; }

    private System.Timers.Timer? _pauseTimer;

    public void LoadScript(string text)
    {
        _words = Regex.Split(text, @"\s+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => Regex.Replace(w, @"[^\w']", "").ToLowerInvariant())
            .Where(w => w.Length > 0)
            .ToArray();

        _currentWordIndex = 0;
        _wordsRecognizedCount = 0;
        _speakingPace = 0;
    }

    public void Start()
    {
        if (_words.Length == 0) return;

        Stop();

        try
        {
            _engine = new SpeechRecognitionEngine(
                SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault()?.Id
                ?? throw new InvalidOperationException("No speech recognizer installed"));

            // Use dictation grammar for free-form speech
            _engine.LoadGrammar(new DictationGrammar());

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.SpeechHypothesized += OnSpeechHypothesized;

            _engine.SetInputToDefaultAudioDevice();
            _engine.RecognizeAsync(RecognizeMode.Multiple);

            _lastRecognitionTime = DateTime.Now;
            IsRunning = true;

            // Pause detection timer
            _pauseTimer = new System.Timers.Timer(1000);
            _pauseTimer.Elapsed += (_, _) =>
            {
                if ((DateTime.Now - _lastRecognitionTime).TotalSeconds > 1.5)
                {
                    SpeechPaused?.Invoke();
                }
            };
            _pauseTimer.Start();
        }
        catch
        {
            IsRunning = false;
        }
    }

    public void Stop()
    {
        _pauseTimer?.Stop();
        _pauseTimer?.Dispose();
        _pauseTimer = null;

        if (_engine != null)
        {
            try { _engine.RecognizeAsyncCancel(); } catch { }
            _engine.SpeechRecognized -= OnSpeechRecognized;
            _engine.SpeechHypothesized -= OnSpeechHypothesized;
            _engine.Dispose();
            _engine = null;
        }
        IsRunning = false;
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        ProcessRecognizedText(e.Result.Text);
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result.Confidence >= 0.3f)
        {
            ProcessRecognizedText(e.Result.Text);
        }
    }

    private void ProcessRecognizedText(string text)
    {
        var spokenWords = Regex.Split(text, @"\s+")
            .Select(w => Regex.Replace(w, @"[^\w']", "").ToLowerInvariant())
            .Where(w => w.Length > 0)
            .ToArray();

        if (spokenWords.Length == 0) return;

        // Find best matching position in the script
        int bestIndex = _currentWordIndex;
        int bestMatchCount = 0;

        // Search within a window around the current position
        int searchStart = Math.Max(0, _currentWordIndex - 5);
        int searchEnd = Math.Min(_words.Length - 1, _currentWordIndex + 50);

        for (int i = searchStart; i <= searchEnd; i++)
        {
            int matchCount = 0;
            for (int j = 0; j < spokenWords.Length && (i + j) < _words.Length; j++)
            {
                if (_words[i + j].Contains(spokenWords[j]) || spokenWords[j].Contains(_words[i + j]))
                    matchCount++;
            }

            if (matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                bestIndex = i;
            }
        }

        if (bestMatchCount > 0)
        {
            _currentWordIndex = bestIndex + spokenWords.Length;

            // Calculate pace
            var elapsed = (DateTime.Now - _lastRecognitionTime).TotalSeconds;
            if (elapsed > 0.1)
            {
                _wordsRecognizedCount += spokenWords.Length;
                double instantPace = spokenWords.Length / elapsed;
                // Exponential moving average
                _speakingPace = _speakingPace == 0 ? instantPace : (_speakingPace * 0.7 + instantPace * 0.3);
            }

            _lastRecognitionTime = DateTime.Now;
            WordRecognized?.Invoke(_currentWordIndex);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
