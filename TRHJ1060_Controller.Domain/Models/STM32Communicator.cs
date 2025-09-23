using RJCP.IO.Ports;
using System.Text;

namespace TRHJ1060_Controller
{
    public class STM32Communicator : IDisposable
    {
        private SerialPortStream _serialPort;
        private bool _isConnected = false;

        public bool IsConnected => _isConnected && _serialPort?.IsOpen == true;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ErrorOccurred;

        public STM32Communicator()
        {
        }

        public async Task<bool> ConnectAsync(string portName, int baudRate = 9600)
        {
            try
            {
                _serialPort = new SerialPortStream(portName, baudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    NewLine = "\n"
                };

                _serialPort.Open();
                _isConnected = true;

                // Запускаем асинхронное чтение
                _ = Task.Run(ReadDataAsync);

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _isConnected = false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка отключения: {ex.Message}");
            }
        }

        public async Task<string> SendCommandAsync(string command)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Не подключено к устройству");
            }

            try
            {
                // Добавляем символ новой строки если его нет
                if (!command.EndsWith("\n"))
                {
                    command += "\n";
                }

                await _serialPort.WriteAsync(Encoding.ASCII.GetBytes(command), 0, command.Length);

                // Ждем ответа с таймаутом
                var response = await ReadResponseAsync(1000);
                return response;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка отправки команды: {ex.Message}");
                throw;
            }
        }

        private async Task<string> ReadResponseAsync(int timeoutMs)
        {
            var buffer = new byte[256];
            var bytesRead = 0;
            var start = DateTime.Now;

            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    bytesRead += await _serialPort.ReadAsync(buffer, bytesRead,
                        Math.Min(_serialPort.BytesToRead, buffer.Length - bytesRead));

                    // Проверяем завершающий символ новой строки
                    if (bytesRead > 0 && buffer[bytesRead - 1] == '\n')
                    {
                        break;
                    }
                }
                await Task.Delay(10);
            }

            return bytesRead > 0 ? Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim() : null;
        }

        private async Task ReadDataAsync()
        {
            var buffer = new byte[1024];

            while (IsConnected)
            {
                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        var bytesRead = await _serialPort.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            var message = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                            MessageReceived?.Invoke(this, message);
                        }
                    }
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"Ошибка чтения: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}