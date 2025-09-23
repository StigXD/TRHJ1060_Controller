using RJCP.IO.Ports;

namespace TRHJ1060_Controller.Domain.Services
{
	public class SerialPortManager
	{
		public List<string> GetAvailablePorts()
		{
			return SerialPortStream.GetPortNames().ToList();
		}

		public SerialPortStream CreateSerialPort(string portName)
		{
			return new SerialPortStream(portName)
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
		}
	}
}