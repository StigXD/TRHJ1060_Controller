using RJCP.IO.Ports;
using System.Windows.Navigation;

namespace TRHJ_1060_Controller;
public class SerialPortManager : IDisposable
{
	private SerialPortStream? _serialPort;
	private bool IsConnected => _serialPort?.IsOpen == true;
	public event EventHandler<string> ErrorOccurred;

	public List<string> GetAvailablePorts()
	{
		return SerialPortStream.GetPortNames().ToList();
	}
	
	public async Task <bool> CreateSerialPort(string portName)
	{
		try
		{
			_serialPort = new SerialPortStream(portName)
			{
				BaudRate = 9600,
				DataBits = 8,
				Parity = Parity.None,
				StopBits = StopBits.One,
				ReadTimeout = 1000,
				WriteTimeout = 1000,
				DtrEnable = true,
				RtsEnable = true
			};

			_serialPort.Open() ;
			return true;
        }
		catch (Exception ex) 
		{
            ErrorOccurred?.Invoke(this, $"Ошибка подключения: {ex.Message}");
            return false;
        }
	}

	public SerialPortStream? GetCurrentPort()
	{
		if (_serialPort == null) 
		{
			return null;
		}
		return _serialPort;
	}

	public bool GetStatus()
	{
		return IsConnected;
	}

    private void Disconnect()
    {
        try
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Ошибка отключения: {ex.Message}");
        }
    }

    public void Dispose()
	{
		Disconnect();
	}
}
