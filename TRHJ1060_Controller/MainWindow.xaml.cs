using System.Windows;
using System.Windows.Controls;
using RJCP.IO.Ports;
using TRHJ1060_Controller.Domain.Models;
using TRHJ1060_Controller.Domain.Services;

namespace TRHJ1060_Controller
{
    public partial class MainWindow : Window
    {
        private SerialPortManager _serialPortManager;
        private SerialPortStream _serialPort;
        private bool _isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeControls();
            InitializeSerialPortManager();
        }

        private void InitializeControls()
        {
            // Инициализация слайдеров
            sliderAmplitude.ValueChanged += (s, e) =>
                txtAmplitudeValue.Text = $"{sliderAmplitude.Value:F1} dB";

            sliderPhase.ValueChanged += (s, e) =>
                txtPhaseValue.Text = $"{sliderPhase.Value:F1}°";

            // Установка значений по умолчанию
            cmbChipId.SelectedIndex = 0;
            cmbChannel.SelectedIndex = 0;
            cmbMode.SelectedIndex = 0;
            sliderAmplitude.Value = 0;
            sliderPhase.Value = 0;
        }

        private void InitializeSerialPortManager()
        {
            _serialPortManager = new SerialPortManager();
            RefreshPortsList();
        }

        private void RefreshPortsList()
        {
            cmbPorts.Items.Clear();
            foreach (var port in _serialPortManager.GetAvailablePorts())
            {
                cmbPorts.Items.Add(port);
            }
            if (cmbPorts.Items.Count > 0) cmbPorts.SelectedIndex = 0;
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortsList();
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPorts.SelectedItem == null)
            {
                MessageBox.Show("Выберите COM порт!");
                return;
            }

            try
            {
                string portName = cmbPorts.SelectedItem.ToString();
                _serialPort = new SerialPortStream(portName)
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _serialPort.Open();
                _isConnected = true;

                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                txtConnectionStatus.Text = $"Статус: Подключено к {portName}";
                LogMessage($"Подключено к {portName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
                LogMessage($"Ошибка подключения: {ex.Message}");
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _isConnected = false;

                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
                txtConnectionStatus.Text = "Статус: Отключено";
                LogMessage("Отключено от COM порта");
            }
        }

        private void BtnSetAmplitudePhase_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Сначала подключитесь к устройству!");
                return;
            }

            try
            {
                byte chipId = byte.Parse((cmbChipId.SelectedItem as ComboBoxItem)?.Content.ToString());
                byte channel = byte.Parse((cmbChannel.SelectedItem as ComboBoxItem)?.Content.ToString());
                bool isTx = cmbMode.SelectedIndex == 0;
                int amplitude = (int)(sliderAmplitude.Value / 0.5); // Конвертация в 0.5 dB шаги
                int phase = (int)(sliderPhase.Value / 5.625);       // Конвертация в 5.625° шаги

                var command = TRHJ1060_CommandBuilder.SetAmplitudePhase(
                    chipId, channel, isTx, amplitude, phase);

                SendCommand(command);
                LogMessage("Команда установки амплитуды/фазы отправлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
                LogMessage($"Ошибка команды: {ex.Message}");
            }
        }

        private void BtnEnableChannel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Сначала подключитесь к устройству!");
                return;
            }

            try
            {
                byte chipId = byte.Parse((cmbChipId.SelectedItem as ComboBoxItem)?.Content.ToString());

                var command = TRHJ1060_CommandBuilder.EnableChannel0(chipId);
                SendCommand(command);
                LogMessage("Команда включения канала отправлена");
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
                byte chipId = byte.Parse((cmbChipId.SelectedItem as ComboBoxItem)?.Content.ToString());

                // Здесь будет реализация чтения температуры
                // float temperature = await ReadTemperatureAsync(chipId);
                // txtResponse.Text = $"Температура: {temperature:F1}°C";

                LogMessage("Запрос температуры отправлен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
                LogMessage($"Ошибка чтения температуры: {ex.Message}");
            }
        }

        private void SendCommand(byte[] command)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Write(command, 0, command.Length);
                txtLastCommand.Text = BitConverter.ToString(command);
            }
        }

        private void LogMessage(string message)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            txtLog.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            base.OnClosed(e);
        }
    }
}