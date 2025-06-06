namespace TRHJ1060_Controller.Domain.Models
{
	public class TRHJ1060_CommandBuilder
	{
		public static byte[] EnableChannel0(byte chipID = 0)
		{
			uint frame = 0;
			frame |= (uint)(chipID & 0x0F) << 20;
			frame |= 0 << 19;
			frame |= 0b000 << 16;
			frame |= 0b1 << 15;
			frame |= 0b0010 << 4;

			return ConvertFrameToBytes(frame);
		}

		public static byte[] SetAmplitudePhase(
			byte chipID,
			byte channel,
			bool isTx,
			int amplitude,
			int phase)
		{
			uint frame = 0;
			frame |= (uint)(chipID & 0x0F) << 20;
			frame |= 0 << 19;
			frame |= 0b001 << 16;
			frame |= (uint)(isTx ? 0 : 1) << 15;
			frame |= (uint)(channel & 0x03) << 13;
			frame |= (uint)(amplitude & 0x3F) << 7;
			frame |= (uint)(phase & 0x3F) << 1;

			return ConvertFrameToBytes(frame);
		}

		private static byte[] ConvertFrameToBytes(uint frame)
		{
			return BitConverter.GetBytes(frame).Reverse().ToArray();
		}
	}
}