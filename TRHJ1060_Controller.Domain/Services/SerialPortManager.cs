using RJCP.IO.Ports;

namespace TRHJ1060_Controller.Domain.Services;

public class SerialPortManager
{
	public List<string>? SerialPorts { get; }

	private void LoadAvailablePorts()
	{
		SerialPorts.AddRange(SerialPortStream.GetPortNames());
	}
}