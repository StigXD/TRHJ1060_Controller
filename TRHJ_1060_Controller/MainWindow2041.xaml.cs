using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace TRHJ_1060_Controller;
public partial class MainWindow2041 : Window, INotifyPropertyChanged
{
    private bool _isConnected = false;
    private ChipTelemetryData _telemetryData;
    private ChipManagment _chipManagment;
    private STM32Communicator _communicator;
    private SerialPortManager _serialPort;
    private ChipTypes _chipType;
    private EventLogger _logger;
    private int _chipId;
    private ButtonStateManager _buttonStateManager;

    const int _chipMode = 0;

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    public MainWindow2041(ChipTypes chipType, SerialPortManager serialPort)
    {
        InitializeComponent();
        _chipType = chipType;
        _serialPort = serialPort;
        _logger = new EventLogger();
        _telemetryData = new ChipTelemetryData(chipType);
        _buttonStateManager = new ButtonStateManager();
        DataContext = _telemetryData;
        InitializeChipManagment(chipType, serialPort);
        _communicator = new STM32Communicator(serialPort);

        // Привязка логера к текстовому полю
        TxtLog.Text = _logger.LogText;
        _logger.PropertyChanged += (s, e) => TxtLog.Text = _logger.LogText;
    }

    private void InitializeChipManagment(ChipTypes chipType, SerialPortManager serialPort)
    {
        _chipManagment = new ChipManagment(chipType, serialPort, _logger);
    }

    private void TxtRawCommand_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtRawCommand.Text == "Введите команду")
        {
            TxtRawCommand.Text = "";
            TxtRawCommand.Foreground = Brushes.Black;
        }
    }

    private void TxtRawCommand_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtRawCommand.Text))
        {
            TxtRawCommand.Text = "Введите команду";
            TxtRawCommand.Foreground = Brushes.Gray;
        }
    }

    private async void BtnSendRawCommand_Click(object sender, RoutedEventArgs e)
    {
        var commandText = TxtRawCommand.Text.Trim();

        if (string.IsNullOrEmpty(commandText) || commandText == "Введите команду")
        {
            _logger.LogError("Команда отсутствует. Введите команду");
            TxtRawCommand.Focus();
            return;
        }

        commandText = Regex.Replace(commandText, @"[^0-9A-Fa-f]", "");

        if (commandText.Length % 2 != 0)
        {
            _logger.LogError("HEX команда должна содержать четное количество символов");
            return;
        }

        try
        {
            TxtLastCommand.Text = commandText;
            await _chipManagment.SendRawCommandAsync(commandText);
            TxtRawCommand.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    private void ChipId_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChipId.SelectedIndex != -1)
        {
            _chipId = ChipId.SelectedIndex;
        }
    }

    private async void BtnInit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            byte chipId = (byte)ChipId.SelectedIndex;
            await _chipManagment.InitializeChipAsync(chipId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка инициализации: {ex.Message}");
        }
    }

    private async void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            byte chipId = (byte)ChipId.SelectedIndex;
            await _chipManagment.ResetChipAsync(chipId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сброса: {ex.Message}");
        }
    }

    private async void BtnTurnOn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn)
            {
                byte chipId = (byte)ChipId.SelectedIndex;

                // Проверяем текущее состояние кнопки
                bool[] enableChannels =
                [
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh0),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh1),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh2),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh3),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh4),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh5),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh6),
                    _buttonStateManager.IsButtonOn(BtnTurnOnCh7),
                ];

                bool isCurrentlyOn = enableChannels[GetChannelFromButton(btn.Name)];

                switch (btn.Name)
                {
                    case "BtnTurnOnCh0":
                    case "BtnTurnOnCh1":
                    case "BtnTurnOnCh2":
                    case "BtnTurnOnCh3":
                    case "BtnTurnOnCh4":
                    case "BtnTurnOnCh5":
                    case "BtnTurnOnCh6":
                    case "BtnTurnOnCh7":
                        await HandleChannelToggle(chipId, _chipMode, GetChannelFromButton(btn.Name), btn, enableChannels);
                        break;
                    case "BtnTurnOnAllCh":
                        await HandleAllChannelsToggle(chipId, btn, isCurrentlyOn);
                        break;
                    case "BtnTurnOnAllChipId":
                        await HandleAllChipsToggle(btn, isCurrentlyOn);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка управления: {ex.Message}");
        }
    }

    private async Task HandleChannelToggle(byte chipId, int mode, int channel, Button btn, bool[] enableChannels)
    {
        if (enableChannels[GetChannelFromButton(btn.Name)])
        {
            // Включаем канал
            await _chipManagment.EnableChannelAsync(chipId, mode, channel, enableChannels);
            _logger.LogInfo($"Канал {channel} включен");
            _buttonStateManager.SetButtonState(btn, true);
        }
        else
        {
            // Выключаем канал
            await _chipManagment.DisableChannelAsync(chipId, channel, enableChannels);
            _logger.LogInfo($"Канал {channel} выключен");
            _buttonStateManager.SetButtonState(btn, false);
        }
    }

    private async Task HandleAllChannelsToggle(byte chipId, Button btn, bool isCurrentlyOn)
    {
        if (!isCurrentlyOn)
        {
            // Включаем все каналы
            await _chipManagment.SwitchOnAllChannelsAsync(chipId);
            _logger.LogInfo($"Все каналы ChipId={chipId} включены");
            _buttonStateManager.SetButtonState(btn, true);
        }
        else
        {
            // Выключаем все каналы
            await _chipManagment.SwitchOffAllChannelsAsync(chipId);
            _logger.LogInfo($"Все каналы ChipId={chipId} выключены");
            _buttonStateManager.SetButtonState(btn, false);
        }
    }

    private async Task HandleAllChipsToggle(Button btn, bool isCurrentlyOn)
    {
        if (!isCurrentlyOn)
        {
            // Включаем все чипы
            await _chipManagment.SwitchOnAllChipsAsync();
            _logger.LogInfo("Все чипы включены");
            _buttonStateManager.SetButtonState(btn, true);
        }
        else
        {
            // Выключаем все чипы
            await _chipManagment.SwitchOffAllChipsAsync();
            _logger.LogInfo("Все чипы выключены");
            _buttonStateManager.SetButtonState(btn, false);
        }
    }

    private int GetChannelFromButton(string buttonName)
    {
        return buttonName switch
        {
            "BtnTurnOnCh0" => 0,
            "BtnTurnOnCh1" => 1,
            "BtnTurnOnCh2" => 2,
            "BtnTurnOnCh3" => 3,
            "BtnTurnOnCh4" => 4,
            "BtnTurnOnCh5" => 5,
            "BtnTurnOnCh6" => 6,
            "BtnTurnOnCh7" => 7,
            _ => 0
        };
    }

    //private async Task HandleChannelEnable(byte chipId, string mode, int channel, Button btn)
    //{
    //    await _chipManagment.EnableChannelAsync(chipId, mode, (byte)channel);

    //    btn.Background = new SolidColorBrush(Colors.LimeGreen);
    //    btn.Content = "Выключить";
    //}

    private async void BtnSetParams_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn)
            {
                byte chipId = (byte)ChipId.SelectedIndex;

                byte channel = btn.Name switch
                {
                    "BtnSetParamsCh0" => 0,
                    "BtnSetParamsCh1" => 1,
                    "BtnSetParamsCh2" => 2,
                    "BtnSetParamsCh3" => 3,
                    "BtnSetParamsCh4" => 4,
                    "BtnSetParamsCh5" => 5,
                    "BtnSetParamsCh6" => 6,
                    "BtnSetParamsCh7" => 7,
                    _ => 0
                };

                double att = GetAttenuationForChannel(channel);
                double phase = GetPhaseForChannel(channel);

                await _chipManagment.SetParametersAsync(chipId, _chipMode, channel, att, phase);
                _logger.LogInfo($"Параметры установлены: Ch{channel}, Att={att}dB, Phase={phase}°");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка установки параметров: {ex.Message}");
        }
    }

    private async void BtnSetParamsAllCh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            byte chipId = (byte)ChipId.SelectedIndex;

            double att = (AttAllChanel.Value ?? 0);
            double phase = (PhaseAllChanel.Value ?? 0);

            await _chipManagment.SetAllChannelsAsync(chipId, _chipMode, att, phase);
            _logger.LogInfo($"Все каналы ChipId={chipId} обновлены: Att={att}dB, Phase={phase}°");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка установки параметров: {ex.Message}");
        }
    }

    private async void BtnSetParamsAllChipId_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double att = (AttAllChanelAllId.Value ?? 0);
            double phase = (PhaseAllChanelAllId.Value ?? 0);

            await _chipManagment.SetAllChipsAsync(att, phase, _chipMode);
            _logger.LogInfo($"Все чипы обновлены: Att={att}dB, Phase={phase}°");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка установки параметров для всех чипов: {ex.Message}");
        }
    }

    private async void BtnGetTelemetry_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInfo("Запрос телеметрии...");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка чтения телеметрии: {ex.Message}");
        }
    }

    private async void BtnSaveLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"TRHJ2041_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                await _logger.SaveToFileAsync(dialog.FileName);
                MessageBox.Show("Журнал успешно сохранен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения журнала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private double GetAttenuationForChannel(int channel)
    {
        return channel switch
        {
            0 => AttChanel0.Value ?? 0,
            1 => AttChanel1.Value ?? 0,
            2 => AttChanel2.Value ?? 0,
            3 => AttChanel3.Value ?? 0,
            4 => AttChanel4.Value ?? 0,
            5 => AttChanel5.Value ?? 0,
            6 => AttChanel6.Value ?? 0,
            7 => AttChanel7.Value ?? 0,
            _ => 0
        };
    }

    private double GetPhaseForChannel(int channel)
    {
        return channel switch
        {
            0 => PhaseChanel0.Value ?? 0,
            1 => PhaseChanel1.Value ?? 0,
            2 => PhaseChanel2.Value ?? 0,
            3 => PhaseChanel3.Value ?? 0,
            4 => PhaseChanel4.Value ?? 0,
            5 => PhaseChanel5.Value ?? 0,
            6 => PhaseChanel6.Value ?? 0,
            7 => PhaseChanel7.Value ?? 0,
            _ => 0
        };
    }
}
