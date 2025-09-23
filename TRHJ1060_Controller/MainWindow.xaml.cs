using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TRHJ1060_Controller.Domain.Models;
using TRHJ1060_Controller.Domain.Services;

namespace TRHJ1060_Controller;

public partial class MainWindow : Window
{
    private SerialPortManager _serialPortManager;
    private STM32Communicator _communicator;
    private bool _isConnected = false;
    private CancellationTokenSource _monitoringCts;

    public MainWindow()
    {
        InitializeComponent();
        InitializeControls();
        InitializeSerialPortManager();
        InitializeCommunicator();

        TxtRawCommand.Text = "Введите HEX команду";
        TxtRawCommand.Foreground = Brushes.Gray;
        TxtRawCommand.PreviewTextInput += TxtRawCommand_PreviewTextInput;
        TxtRawCommand.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnPasting));
    }

    private void InitializeCommunicator()
    {
        _communicator = new STM32Communicator();
        _communicator.MessageReceived += Communicator_MessageReceived;
        _communicator.ErrorOccurred += Communicator_ErrorOccurred;
    }

    private void Communicator_MessageReceived(object sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtResponse.Text = message;
            LogMessage($"Получено: {message}");
        });
    }

    private void Communicator_ErrorOccurred(object sender, string error)
    {
        Dispatcher.Invoke(() =>
        {
            LogMessage($"Ошибка: {error}");
        });
    }

    private void TxtRawCommand_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9A-Fa-f]");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            Regex regex = new Regex("[^0-9A-Fa-f]");
            if (regex.IsMatch(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void InitializeControls()
    {
        SliderAmplitude.ValueChanged += (s, e) =>
            TxtAmplitudeValue.Text = $"{SliderAmplitude.Value:F1} dB";

        SliderPhase.ValueChanged += (s, e) =>
            TxtPhaseValue.Text = $"{SliderPhase.Value:F1}°";

        CmbChipId.SelectedIndex = 0;
        CmbChannel.SelectedIndex = 0;
        CmbMode.SelectedIndex = 0;
        SliderAmplitude.Value = 0;
        SliderPhase.Value = 0;

        var contextMenu = new ContextMenu();
        var menuItemReset = new MenuItem { Header = "Сброс (RST)" };
        menuItemReset.Click += (s, e) =>
        {
            TxtRawCommand.Text = "000000";
            TxtRawCommand.Foreground = Brushes.Black;
        };
        contextMenu.Items.Add(menuItemReset);

        var menuItemTemp = new MenuItem { Header = "Чтение температуры" };
        menuItemTemp.Click += (s, e) =>
        {
            TxtRawCommand.Text = "020000";
            TxtRawCommand.Foreground = Brushes.Black;
        };
        contextMenu.Items.Add(menuItemTemp);

        TxtRawCommand.ContextMenu = contextMenu;

        BtnStartMonitoring.Click += async (s, e) => await StartMonitoringAsync();
        BtnStopMonitoring.Click += (s, e) => StopMonitoring();
    }

    private async Task StartMonitoringAsync()
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        _monitoringCts = new CancellationTokenSource();
        BtnStartMonitoring.IsEnabled = false;
        BtnStopMonitoring.IsEnabled = true;

        try
        {
            while (!_monitoringCts.Token.IsCancellationRequested)
            {
                await ReadTemperatureAsync();
                await Task.Delay(1000, _monitoringCts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LogMessage($"Ошибка мониторинга: {ex.Message}");
        }
        finally
        {
            BtnStartMonitoring.IsEnabled = true;
            BtnStopMonitoring.IsEnabled = false;
        }
    }

    private void StopMonitoring()
    {
        _monitoringCts?.Cancel();
    }

    private void InitializeSerialPortManager()
    {
        _serialPortManager = new SerialPortManager();
        RefreshPortsList();
    }

    private async Task ReadTemperatureAsync()
    {
        try
        {
            var response = await _communicator.SendCommandAsync("020000");
            if (response != null && response.StartsWith("TEMP:"))
            {
                var tempStr = response.Substring(5);
                if (float.TryParse(tempStr, out float temperature))
                {
                    TxtResponse.Text = $"Температура: {temperature:F1}°C";
                    LogMessage($"Температура: {temperature:F1}°C");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Ошибка чтения температуры: {ex.Message}");
        }
    }

    private void RefreshPortsList()
    {
        CmbPorts.Items.Clear();
        foreach (var port in _serialPortManager.GetAvailablePorts())
        {
            CmbPorts.Items.Add(port);
        }
        if (CmbPorts.Items.Count > 0) CmbPorts.SelectedIndex = 0;
    }

    private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
    {
        RefreshPortsList();
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (CmbPorts.SelectedItem == null)
        {
            MessageBox.Show("Выберите COM порт!");
            return;
        }

        try
        {
            var portName = CmbPorts.SelectedItem.ToString();
            var success = await _communicator.ConnectAsync(portName);

            if (success)
            {
                _isConnected = true;
                BtnConnect.IsEnabled = false;
                BtnDisconnect.IsEnabled = true;
                TxtConnectionStatus.Text = $"Статус: Подключено к {portName}";
                LogMessage($"Подключено к {portName}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка подключения: {ex.Message}");
            LogMessage($"Ошибка подключения: {ex.Message}");
        }
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _communicator.Disconnect();
        _isConnected = false;
        BtnConnect.IsEnabled = true;
        BtnDisconnect.IsEnabled = false;
        TxtConnectionStatus.Text = "Статус: Отключено";
        LogMessage("Отключено от COM порта");
    }

    private async void BtnResetChip_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var response = await _communicator.SendCommandAsync("000000");
            if (response != null && response.Contains("RESET_OK"))
            {
                LogMessage("Чип успешно сброшен");
                TxtResponse.Text = "RESET: OK";
            }
            else
            {
                LogMessage("Ошибка сброса чипа");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сброса: {ex.Message}");
            LogMessage($"Ошибка сброса: {ex.Message}");
        }
    }

    private async void BtnInitRegisters_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var response = await _communicator.SendCommandAsync("FFFFFF");
            if (response != null && response.Contains("INIT_OK"))
            {
                LogMessage("Контрольные регистры инициализированы");
                TxtResponse.Text = "INIT: OK";
            }
            else
            {
                LogMessage("Ошибка инициализации регистров");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка инициализации: {ex.Message}");
            LogMessage($"Ошибка инициализации: {ex.Message}");
        }
    }

    private async void BtnSetAmplitudePhase_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var chipId = byte.Parse((CmbChipId.SelectedItem as ComboBoxItem)?.Content.ToString());
            var channel = byte.Parse((CmbChannel.SelectedItem as ComboBoxItem)?.Content.ToString());
            var isTx = CmbMode.SelectedIndex == 0;
            var amplitude = (double)SliderAmplitude.Value;
            var phase = (double)SliderPhase.Value;

            var commandBytes = TRHJ1060_CommandBuilder.SetAmplitudePhase(chipId, channel, isTx, amplitude, phase);
            var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");

            var response = await _communicator.SendCommandAsync(commandHex);
            LogMessage("Команда установки амплитуды/фазы отправлена");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
            LogMessage($"Ошибка команды: {ex.Message}");
        }
    }

    // ВОССТАНОВЛЕННЫЙ МЕТОД BtnEnableChannel_Click
    private async void BtnEnableChannel_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var chipId = byte.Parse((CmbChipId.SelectedItem as ComboBoxItem)?.Content.ToString());
            var channel = byte.Parse((CmbChannel.SelectedItem as ComboBoxItem)?.Content.ToString());

            // Используем билдер команд для генерации команды включения канала
            var commandBytes = TRHJ1060_CommandBuilder.EnableChannel(chipId, channel, true);
            var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");

            var response = await _communicator.SendCommandAsync(commandHex);

            if (response != null && response.Contains("TX_OK"))
            {
                LogMessage($"Канал {channel} чипа {chipId} включен");
                TxtResponse.Text = $"CH{channel}: ENABLED";
            }
            else
            {
                LogMessage($"Ошибка включения канала {channel}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
            LogMessage($"Ошибка команды: {ex.Message}");
        }
    }

    // Метод для отключения канала (если нужен)
    private async void BtnDisableChannel_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var chipId = byte.Parse((CmbChipId.SelectedItem as ComboBoxItem)?.Content.ToString());
            var channel = byte.Parse((CmbChannel.SelectedItem as ComboBoxItem)?.Content.ToString());

            var commandBytes = TRHJ1060_CommandBuilder.EnableChannel(chipId, channel, false);
            var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");

            var response = await _communicator.SendCommandAsync(commandHex);

            if (response != null && response.Contains("TX_OK"))
            {
                LogMessage($"Канал {channel} чипа {chipId} отключен");
                TxtResponse.Text = $"CH{channel}: DISABLED";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
            LogMessage($"Ошибка команды: {ex.Message}");
        }
    }

    private async void BtnReadTemperature_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var response = await _communicator.SendCommandAsync("020000");
            if (response != null && response.StartsWith("TEMP:"))
            {
                var tempStr = response.Substring(5);
                if (float.TryParse(tempStr, out float temperature))
                {
                    TxtResponse.Text = $"Температура: {temperature:F1}°C";
                    LogMessage($"Температура: {temperature:F1}°C");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Ошибка чтения температуры: {ex.Message}");
        }
    }

    private void TxtRawCommand_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtRawCommand.Text == "Введите HEX команду")
        {
            TxtRawCommand.Text = "";
            TxtRawCommand.Foreground = Brushes.Black;
        }
    }

    private void TxtRawCommand_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtRawCommand.Text))
        {
            TxtRawCommand.Text = "Введите HEX команду";
            TxtRawCommand.Foreground = Brushes.Gray;
        }
    }

    private async void BtnSendRawCommand_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show("Сначала подключитесь к устройству!");
            return;
        }

        try
        {
            var commandText = TxtRawCommand.Text.Trim();

            if (commandText == "Введите HEX команду" || string.IsNullOrEmpty(commandText))
            {
                MessageBox.Show("Введите команду в HEX формате");
                TxtRawCommand.Focus();
                return;
            }

            commandText = Regex.Replace(commandText, @"[^0-9A-Fa-f]", "");

            if (string.IsNullOrEmpty(commandText) || commandText.Length % 2 != 0)
            {
                MessageBox.Show("Некорректная команда. Должно быть четное количество HEX символов");
                TxtRawCommand.Focus();
                return;
            }

            var response = await _communicator.SendCommandAsync(commandText);
            LogMessage($"Отправлена команда: {commandText}, Ответ: {response}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка отправки команды: {ex.Message}");
            LogMessage($"Ошибка отправки команды: {ex.Message}");
        }
    }

    private void LogMessage(string message)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        TxtLog.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitoringCts?.Cancel();
        _communicator?.Dispose();
        base.OnClosed(e);
    }
}