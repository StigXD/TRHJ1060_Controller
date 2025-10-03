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

		// Добавляем обработчики для кнопок INIT и RESET
		SubscribeToButtons("BtnInit", BtnTurnOn_Click);
		SubscribeToButtons("BtnReset", BtnTurnOn_Click);

		TxtRawCommand.Text = "Введите HEX команду";
		TxtRawCommand.Foreground = Brushes.Gray;
		TxtRawCommand.PreviewTextInput += TxtRawCommand_PreviewTextInput;
		TxtRawCommand.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnPasting));
	}

	// Вспомогательный метод для подписки на кнопки по паттерну имени
	private void SubscribeToButtons(string namePattern, RoutedEventHandler handler)
	{
		var buttons = FindVisualChildren<Button>(this).Where(b => b.Name.Contains(namePattern));
		foreach (var button in buttons)
		{
			// Инициализируем состояние кнопок включения
			if (namePattern.Contains("TurnOn"))
			{
				button.Tag = false; // Изначально выключено
				if (namePattern.Contains("AllChipId"))
					button.Content = "SWITCH ON ALL CHIP";
				else if (namePattern.Contains("AllCh"))
					button.Content = "SWITCH ON";
				else
					button.Content = "Включить";

				button.Background = new SolidColorBrush(Colors.Gray);
			}

			button.Click += handler;
		}
	}
	// Вспомогательный метод для поиска элементов в визуальном дереве
	public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
	{
		if (depObj != null)
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
				if (child != null && child is T)
				{
					yield return (T) child;
				}

				foreach (T childOfChild in FindVisualChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}
	}

	private void InitializeControls()
	{
		// Инициализация всех контролов для каждого чипа и канала
		InitializeChipControls();

		// Контекстное меню для RAW команд
		InitializeContextMenu();

		// Мониторинг
		BtnStartMonitoring.Click += async (s, e) => await StartMonitoringAsync();
		BtnStopMonitoring.Click += (s, e) => StopMonitoring();
	}

	private void InitializeChipControls()
	{
		// Инициализация для всех ChipID (0-3) и всех каналов (0-3)
		for (var channel = 0; channel < 4; channel++)
			InitializeChannelControls(chipId, channel);

		// Инициализация кнопок для всех чипов
		InitializeAllChipsControls();
	}

	private void InitializeChannelControls(int chipId, int channel)
	{
		// Установка начальных значений для контролов канала
		var attControl = FindName($"AttChanel{channel}Id{chipId}") as Xceed.Wpf.Toolkit.DoubleUpDown;
		var phaseControl = FindName($"PhaseChanel{channel}Id{chipId}") as Xceed.Wpf.Toolkit.DoubleUpDown;
		var modeControl = FindName($"CmbModeCh{channel}Id{chipId}") as ComboBox;

		if (attControl != null) attControl.Value = 0;
		if (phaseControl != null) phaseControl.Value = 0;
		if (modeControl != null) modeControl.SelectedIndex = 0;

		// Инициализация кнопок включения/выключения
		var turnOnButton = FindName($"BtnTurnOnCh{channel}Id{chipId}") as Button;
		if (turnOnButton != null)
		{
			turnOnButton.Content = "Включить";
			turnOnButton.Background = new SolidColorBrush(Colors.Gray);
			turnOnButton.Tag = false; // false = выключено, true = включено
		}
	}

	private void InitializeAllChannelsControls(int chipId)
	{
		// Установка начальных значений для "Все каналы"
		var attAllControl = FindName($"AttAllChanelId{chipId}") as Xceed.Wpf.Toolkit.DoubleUpDown;
		var phaseAllControl = FindName($"PhaseAllChanelId{chipId}") as Xceed.Wpf.Toolkit.DoubleUpDown;
		var modeAllControl = FindName($"CmbModeAllChId{chipId}") as ComboBox;

		if (attAllControl != null) attAllControl.Value = 0;
		if (phaseAllControl != null) phaseAllControl.Value = 0;
		if (modeAllControl != null) modeAllControl.SelectedIndex = 0;

		// Кнопка включения всех каналов
		var turnOnAllButton = FindName($"BtnTurnOnAllChId{chipId}") as Button;
		if (turnOnAllButton != null)
		{
			turnOnAllButton.Content = "SWITCH ON";
			turnOnAllButton.Background = new SolidColorBrush(Colors.Gray);
			turnOnAllButton.Tag = false;
		}
	}

	private void InitializeAllChipsControls()
	{
		// Установка начальных значений для "Все ChipID"
		if (AttAllChanelAllId != null) AttAllChanelAllId.Value = 0;
		if (PhaseAllChanelAllId != null) PhaseAllChanelAllId.Value = 0;
		if (CmbModeAllChAllId != null) CmbModeAllChAllId.SelectedIndex = 0;

		// Кнопка включения всех чипов
		if (BtnTurnOnAllChipId != null)
		{
			BtnTurnOnAllChipId.Content = "SWITCH ON ALL CHIP ID";
			BtnTurnOnAllChipId.Background = new SolidColorBrush(Colors.Gray);
			BtnTurnOnAllChipId.Tag = false;
		}
	}

	private void InitializeContextMenu()
	{
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
		Dispatcher.Invoke(() => { LogMessage($"Ошибка: {error}"); });
	}

	private void TxtRawCommand_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		var regex = new Regex("[^0-9A-Fa-f]");
		e.Handled = regex.IsMatch(e.Text);
	}

	private void OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		if (e.DataObject.GetDataPresent(typeof(string)))
		{
			var text = (string) e.DataObject.GetData(typeof(string));
			var regex = new Regex("[^0-9A-Fa-f]");
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

	// Обработчик для установки параметров отдельного канала
	private async void BtnSetParams_Click(object sender, RoutedEventArgs e)
	{
		if (!_isConnected)
		{
			MessageBox.Show("Сначала подключитесь к устройству!");
			return;
		}

		try
		{
			var button = sender as Button;
			var buttonName = button.Name;

			// Парсим ChipID и Channel из имени кнопки
			var match = Regex.Match(buttonName, @"BtnSetParamsCh(\d+)Id(\d+)");
			if (match.Success)
			{
				var channel = byte.Parse(match.Groups[1].Value);
				var chipId = byte.Parse(match.Groups[2].Value);

				// Получаем значения из соответствующих контролов
				var amplitude = GetAmplitudeValue(chipId, channel);
				var phase = GetPhaseValue(chipId, channel);
				var isTx = GetModeValue(chipId, channel);

				var commandBytes = TRHJ1060_CommandBuilder.SetAmplitudePhase(chipId, channel, isTx, amplitude, phase);
				var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");

				var response = await _communicator.SendCommandAsync(commandHex);

				if (response != null && response.Contains("TX_OK"))
				{
					LogMessage($"Параметры установлены для Chip{chipId} Ch{channel}");
					UpdateCurrentValues(chipId, channel, amplitude, phase, isTx);
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка установки параметров: {ex.Message}");
			LogMessage($"Ошибка установки параметров: {ex.Message}");
		}
	}

	// Обработчик для включения/выключения каналов
	private async void BtnTurnOn_Click(object sender, RoutedEventArgs e)
	{
		if (!_isConnected)
		{
			MessageBox.Show("Сначала подключитесь к устройству!");
			return;
		}

		try
		{
			var button = sender as Button;
			var buttonName = button.Name;
			var isCurrentlyOn = button.Tag as bool? ?? false;
			var enable = !isCurrentlyOn; // Инвертируем текущее состояние

			if (buttonName.Contains("TurnOnAllChAllId"))
			{
				// Включить/выключить все каналы всех чипов
				for (byte chipId = 0; chipId < 4; chipId++)
				{
					var commandBytes = TRHJ1060_CommandBuilder.EnableAllChannels(chipId, enable);
					var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");
					var response = await _communicator.SendCommandAsync(commandHex);

					if (response != null && response.Contains("TX_OK"))
					{
						// Обновляем все кнопки включения для этого чипа
						UpdateAllTurnOnButtonsState(chipId, enable);
					}
				}

				// Обновляем главную кнопку
				UpdateButtonState(button, enable);
				LogMessage(enable ? "Все каналы всех чипов ВКЛЮЧЕНЫ" : "Все каналы всех чипов ВЫКЛЮЧЕНЫ");
			}
			else if (buttonName.Contains("TurnOnAllChId"))
			{
				// Включить/выключить все каналы конкретного чипа
				var chipId = byte.Parse(buttonName.Substring(buttonName.Length - 1));
				var commandBytes = TRHJ1060_CommandBuilder.EnableAllChannels(chipId, enable);
				var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");

				var response = await _communicator.SendCommandAsync(commandHex);
				if (response != null && response.Contains("TX_OK"))
				{
					UpdateAllTurnOnButtonsState(chipId, enable);
					UpdateButtonState(button, enable);
					LogMessage(enable ? $"Все каналы Chip{chipId} ВКЛЮЧЕНЫ" : $"Все каналы Chip{chipId} ВЫКЛЮЧЕНЫ");
				}
			}
			else if (buttonName.Contains("TurnOnCh"))
			{
				// Включить/выключить конкретный канал
				var match = Regex.Match(buttonName, @"BtnTurnOnCh(\d+)Id(\d+)");
				if (match.Success)
				{
					var channel = byte.Parse(match.Groups[1].Value);
					var chipId = byte.Parse(match.Groups[2].Value);

					var commandBytes = TRHJ1060_CommandBuilder.EnableChannel(chipId, channel, enable);
					var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");

					var response = await _communicator.SendCommandAsync(commandHex);
					if (response != null && response.Contains("TX_OK"))
					{
						UpdateButtonState(button, enable);
						LogMessage(enable ? $"Канал {channel} Chip{chipId} ВКЛЮЧЕН" : $"Канал {channel} Chip{chipId} ВЫКЛЮЧЕН");
					}
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка управления каналом: {ex.Message}");
			LogMessage($"Ошибка управления каналом: {ex.Message}");
		}
	}

	private void UpdateButtonState(Button button, bool isOn)
	{
		button.Dispatcher.Invoke(() =>
		{
			if (button.Name.Contains("AllChAllId") || button.Name.Contains("AllChId"))
			{
				// Для кнопок "все каналы"
				button.Content = isOn ? "SWITCH OFF" : "SWITCH ON";
			}
			else
			{
				// Для отдельных каналов
				button.Content = isOn ? "Выключить" : "Включить";
			}

			button.Background = isOn ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Gray);

			button.Tag = isOn; // Сохраняем состояние
		});
	}

	// Метод для обновления всех кнопок включения для чипа
	private void UpdateAllTurnOnButtonsState(byte chipId, bool isOn)
	{
		// Обновляем кнопки отдельных каналов
		for (byte channel = 0; channel < 4; channel++)
		{
			var buttonName = $"BtnTurnOnCh{channel}Id{chipId}";
			var button = FindName(buttonName) as Button;
			if (button != null)
			{
				UpdateButtonState(button, isOn);
			}
		}

		// Обновляем кнопку "все каналы" для этого чипа
		var allChannelsButton = FindName($"BtnTurnOnAllChId{chipId}") as Button;
		if (allChannelsButton != null)
		{
			UpdateButtonState(allChannelsButton, isOn);
		}
	}

	// Обработчик для установки параметров всех каналов
	private async void BtnSetParamsAllCh_Click(object sender, RoutedEventArgs e)
	{
		if (!_isConnected)
		{
			MessageBox.Show("Сначала подключийтесь к устройству!");
			return;
		}

		try
		{
			var button = sender as Button;
			var buttonName = button.Name;

			if (buttonName.Contains("SetParamsAllChAllId"))
			{
				// Установить параметры для всех каналов всех чипов
				var amplitude = (double) AttAllChanelAllId.Value;
				var phase = (double) PhaseAllChanelAllId.Value;
				var isTx = CmbModeAllChAllId.SelectedIndex == 0;

				for (byte chipId = 0; chipId < 4; chipId++)
				{
					for (byte channel = 0; channel < 4; channel++)
					{
						var commandBytes = TRHJ1060_CommandBuilder.SetAmplitudePhase(chipId, channel, isTx, amplitude, phase);
						var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");
						await _communicator.SendCommandAsync(commandHex);
						await Task.Delay(10); // Небольшая задержка между командами
					}
				}

				LogMessage($"Параметры установлены для всех каналов всех чипов: Атт={amplitude}dB, Фаза={phase}°");
			}
			else if (buttonName.Contains("SetParamsAllChId"))
			{
				// Установить параметры для всех каналов конкретного чипа
				var chipId = byte.Parse(buttonName.Substring(buttonName.Length - 1));
				var amplitude = GetAmplitudeAllValue(chipId);
				var phase = GetPhaseAllValue(chipId);
				var isTx = GetModeAllValue(chipId);

				for (byte channel = 0; channel < 4; channel++)
				{
					var commandBytes = TRHJ1060_CommandBuilder.SetAmplitudePhase(chipId, channel, isTx, amplitude, phase);
					var commandHex = BitConverter.ToString(commandBytes).Replace("-", "");
					await _communicator.SendCommandAsync(commandHex);
					await Task.Delay(10);
				}

				LogMessage($"Параметры установлены для всех каналов Chip{chipId}: Атт={amplitude}dB, Фаза={phase}°");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка установки параметров: {ex.Message}");
			LogMessage($"Ошибка установки параметров: {ex.Message}");
		}
	}

	// Вспомогательные методы для получения значений из контролов
	private double GetAmplitudeValue(byte chipId, byte channel)
	{
		var controlName = $"AttChanel{channel}Id{chipId}";
		var control = FindName(controlName) as Xceed.Wpf.Toolkit.DoubleUpDown;
		return control?.Value ?? 0;
	}

	private double GetPhaseValue(byte chipId, byte channel)
	{
		var controlName = $"PhaseChanel{channel}Id{chipId}";
		var control = FindName(controlName) as Xceed.Wpf.Toolkit.DoubleUpDown;
		return control?.Value ?? 0;
	}

	private bool GetModeValue(byte chipId, byte channel)
	{
		var controlName = $"CmbModeCh{channel}Id{chipId}";
		var control = FindName(controlName) as ComboBox;
		return control?.SelectedIndex == 0; // 0 = TX, 1 = RX
	}

	private double GetAmplitudeAllValue(byte chipId)
	{
		var controlName = $"AttAllChanelId{chipId}";
		var control = FindName(controlName) as Xceed.Wpf.Toolkit.DoubleUpDown;
		return control?.Value ?? 0;
	}

	private double GetPhaseAllValue(byte chipId)
	{
		var controlName = $"PhaseAllChanelId{chipId}";
		var control = FindName(controlName) as Xceed.Wpf.Toolkit.DoubleUpDown;
		return control?.Value ?? 0;
	}

	private bool GetModeAllValue(byte chipId)
	{
		var controlName = $"CmbModeAllChId{chipId}";
		var control = FindName(controlName) as ComboBox;
		return control?.SelectedIndex == 0;
	}

	// Метод для обновления текущих значений в UI
	private void UpdateCurrentValues(byte chipId, byte channel, double amplitude, double phase, bool isTx)
	{
		var stackPanelName = $"CurrentValuesCh{channel}Id{chipId}";
		var stackPanel = FindName(stackPanelName) as StackPanel;

		if (stackPanel != null && stackPanel.Children.Count >= 5)
		{
			(stackPanel.Children[1] as TextBlock).Text = $"{amplitude:F1}";
			(stackPanel.Children[3] as TextBlock).Text = $"{phase:F1}";
			(stackPanel.Children[4] as TextBlock).Text = isTx ? "TX" : "RX";
		}
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
		catch (OperationCanceledException)
		{
		}
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