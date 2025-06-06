using RJCP.IO.Ports;

namespace TRHJ1060_Controller.Domain.Services
{
	public class SerialPortManager
	{
		public List<string> GetAvailablePorts()
		{
			return new List<string>(SerialPortStream.GetPortNames());
		}
	}
}