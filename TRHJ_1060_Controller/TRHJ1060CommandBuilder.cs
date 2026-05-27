using System.Text;

namespace TRHJ_1060_Controller;
public class TRHJ1060CommandBuilder : ICommandBuilder
{
    private ControlRegisters controlRegisters = new ControlRegisters();

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
    public byte[] EnableChannelsMask(byte chipId, bool[] enabled)
	{

		if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
		if (enabled == null || enabled.Length != 4) throw new ArgumentException("Enabled array must have exactly 4 elements");
		uint channelCode = 0;
		for (var i = 0; i < enabled.Length; i++)
        {
            if (enabled[i])
                channelCode |= (uint)(1 << i); // Канал 0 → бит 0 → D20
        }

        uint frame = 0;
		
		frame |= (uint) (chipId & 0x0F) << 20; // Зеркально относительно Datasheet на TRHJ-1060 (D0-D3 в документации → D20-D23 в коде)
        frame |= 0u << 19;
        frame |= (uint)SpiFunction.ChannelSwitch << 16;
        frame |= (uint)1u << 15;
		frame |= channelCode;
		
		return ConvertFrameToBytes(frame);
	}
	#endregion

	#region Amplitude and Phase Functions (Таблицы 10-13)

	public byte[] SetAmplitudePhase(
		byte chipId,
		byte channel,
		int mode,
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

		frame |= (uint) (chipId & 0x0F) << 20;
		frame |= 0u << 19;
		frame |= (uint) SpiFunction.AmplitudePhase << 16;
		frame |= (uint) (mode == 0 ? 1 : 0) << 14;
		frame |= (uint) (channel & 0x02) << 12;
		frame |= amplitudeBits << 6;
		frame |= phaseBits;

		return ConvertFrameToBytes(frame);
	}

		private uint ConvertAmplitudeToBits(double amplitudeDb)
		{
			// Конвертация dB в битовое представление (0-31)
			// 0dB = 0, -15.5dB = 31
			var value = (int) Math.Round(amplitudeDb * 2);
			return (uint) Math.Clamp(value, 0, 31);
		}

		private uint ConvertPhaseToBits(double phaseDegrees)
		{
			// Конвертация градусов в битовое представление (0-63)
			// 0° = 0, 354.375° = 63
			var value = (int) Math.Round(phaseDegrees / 5.625);
			return (uint) (value % 64); // 6 бит = 64 значения
		}

		public (double amplitudeDb, double phaseDegrees) ParseAmplitudePhase(uint amplitudeBits, uint phaseBits)
		{
			var amplitudeDb = amplitudeBits * 0.5;
			var phaseDegrees = phaseBits * 5.625;
			return (amplitudeDb, phaseDegrees);
		}

		#endregion

	#region Control Register Functions (Таблицы 14-15)

		public byte[] WriteControlRegister(byte chipId, byte registerAddress, byte registerValue)
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

		public byte[] ReadControlRegister(byte chipId, byte registerAddress)
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

		public byte[] ReadSignalRegister(byte chipId, byte registerAddress)
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
		public byte[][] PrepareTemperatureReading(byte chipId)
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

		/// <summary>
		/// Формирует последовательность команд для полного цикла чтения температуры (по даташиту).
		/// </summary>
		/// <param name="chipId">ID микросхемы</param>
		/// <returns>Массив команд: 3 управляющих + 1 чтение сигнального регистра</returns>
		public byte[][] BuildTemperatureReadSequence(byte chipId)
		{
			var sequence = new byte[4][];
			// 1. CTRL244 = 0x00 (temperature input)
			sequence[0] = WriteControlRegister(chipId, 0xF4, 0x00);
			// 2. CTRL16 = 0x8C (ADC enable + divider)
			sequence[1] = WriteControlRegister(chipId, 0x10, 0x8C);
			// 3. CTRL245 = 0x20 (default calibration)
			sequence[2] = WriteControlRegister(chipId, 0xF5, 0x20);
			// 4. Чтение сигнального регистра (адрес 0x00)
			sequence[3] = ReadSignalRegister(chipId, 0x00);
			return sequence;
		}

		public float ParseTemperatureResponse(byte[] response)
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
	public byte[] ResetChip(byte chipId)
	{
			// Команда сброса через контрольный регистр
			return WriteControlRegister(chipId, 0xFF, 0x00);
	}
	public string InitializeDefaultRegisters(byte chipId)
	{
        string regInString = string.Empty;
        var registers = new byte[controlRegisters.ControlRegisterAddress1060.Count()][];
		for (var i = 0; i < controlRegisters.ControlRegisterAddress1060.Count(); i++)
		{
			registers[i] = WriteControlRegister(chipId, controlRegisters.ControlRegisterAddress1060[i], controlRegisters.ControlRegisterValue1060[i]);
            regInString += BytesToHexString(registers[i]) + " ";
        }
        return regInString.TrimEnd();
	}
	public byte[] SetHighSpeedMode(byte chipId, bool highSpeed)
	{
		// Установка высокоскоростного режима (Таблица 17)
		byte value = highSpeed ? (byte) 0x00 : (byte) 0x0F;
		return WriteControlRegister(chipId, 0x20, value);
	}
		#endregion

	#region Special Commands for STM32 Protocol

		public class SpecialCommands
		{
			public byte[] ResetCommand => new byte[] { 0x00, 0x00, 0x00 };
			public byte[] InitializeRegistersCommand => new byte[] { 0xFF, 0xFF, 0xFF };
			public byte[] TemperatureReadCommand => new byte[] { 0x02, 0x00, 0x00 };

			// Команда для передачи данных (прошивка ожидает 0x01 в старшем байте)
			public byte[] CreateDataTransmitCommand(byte[] dataBytes)
			{
				if (dataBytes.Length != 3)
					throw new ArgumentException("Должно быть 3 байта данных");

				return new byte[] { 0x01, dataBytes[1], dataBytes[2] };
			}
		}

		#endregion

	#region Utility Methods

		public byte[] ConvertFrameToBytes(uint frame)
		{
			// Правильное преобразование 24-битного кадра в 3 байта
			byte[] bytes = new byte[3];
			bytes[0] = (byte) ((frame >> 16) & 0xFF); // Старший байт (D0-D7)
			bytes[1] = (byte) ((frame >> 8) & 0xFF);  // Средний байт (D8-D15)
			bytes[2] = (byte) (frame & 0xFF);         // Младший байт (D16-D23)
        return bytes;
		}

		public uint BytesToFrame(byte[] bytes)
		{	
			if (bytes.Length != 3) return 0;
			return ((uint) bytes[0] << 16) | ((uint) bytes[1] << 8) | bytes[2];
		}

		public string FrameToBinaryString(uint frame)
		{
			return Convert.ToString(frame, 2).PadLeft(24, '0');
		}

		public string BytesToHexString(byte[] bytes)
		{
			return BitConverter.ToString(bytes).Replace("-", "");
		}

		public void ParseFrame(uint frame, out byte chipId, out bool isRead, out uint function, out byte[] data)
		{
			chipId = (byte) ((frame >> 20) & 0x0F);
			isRead = ((frame >> 19) & 0x01) == 1;
			function = ((frame >> 16) & 0x07);

			data = new byte[2];
			data[0] = (byte) ((frame >> 8) & 0xFF); // Address или старшие данные
			data[1] = (byte) (frame & 0xFF);        // Value или младшие данные
		}

		#endregion
}