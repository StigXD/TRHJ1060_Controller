namespace TRHJ1060_Controller.Domain.Models
{
	public class TRHJ1060_CommandBuilder
	{
		// Основные функции согласно Table 7 (стр. 11)
		public enum SpiFunction : uint
		{
			ChannelSwitch = 0b000,       // D5-D7: 000
			AmplitudePhase = 0b001,      // D5-D7: 001  
			CalibrationRegister = 0b010, // D5-D7: 010
			SignalRegister = 0b101,      // D5-D7: 101
			ControlRegister = 0b011      // D5-D7: 011
		}

		#region Channel Switch Functions (Таблица 8-9)

		public static byte[] EnableChannel(byte chipId, byte channel, bool enable)
		{
			if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
			if (channel > 3) throw new ArgumentException("Channel must be 0-3");

			uint frame = 0;

			// Chip ID (4 бита) - D0-D3
			frame |= (uint) (chipId & 0x0F) << 20;

			// Write operation (0) - D4
			frame |= 0u << 19;

			// Function: Channel Switch (000) - D5-D7
			frame |= (uint) SpiFunction.ChannelSwitch << 16;

			// Enable bit - D8
			frame |= (uint) (enable ? 1 : 0) << 15;

			// NULL bits (D9-D19) - заполняем 1 (документация позволяет 0 или 1)
			frame |= 0b11111111111u << 4;

			// Channel number (4 бита) - D20-D23
			uint channelBits = channel switch
			{
				0 => 0b0001,
				1 => 0b0010,
				2 => 0b0100,
				3 => 0b1000,
				_ => 0b0000
			};
			frame |= channelBits;

			return ConvertFrameToBytes(frame);
		}

		public static byte[] EnableAllChannels(byte chipId, bool enable)
		{
			uint frame = 0;

			frame |= (uint) (chipId & 0x0F) << 20;
			frame |= 0u << 19;
			frame |= (uint) SpiFunction.ChannelSwitch << 16;
			frame |= (uint) (enable ? 1 : 0) << 15;
			frame |= 0b11111111111u << 4; // NULL bits

			// Все каналы: 0b1111 или выключены: 0b0000
			frame |= enable ? 0b1111u : 0b0000u;

			return ConvertFrameToBytes(frame);
		}

		#endregion

		#region Amplitude and Phase Functions (Таблицы 10-13)

		public static byte[] SetAmplitudePhase(
			byte chipId,
			byte channel,
			bool isTx,
			double amplitudeDb,
			double phaseDegrees)
		{
			if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
			if (channel > 3) throw new ArgumentException("Channel must be 0-3");
			if (amplitudeDb < 0 || amplitudeDb > 15.5) throw new ArgumentException("Amplitude must be 0-15.5 dB");
			if (phaseDegrees < 0 || phaseDegrees >= 360) throw new ArgumentException("Phase must be 0-359.999°");

			// Конвертация значений в битовые представления
			uint amplitudeBits = ConvertAmplitudeToBits(amplitudeDb);
			uint phaseBits = ConvertPhaseToBits(phaseDegrees);

			uint frame = 0;

			// Chip ID (4 бита) - D0-D3
			frame |= (uint) (chipId & 0x0F) << 20;

			// Write operation (0) - D4
			frame |= 0u << 19;

			// Function: Amplitude/Phase (001) - D5-D7
			frame |= (uint) SpiFunction.AmplitudePhase << 16;

			// Tx/Rx selection (0=Tx, 1=Rx) - D9
			frame |= (uint) (isTx ? 0 : 1) << 15;

			// Channel selection (2 бита) - D10-D11
			frame |= (uint) (channel & 0x03) << 13;

			// Amplitude (6 бит) - D13-D17 (D12 не используется)
			frame |= amplitudeBits << 7;

			// Phase (6 бит) - D18-D23
			frame |= phaseBits << 1;

			return ConvertFrameToBytes(frame);
		}

		private static uint ConvertAmplitudeToBits(double amplitudeDb)
		{
			// Конвертация dB в битовое представление (0-31)
			// 0dB = 0, -15.5dB = 31
			int value = (int) Math.Round(amplitudeDb * 2);
			return (uint) Math.Clamp(value, 0, 31);
		}

		private static uint ConvertPhaseToBits(double phaseDegrees)
		{
			// Конвертация градусов в битовое представление (0-63)
			// 0° = 0, 354.375° = 63
			int value = (int) Math.Round(phaseDegrees / 5.625);
			return (uint) (value % 64); // 6 бит = 64 значения
		}

		public static (double amplitudeDb, double phaseDegrees) ParseAmplitudePhase(uint amplitudeBits, uint phaseBits)
		{
			double amplitudeDb = amplitudeBits * 0.5;
			double phaseDegrees = phaseBits * 5.625;
			return (amplitudeDb, phaseDegrees);
		}

		#endregion

		#region Control Register Functions (Таблицы 14-15)

		public static byte[] WriteControlRegister(byte chipId, byte registerAddress, byte registerValue)
		{
			if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");

			uint frame = 0;

			// Chip ID (4 бита) - D0-D3
			frame |= (uint) (chipId & 0x0F) << 20;

			// Write operation (0) - D4
			frame |= 0u << 19;

			// Function: Control Register (011) - D5-D7
			frame |= (uint) SpiFunction.ControlRegister << 16;

			// Register address (8 бит) - D8-D15
			frame |= (uint) (registerAddress & 0xFF) << 8;

			// Register value (8 бит) - D16-D23
			frame |= (uint) (registerValue & 0xFF);

			return ConvertFrameToBytes(frame);
		}

		public static byte[] ReadControlRegister(byte chipId, byte registerAddress)
		{
			if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");

			uint frame = 0;

			// Chip ID (4 бита) - D0-D3
			frame |= (uint) (chipId & 0x0F) << 20;

			// Read operation (1) - D4
			frame |= 1u << 19;

			// Function: Control Register (011) - D5-D7
			frame |= (uint) SpiFunction.ControlRegister << 16;

			// Register address (8 бит) - D8-D15
			frame |= (uint) (registerAddress & 0xFF) << 8;

			// Data part can be 0 (will be ignored during read) - D16-D23
			frame |= 0u;

			return ConvertFrameToBytes(frame);
		}

		#endregion

		#region Signal Register Functions (Temperature Reading) (Таблицы 18-21)

		public static byte[] ReadSignalRegister(byte chipId, byte registerAddress)
		{
			if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");

			uint frame = 0;

			// Chip ID (4 бита) - D0-D3
			frame |= (uint) (chipId & 0x0F) << 20;

			// Read operation (1) - D4
			frame |= 1u << 19;

			// Function: Signal Register (101) - D5-D7
			frame |= (uint) SpiFunction.SignalRegister << 16;

			// Register address (8 бит) - D8-D15
			frame |= (uint) (registerAddress & 0xFF) << 8;

			// Data part is ignored for read commands - D16-D23
			frame |= 0u;

			return ConvertFrameToBytes(frame);
		}

		// Подготовка к чтению температуры (многошаговая процедура - стр. 17-18)
		public static byte[][] PrepareTemperatureReading(byte chipId)
		{
			return new byte[][]
			{
				// 1. Configure CTRL244 - ADC input selection (temperature)
				WriteControlRegister(chipId, 0xF4, 0x00), // CTRL244 = 0x00 (temperature input)

				// 2. Configure CTRL16 - ADC clock enable and divider
				WriteControlRegister(chipId, 0x10, 0x8C), // Enable + divider 100

				// 3. Configure CTRL245 - Temperature calibration
				WriteControlRegister(chipId, 0xF5, 0x20) // Default calibration 10_0000
			};
		}

		public static float ParseTemperatureResponse(byte[] response)
		{
			if (response == null || response.Length < 3)
				return float.NaN;

			// Извлекаем 10-битное значение температуры (биты D8-D17 ответного фрейма)
			// response[0] - старший байт, response[1] - средний, response[2] - младший
			uint adcValue = ((uint) (response[1] & 0x3F) << 4) | ((uint) (response[2] & 0xF0) >> 4);

			// Формула из документации: Temperature = 249 - 0.364 * Code
			return 249f - 0.364f * adcValue;
		}

		#endregion

		#region Special Commands

		public static byte[] ResetChip(byte chipId)
		{
			// Команда сброса через контрольный регистр
			return WriteControlRegister(chipId, 0xFF, 0x00);
		}

		public static byte[] InitializeDefaultRegisters(byte chipId)
		{
			// Инициализация контрольных регистров по умолчанию (Таблица 15)
			return WriteControlRegister(chipId, 0x00, 0x20); // CH1 TX PA bias
		}

		public static byte[] SetHighSpeedMode(byte chipId, bool highSpeed)
		{
			// Установка высокоскоростного режима (Таблица 17)
			byte value = highSpeed ? (byte) 0x00 : (byte) 0x0F;
			return WriteControlRegister(chipId, 0x20, value);
		}

		#endregion

		#region Utility Methods

		private static byte[] ConvertFrameToBytes(uint frame)
		{
			// Правильное преобразование 24-битного кадра в 3 байта
			byte[] bytes = new byte[3];
			bytes[0] = (byte) ((frame >> 16) & 0xFF); // Старший байт (D0-D7)
			bytes[1] = (byte) ((frame >> 8) & 0xFF);  // Средний байт (D8-D15)
			bytes[2] = (byte) (frame & 0xFF);         // Младший байт (D16-D23)
			return bytes;
		}

		public static uint BytesToFrame(byte[] bytes)
		{
			if (bytes.Length != 3) return 0;
			return ((uint) bytes[0] << 16) | ((uint) bytes[1] << 8) | bytes[2];
		}

		public static string FrameToBinaryString(uint frame)
		{
			return Convert.ToString(frame, 2).PadLeft(24, '0');
		}

		public static string BytesToHexString(byte[] bytes)
		{
			return BitConverter.ToString(bytes).Replace("-", "");
		}

		public static void ParseFrame(uint frame, out byte chipId, out bool isRead, out SpiFunction function, out byte[] data)
		{
			chipId = (byte) ((frame >> 20) & 0x0F);
			isRead = ((frame >> 19) & 0x01) == 1;
			function = (SpiFunction) ((frame >> 16) & 0x07);

			data = new byte[2];
			data[0] = (byte) ((frame >> 8) & 0xFF); // Address или старшие данные
			data[1] = (byte) (frame & 0xFF);        // Value или младшие данные
		}

		#endregion

		#region Predefined Control Registers (Таблица 15)

		public static class ControlRegisters
		{
			// Bias registers
			public const byte CH1_TX_PA_BIAS = 0x00;
			public const byte CH1_TX_DA_BIAS = 0x01;
			public const byte CH1_TX_AMP_BIAS = 0x02;
			public const byte CH1_RX_LNA_BIAS = 0x03;
			public const byte CH1_RX_AMP_BIAS = 0x04;

			// Temperature coefficients (Таблица 16)
			public const byte TX_DA_TEMP_COEFF = 0xF6;  // CTRL246
			public const byte TX_PA_TEMP_COEFF = 0xF7;  // CTRL247
			public const byte TX_AMP_TEMP_COEFF = 0xFA; // CTRL250
			public const byte RX_LNA_TEMP_COEFF = 0xFB; // CTRL251
			public const byte RX_AMP_TEMP_COEFF = 0xFE; // CTRL254

			// Default values
			public const byte DEFAULT_BIAS = 0x20;
			public const byte DEFAULT_TEMP_COEFF = 0x5A;
		}

		#endregion
	}
}