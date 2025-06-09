using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RJCP.IO.Ports;
using TRHJ1060_Controller.Domain.Models;
using TRHJ1060_Controller.Domain.Services;

namespace TRHJ1060_Controller;

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

		txtRawCommand.PreviewTextInput += TxtRawCommand_PreviewTextInput;
		txtRawCommand.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnPasting));
    }

	private void TxtRawCommand_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		// Разрешаем только HEX символы: 0-9, A-F, a-f
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

		var contextMenu = new ContextMenu();

		var menuItemReset = new MenuItem();
		menuItemReset.Header = "Сброс (RST)";
		menuItemReset.Click += (s, e) => txtRawCommand.Text = "000001"; // Пример команды сброса
		contextMenu.Items.Add(menuItemReset);

		var menuItemTemp = new MenuItem();
		menuItemTemp.Header = "Чтение температуры";
		menuItemTemp.Click += (s, e) => txtRawCommand.Text = "A1B2C3"; // Пример команды чтения температуры
		contextMenu.Items.Add(menuItemTemp);

		txtRawCommand.ContextMenu = contextMenu;
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
			int amplitude = (int) (sliderAmplitude.Value / 0.5); // Конвертация в 0.5 dB шаги
			int phase = (int) (sliderPhase.Value / 5.625);       // Конвертация в 5.625° шаги

			var command = TRHJ1060_CommandBuilder.SetAmplitudePhase(
				chipId,
				channel,
				isTx,
				amplitude,
				phase);

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

	private void BtnSendRawCommand_Click(object sender, RoutedEventArgs e)
	{
		if (!_isConnected)
		{
			MessageBox.Show("Сначала подключитесь к устройству!");
			return;
		}

		try
		{
			var commandText = txtRawCommand.Text.Trim();

			// Проверка на плейсхолдер
			if (commandText == "Введите HEX команду...")
			{
				MessageBox.Show("Введите команду в HEX формате");
				return;
			}

			// Удаление пробелов и недопустимых символов
			commandText = Regex.Replace(commandText, @"[^0-9A-Fa-f]", "");

			if (string.IsNullOrEmpty(commandText))
			{
				MessageBox.Show("Введите команду в HEX формате");
				return;
			}

            // Проверка на четное количество символов (каждый байт - 2 символа)
            if (commandText.Length % 2 != 0)
			{
				MessageBox.Show("Некорректная длина команды. Должно быть четное количество символов");
				return;
			}

			// Преобразование строки в массив байт
			var command = new byte[commandText.Length / 2];
			for (int i = 0; i < command.Length; i++)
			{
				var byteValue = commandText.Substring(i * 2, 2);
				command[i] = Convert.ToByte(byteValue, 16);
			}

			// Проверка длины команды (должна быть 3 байта)
			if (command.Length != 3)
			{
				MessageBox.Show("Команда должна состоять из 3 байт (6 HEX символов)");
				return;
			}

			SendCommand(command);
			LogMessage($"Отправлена RAW команда: {BitConverter.ToString(command)}");
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка отправки команды: {ex.Message}");
			LogMessage($"Ошибка отправки RAW команды: {ex.Message}");
		}
	}
	private void SendCommand(byte[] command)
	{
		if (_serialPort != null && _serialPort.IsOpen)
		{
			try
			{
				// Очистка буфера перед отправкой
				_serialPort.DiscardInBuffer();

				// Отправка команды
				_serialPort.Write(command, 0, command.Length);
				txtLastCommand.Text = BytesToHexString(command);

				// Чтение ответа с таймаутом
				DateTime start = DateTime.Now;
				while (_serialPort.BytesToRead < 3 && (DateTime.Now - start).TotalMilliseconds < 500)
				{
					Thread.Sleep(10);
				}

				if (_serialPort.BytesToRead >= 3)
				{
					byte[] response = new byte[3];
					int bytesRead = _serialPort.Read(response, 0, 3);
					txtResponse.Text = $"Ответ: {BytesToHexString(response)}";
					LogMessage($"Получен ответ: {BytesToHexString(response)}");
				}
				else
				{
					txtResponse.Text = "Ответ не получен (таймаут)";
					LogMessage("Ответ не получен (таймаут)");
				}
			}
			catch (TimeoutException)
			{
				txtResponse.Text = "Таймаут при чтении ответа";
				LogMessage("Таймаут при чтении ответа");
			}
			catch (Exception ex)
			{
				txtResponse.Text = $"Ошибка: {ex.Message}";
				LogMessage($"Ошибка при обработке ответа: {ex.Message}");
			}
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
	private string BytesToHexString(byte[] bytes)
	{
		return BitConverter.ToString(bytes).Replace("-", "");
	}
}