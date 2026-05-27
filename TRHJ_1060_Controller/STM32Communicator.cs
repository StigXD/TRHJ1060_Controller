using RJCP.IO.Ports;
using System.Text;

namespace TRHJ_1060_Controller;
public class STM32Communicator
{
    private SerialPortStream _serialPort;
    public bool IsConnected => _serialPort.IsOpen == true;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? ErrorOccurred;
    
    public STM32Communicator(SerialPortManager port)
    {
        _serialPort = port.GetCurrentPort();

        if(_serialPort == null)
        {
            ErrorOccurred?.Invoke(this, "Ошибка подключения к COM порту.");
            port.Dispose();
        }

        ReadDataAsync();
    }
    public async Task<string> SendCommandAsync(byte[] commandBytes)
    {
        try
        {
            // Конвертируем байты в HEX строку (прошивка ожидает HEX формат)
            var hexCommand = BitConverter.ToString(commandBytes).Replace("-", "") + "\n";

            await _serialPort.WriteAsync(Encoding.ASCII.GetBytes(hexCommand), 0, hexCommand.Length);

            // Ждем ответа с таймаутом
			return await ReadResponseAsync(1000);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Ошибка отправки команды: {ex.Message}");
            throw;
        }
    }
    public async Task<string> SendCommandAsync(string hexCommand)
    {
  //      // Убедимся, что это валидная HEX строка
		//if (hexCommand.Length % 2 != 0)
  //          throw new ArgumentException("HEX команда должна содержать четное количество символов");

        // Добавляем символ новой строки если его нет
		if (!hexCommand.EndsWith("\n"))
        {
                hexCommand += "\n";
        }
        
        await _serialPort.WriteAsync(Encoding.ASCII.GetBytes(hexCommand), 0, hexCommand.Length);
        return await ReadResponseAsync(1000);
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
}