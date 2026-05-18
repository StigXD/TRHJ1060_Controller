using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace TRHJ_1060_Controller;

public class ButtonStateManager
{
    private readonly Dictionary<string, bool> _buttonStates = new();
    private readonly SolidColorBrush _greenBrush = new SolidColorBrush(Colors.LimeGreen);
    private readonly SolidColorBrush _defaultBrush;

    public ButtonStateManager()
    {
        // Используем серый цвет как цвет по умолчанию
        _defaultBrush = new SolidColorBrush(Colors.LightGray);
    }

    public void SetButtonState(Button button, bool isOn)
    {
        if (button == null) return;

        string buttonKey = button.Name;

        if (isOn)
        {
            _buttonStates[buttonKey] = true;
            button.Background = _greenBrush;
            button.Content = GetOffText(button.Name);
        }
        else
        {
            _buttonStates[buttonKey] = false;
            button.Background = _defaultBrush;
            button.Content = GetOnText(button.Name);
        }
    }

    public bool IsButtonOn(Button button)
    {
        if (button == null) return false;
        return _buttonStates.TryGetValue(button.Name, out var state) && state;
    }

    private string GetOnText(string buttonName)
    {
        return buttonName switch
        {
            "BtnTurnOnCh0" or "BtnTurnOnCh1" or "BtnTurnOnCh2" or "BtnTurnOnCh3" => "Включить",
            "BtnTurnOnAllCh" => "SWITCH ON",
            "BtnTurnOnAllChipId" => "SWITCH ON ALL CHIP",
            _ => "Включить"
        };
    }

    private string GetOffText(string buttonName)
    {
        return buttonName switch
        {
            "BtnTurnOnCh0" or "BtnTurnOnCh1" or "BtnTurnOnCh2" or "BtnTurnOnCh3" => "Выключить",
            "BtnTurnOnAllCh" => "SWITCH OFF",
            "BtnTurnOnAllChipId" => "SWITCH OFF ALL CHIP",
            _ => "Выключить"
        };
    }

    public void ResetAllButtons(params Button[] buttons)
    {
        foreach (var btn in buttons)
        {
            if (btn != null)
            {
                SetButtonState(btn, false);
            }
        }
    }
}
