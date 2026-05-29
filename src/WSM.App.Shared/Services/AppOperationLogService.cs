using System;
using System.Collections.ObjectModel;
using WSM.Core.Interfaces;
using WSM.Core.Models;

namespace WSM.App.Shared.Services;

/// <summary>
/// WSM 应用操作日志服务。
/// </summary>
public sealed class AppOperationLogService : IOperationLogSink
{
    private const int MaxEntries = 1000;

    public ObservableCollection<OperationLogEntry> Entries { get; } = new();

    public event EventHandler? EntriesChanged;

    public void Log(OperationLogLevel level, string category, string message)
    {
        var entry = new OperationLogEntry
        {
            TimestampLocal = DateTime.Now,
            Level = level,
            Category = category,
            Message = message
        };

        RunOnUi(() =>
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }

            EntriesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Clear()
    {
        RunOnUi(() =>
        {
            Entries.Clear();
            EntriesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
