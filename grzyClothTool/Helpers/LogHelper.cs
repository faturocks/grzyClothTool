using grzyClothTool.Views;
using System;
using System.IO;

namespace grzyClothTool.Helpers;

public class LogMessageEventArgs : EventArgs
{
    public string TypeIcon { get; set; }
    public string Message { get; set; }
}

public static class LogHelper
{
    private static LogWindow _logWindow;
    private static string _logFilePath;
    public static event EventHandler<LogMessageEventArgs> LogMessageCreated;

    public static void Init()
    {
        _logWindow = new LogWindow();
        
        // Initialize file logging
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string logDirectory = Path.Combine(documentsPath, "grzyClothTool", "Logs");
        Directory.CreateDirectory(logDirectory);
        
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = Path.Combine(logDirectory, $"grzyClothTool_{timestamp}.log");
        
        // Write initial log entry
        WriteToFile($"[{DateTime.Now:HH:mm:ss}] [INFO] Log file initialized: {_logFilePath}");
    }

    public static void Log(string message, LogType logtype = LogType.Info)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var type = GetLogTypeIcon(logtype);
        var logTypeString = logtype.ToString().ToUpper();

        // Write to UI if available
        if (_logWindow != null)
        {
            _logWindow.Dispatcher.Invoke(() =>
            {
                _logWindow.LogMessages.Add(new LogMessage { TypeIcon = type, Message = message, Timestamp = timestamp });
                LogMessageCreated?.Invoke(_logWindow, new LogMessageEventArgs { TypeIcon = type, Message = message });
            });
        }

        // Always write to file
        WriteToFile($"[{timestamp}] [{logTypeString}] {message}");
    }

    private static void WriteToFile(string logEntry)
    {
        try
        {
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
        }
        catch
        {
            // Silently fail if we can't write to log file
        }
    }

    public static string GetLogTypeIcon(LogType type)
    {
        return type switch
        {
            LogType.Info => "Check",
            LogType.Warning => "WarningOutline",
            LogType.Error => "Close",
            _ => "Info"
        };
    }

    public static void OpenLogWindow()
    {
        _logWindow.Show();
    }

    public static void OpenLogFile()
    {
        try
        {
            if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _logFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Silently fail
        }
    }

    public static void Close()
    {
        WriteToFile($"[{DateTime.Now:HH:mm:ss}] [INFO] Application closing, log file saved.");
        
        if (_logWindow != null)
        {
            _logWindow.Closing -= _logWindow.LogWindow_Closing;
            _logWindow.Close();
            _logWindow = null;
        }
    }
}
