using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

public class EventLogger : INotifyPropertyChanged
{
    private StringBuilder _logBuilder;
    private string _logText = "";

    public event PropertyChangedEventHandler PropertyChanged;

    public string LogText
    {
        get => _logText;
        private set
        {
            if (_logText != value)
            {
                _logText = value;
                OnPropertyChanged();
            }
        }
    }

    public EventLogger()
    {
        _logBuilder = new StringBuilder();
    }

    public void LogCommand(string command)
    {
        var message = $"[{DateTime.Now:HH:mm:ss.fff}] → КОМАНДА: {command}";
        AddLog(message);
    }

    public void LogResponse(string response)
    {
        var message = $"[{DateTime.Now:HH:mm:ss.fff}] ← ОТВЕТ: {response}";
        AddLog(message);
    }

    public void LogError(string error)
    {
        var message = $"[{DateTime.Now:HH:mm:ss.fff}] ⚠ ОШИБКА: {error}";
        AddLog(message);
    }

    public void LogInfo(string info)
    {
        var message = $"[{DateTime.Now:HH:mm:ss.fff}] ℹ {info}";
        AddLog(message);
    }

    private void AddLog(string message)
    {
        _logBuilder.AppendLine(message);
        LogText = _logBuilder.ToString();
    }

    public void Clear()
    {
        _logBuilder.Clear();
        LogText = "";
    }

    public async Task SaveToFileAsync(string filePath)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, LogText, Encoding.UTF8);
            LogInfo($"Журнал сохранен в файл: {filePath}");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при сохранении журнала: {ex.Message}");
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
