using RJCP.IO.Ports;
using System;
using System.IO.Ports;
using System.Windows;

namespace TRHJ_1060_Controller
{
    public partial class DeviceSelectionWindow : Window
    {
        private SerialPortManager _serialPort = new SerialPortManager();
        private ChipTypes _selectedDeviceType;
        private bool _isConnected = false;

        public DeviceSelectionWindow()
        {
            InitializeComponent();
            RefreshPorts();
            DeviceType_Checked(this, null);
        }

        private void RefreshPorts()
        {
            var ports = _serialPort.GetAvailablePorts();

            CmbPorts.ItemsSource = ports;

            if (ports.Count > 0)
                CmbPorts.SelectedIndex = 0;
            else
                CmbPorts.SelectedIndex = -1;
        }

        private void DeviceType_Checked(object sender, RoutedEventArgs e)
        {
            CmbChipType.ItemsSource = Enum.GetValues(typeof(ChipTypes));

            if (CmbChipType.Items.Count > 0)
                CmbChipType.SelectedIndex = 0;

            if (CmbChipType.SelectedItem != null)
                _selectedDeviceType = (ChipTypes)CmbChipType.SelectedItem;
        }

        private void CmbChipType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbChipType.SelectedItem != null)
            {
                _selectedDeviceType = (ChipTypes)CmbChipType.SelectedItem;
                UpdateContinueButton();
            }
        }

        private void UpdateContinueButton()
        {
            if (_isConnected && CmbChipType.SelectedItem != null) 
                BtnContinue.IsEnabled = true;

            if (!_isConnected || CmbChipType.SelectedItem == null)
                BtnContinue.IsEnabled = false;
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
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

                if (!string.IsNullOrEmpty(portName))
                {
                    var success = await _serialPort.CreateSerialPort(portName);
                    if (success)
                    {
                        _isConnected = true;
                        BtnConnect.IsEnabled = false;
                        BtnDisconnect.IsEnabled = true;
                        UpdateContinueButton();
                        TxtConnectionStatus.Text = $"Статус: Подключено к {portName}";
                    }
                    else
                    {
                        MessageBox.Show($"Не удалось подключиться к {portName}");
                        TxtConnectionStatus.Text = $"Не удалось подключиться к {portName}";
                    }
                }
            }
            catch (Exception ex)
            {
                TxtConnectionStatus.Text = ex.Message;
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serialPort?.Dispose();
                _isConnected = false;
                TxtConnectionStatus.Text = "Статус: Не подключено";
                BtnConnect.IsEnabled = true;
                BtnDisconnect.IsEnabled = false;
                UpdateContinueButton();
            }
            catch (Exception ex)
            {
                TxtConnectionStatus.Text = $"Ошибка отключения: {ex.Message}";
            }
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            Window mainWindow = null;

            switch (_selectedDeviceType)
            {
                case ChipTypes.TRHJ_1060:
                    mainWindow = new MainWindow1060(_selectedDeviceType, _serialPort);
                    break;

                case ChipTypes.TRHJ_2011:
                    mainWindow = new MainWindow2011(_selectedDeviceType, _serialPort);
                    break;

                case ChipTypes.TRHJ_2041:
                    mainWindow = new MainWindow2041(_selectedDeviceType, _serialPort);
                    break;
            }

            if (mainWindow != null)
            {
                mainWindow.Show();
                Close(); // Закрываем окно выбора
            }
        }
    }
}