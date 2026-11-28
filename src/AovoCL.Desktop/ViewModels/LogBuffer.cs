using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;

namespace AovoCL.Desktop.ViewModels;

/// <summary>日志缓冲：后台线程写入，120ms 合并刷新一次，避免高频刷新卡顿。</summary>
public sealed class LogBuffer : ObservableObject
{
    private const int MaxChars = 200_000;

    private const string EventStart = "<log4j:Event";
    private const string EventEnd = "</log4j:Event>";

    private static readonly Regex EventRegex = new(
        @"<log4j:Event\s[^>]*level=""(?<level>[^""]*)""[^>]*>(?<body>.*?)</log4j:Event>",
        RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MessageRegex = new(
        @"<log4j:Message><!\[CDATA\[(?<msg>.*?)\]\]></log4j:Message>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly StringBuilder _builder = new();
    private readonly StringBuilder _pending = new();
    private readonly DispatcherTimer _timer;
    private bool _dirty;
    private string _text = "";
    private bool _isVisible = true;

    public LogBuffer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += (_, _) => Flush();
    }

    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>写入日志片段；Minecraft 的 log4j XML 会被转成 [LEVEL] 消息 的一行文本。</summary>
    public void Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        lock (_builder)
        {
            var text = Format(chunk);
            if (text.Length == 0) return;

            _builder.AppendLine(text);
            if (_builder.Length > MaxChars) _builder.Remove(0, _builder.Length - MaxChars + 50_000);
            _dirty = true;
        }

        StartTimer();
    }

    /// <summary>把 log4j 事件流拆成可读文本；不完整的事件留到下一次拼接。</summary>
    private string Format(string chunk)
    {
        _pending.Append(chunk);
        var raw = _pending.ToString();
        var output = new StringBuilder(raw.Length);

        var pos = 0;
        while (pos < raw.Length)
        {
            var eventStart = raw.IndexOf(EventStart, pos, StringComparison.Ordinal);
            var plainLength = (eventStart < 0 ? raw.Length : eventStart) - pos;

            if (plainLength > 0)
            {
                foreach (var line in raw.Substring(pos, plainLength).Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (trimmed.Length > 0) output.AppendLine(trimmed);
                }
            }

            if (eventStart < 0)
            {
                pos = raw.Length;
                break;
            }

            var eventEnd = raw.IndexOf(EventEnd, eventStart, StringComparison.Ordinal);
            if (eventEnd < 0)
            {
                pos = eventStart;
                break;
            }

            eventEnd += EventEnd.Length;
            var block = raw.Substring(eventStart, eventEnd - eventStart);
            var match = EventRegex.Match(block);
            var message = match.Success ? MessageRegex.Match(match.Groups["body"].Value) : Match.Empty;
            if (message.Success) output.AppendLine($"[{match.Groups["level"].Value}] {message.Groups["msg"].Value}");

            pos = eventEnd;
        }

        var rest = raw.Substring(pos);
        _pending.Clear();
        if (rest.Length > 0) _pending.Append(rest);
        if (_pending.Length > 100_000)
        {
            _pending.Clear();
            output.AppendLine("[日志] 已跳过无法解析的输出");
        }

        return output.ToString().TrimEnd('\r', '\n');
    }

    public void Clear()
    {
        lock (_builder)
        {
            _builder.Clear();
            _pending.Clear();
            _dirty = true;
        }

        Flush();
    }

    private void StartTimer()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            if (!_timer.IsEnabled) _timer.Start();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_timer.IsEnabled) _timer.Start();
            });
        }
    }

    private void Flush()
    {
        string snapshot;
        lock (_builder)
        {
            if (!_dirty)
            {
                _timer.Stop();
                return;
            }

            _dirty = false;
            snapshot = _builder.ToString();
        }

        Text = snapshot;
    }
}
