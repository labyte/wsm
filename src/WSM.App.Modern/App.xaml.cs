using System;
using System.IO;
using System.Threading;
using System.Windows;
using WSM.App.Shared.Hosting;

namespace WSM.App.Modern;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Global\WSM.SingleInstance";
    private AppHost? _host;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单例保护：同一时间仅允许一个 WSM 实例运行
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        DispatcherUnhandledException += (_, args) =>
        {
            LogStartupFailure(args.Exception);
            args.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogStartupFailure(ex);
            }
        };

        try
        {
            _host = new AppHost();
            MainWindow = _host.CreateMainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            LogStartupFailure(ex);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Shutdown();
        if (_singleInstanceMutex != null)
        {
            if (_ownsSingleInstanceMutex)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // 忽略互斥锁释放异常
                }
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    private static void LogStartupFailure(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WSM",
                "startup-error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, ex.ToString());
        }
        catch
        {
            // 忽略日志写入失败
        }
    }
}
