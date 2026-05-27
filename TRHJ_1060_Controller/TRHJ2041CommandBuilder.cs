using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

public class TRHJ2041CommandBuilder : ICommandBuilder
{
    private ControlRegisters controlRegisters = new ControlRegisters();

    #region Channel Switch Functions (Table 7-8, стр. 15)

    /// <summary>
    /// Включение/выключение каналов
    /// Структура: D0-D3: Chip ID | D4: R/W (0) | D5-D7: 000 | D8-D15: NULL | D16-D23: Channel mask
    /// </summary>
    public byte[] EnableChannelsMask(byte chipId, bool[] enabled)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (enabled == null || enabled.Length != 8) throw new ArgumentException("Enabled array must have exactly 8 elements");

        uint channelMask = 0;
        for (var i = 0; i < enabled.Length; i++)
        {
            if (enabled[i])
                channelMask |= (uint)(1 << i); // Бит 0 = Channel 0 → D16
        }

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;   // D0-D3: Chip ID
        frame |= 0u << 19;                     // D4: Write (0)
                                               // D5-D7: 000 (Channel Switch)
                                               // D8-D15: NULL (0)
        frame |= channelMask;               // D16-D23: Channel mask

        return ConvertFrameToBytes(frame);
    }

    #endregion

    #region Amplitude and Phase Functions (Table 9-12, стр. 16-17)

    /// <summary>
    /// Установка амплитуды и фазы для канала
    /// Структура: D0-D3: Chip ID | D4: R/W (0) | D5-D7: 001 | D8: NULL | D9: Mode (0=Rx,1=Tx) | D10-D11: Channel | D12: NULL | D13-D17: Amplitude (5 бит) | D18-D23: Phase (6 бит)
    /// </summary>
    public byte[] SetAmplitudePhase(
        byte chipId,
        byte channel,
        int isTx,  // true=Tx, false=Rx
        double amplitudeDb,
        double phaseDegrees)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (channel > 7) throw new ArgumentException("Channel must be 0-7");
        if (amplitudeDb < 0 || amplitudeDb > 31.5) throw new ArgumentException("Amplitude must be 0-31.5 dB"); // 31.5 dB для 6 бит
        if (phaseDegrees < 0 || phaseDegrees >= 360) throw new ArgumentException("Phase must be 0-359.999°");

        uint amplitudeBits = ConvertAmplitudeToBits(amplitudeDb);
        uint phaseBits = ConvertPhaseToBits(phaseDegrees);

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;        // D0-D3: Chip ID
        frame |= 0u << 19;                          // D4: Write (0)
        frame |= 0b001u << 16;                     // D5-D7: 001 (Amplitude/Phase)
                                                   // D8: NULL
        frame |= (uint)(channel & 0x07) << 12;   // D10-D12? Channel (3 бита для 8 каналов)
        frame |= amplitudeBits << 6;            // D13-D17: Amplitude (5 бит)
        frame |= phaseBits;                    // D18-D23: Phase (6 бит)

        return ConvertFrameToBytes(frame);
    }

    private uint ConvertAmplitudeToBits(double amplitudeDb)
    {
        // 6 бит: 0 = 0dB, 63 = 31.5dB (шаг 0.5dB)
        var value = (int)Math.Round(amplitudeDb * 2);
        return (uint)Math.Clamp(value, 0, 63);
    }

    private uint ConvertPhaseToBits(double phaseDegrees)
    {
        // 6 бит: 0° = 0, 354.375° = 63
        var value = (int)Math.Round(phaseDegrees / 5.625);
        return (uint)(value % 64);
    }

    public (double amplitudeDb, double phaseDegrees) ParseAmplitudePhase(uint amplitudeBits, uint phaseBits)
    {
        var amplitudeDb = amplitudeBits * 0.5;
        var phaseDegrees = phaseBits * 5.625;
        return (amplitudeDb, phaseDegrees);
    }
    #endregion

    #region Control Register Functions (Table 13-14, стр. 17-19)

    /// <summary>
    /// Запись в контрольный регистр
    /// Структура: D0-D3: Chip ID | D4: R/W | D5-D9: 01001 | D10-D15: Address (6 бит) | D16-D23: Data (8 бит)
    /// </summary>
    public byte[] WriteControlRegister(byte chipId, byte ctrlNumber, byte data8bit)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (ctrlNumber > 63) throw new ArgumentException("CTRL number must be 0-63");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;               // D0-D3: Chip ID
        frame |= 0u << 19;                             // D4: Write (0)
        frame |= 0b01001u << 14;                       // D5-D9: 01001 (Control Register)
        frame |= (uint)(ctrlNumber & 0x3F) << 8;      // D10-D15: Address (6 бит)
        frame |= (uint)data8bit;                 // D16-D23: Data (8 бит)

        return ConvertFrameToBytes(frame);
    }

    public byte[] ReadControlRegister(byte chipId, byte ctrlNumber)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (ctrlNumber > 63) throw new ArgumentException("CTRL number must be 0-63");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;               // D0-D3: Chip ID
        frame |= 1u << 19;                             // D4: Read (1)
        frame |= 0b01001u << 14;                       // D5-D9: 01001 (Control Register)
        frame |= (uint)(ctrlNumber & 0x3F) << 8;      // D10-D15: Address (6 бит)

        return ConvertFrameToBytes(frame);
    }

    #endregion

    #region Phase Table Register (Table 16, стр. 19)

    /// <summary>
    /// Запись в таблицу фаз (64 состояния по 24 бита)
    /// Структура: D0-D3: Chip ID | D4: R/W (0) | D5-D7: 010 | D8-D13: Address (6 бит) | D14-D15: MLSB (2 бита) | D16-D17: NULL | D18-D23: Data (4 бита)
    /// </summary>
    public byte[] WritePhaseTable(byte chipId, byte address, byte mlsb, byte data6bit)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (address > 63) throw new ArgumentException("Address must be 0-63");
        if (mlsb > 3) throw new ArgumentException("MLSB must be 0-3");
        if (data6bit > 63) throw new ArgumentException("Data must be 0-360");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;           // D0-D3: Chip ID
        frame |= 0u << 19;                             // D4: Write (0)
        frame |= 0b010u << 16;                        // D5-D9: 010 (Phase Table)
        frame |= (uint)(address & 0x3F) << 10;       // D10-D15: Address (6 бит)
        frame |= (uint)(mlsb & 0x03) << 8;          // D16-D17: MLSB (2 бита)
        frame |= (uint)(data6bit & 0x3F);          // D20-D23: Data (6 бит)

        return ConvertFrameToBytes(frame);
    }
    public byte[] ReadPhaseTable(byte chipId, byte address, byte mlsb)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (address > 63) throw new ArgumentException("Address must be 0-63");
        if (mlsb > 3) throw new ArgumentException("MLSB must be 0-3");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;           // D0-D3: Chip ID
        frame |= 1u << 19;                             // D4: Write (0)
        frame |= 0b010u << 16;                        // D5-D9: 010 (Phase Table)
        frame |= (uint)(address & 0x3F) << 10;       // D10-D15: Address (6 бит)
        frame |= (uint)(mlsb & 0x03) << 8;          // D16-D17: MLSB (2 бита)

        return ConvertFrameToBytes(frame);
    }

    #endregion

    #region Signal Register (Table 17-18, стр. 20-21)

    /// <summary>
    /// Чтение сигнального регистра
    /// Структура: D0-D3: Chip ID | D4: Read (1) | D5-D8: 0101 | D9-D15: Address (7 бит) | D16-D23: Data (ignored)
    /// </summary>
    public byte[] ReadSignalRegister(byte chipId, byte registerAddress)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F) << 20;        // D0-D3: Chip ID
        frame |= 1u << 19;                          // D4: Read (1)
        frame |= 0b0101u << 15;                    // D5-D8: 0101 (Signal Register)
        frame |= 0x07 << 12;                      // D9-D15: Address (7 бит)

        return ConvertFrameToBytes(frame);
    }

    /// <summary>
    /// Подготовка к чтению температуры (стр. 21)
    /// </summary>
    public byte[][] PrepareTemperatureReading(byte chipId)
    {
        return new byte[][]
        {
            WriteControlRegister(chipId, 62, 0x00),   // CTRL62: ADC input selection
            WriteControlRegister(chipId, 63, 0x20),   // CTRL63: Temperature calibration
        };
    }

    /// <summary>
    /// Полный цикл чтения температуры
    /// </summary>
    public byte[][] BuildTemperatureReadSequence(byte chipId)
    {
        var sequence = new byte[3][];
        sequence[0] = WriteControlRegister(chipId, 62, 0x00);   // CTRL62: ADC input selection
        sequence[1] = WriteControlRegister(chipId, 63, 0x20);  // CTRL63: Temperature calibration
        sequence[2] = ReadSignalRegister(chipId, 0x07);             // SIG07
        return sequence;
    }

    /// <summary>
    /// Парсинг температуры из ответа (10 бит, формула из даташита)
    /// </summary>
    public float ParseTemperatureResponse(byte[] response)
    {
        if (response == null || response.Length < 3)
            return float.NaN;

        // 10 бит данных (игнорируем D12-D13)
        // Согласно документации: D8-D11 и D14-D19
        uint highBits = (uint)((response[1] >> 0) & 0x0F);  // D8-D11 (младшие 4 бита 2-го байта)
        uint lowBits = (uint)((response[2] >> 0) & 0x3F);   // D14-D19 (младшие 6 бит 3-го байта)
        uint adcValue = (highBits << 6) | lowBits;

        // Формула: Temperature = 249 - 0.364 * Code
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
        var registers = new byte[controlRegisters.ControlRegisterAddress2041.Count()][];
        for (var i = 0; i < controlRegisters.ControlRegisterAddress2041.Count(); i++)
        {
            registers[i] = WriteControlRegister(chipId, controlRegisters.ControlRegisterAddress2041[i], controlRegisters.ControlRegisterValue2041[i]);
            regInString += BytesToHexString(registers[i])+" ";
        }
        return regInString.TrimEnd();
    }

    public byte[] SetHighSpeedMode(byte chipId, bool highSpeed)
    {
        // Установка высокоскоростного режима (Таблица 17)
        byte value = highSpeed ? (byte)0x00 : (byte)0x0F;
        return WriteControlRegister(chipId, 0x20, value);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Преобразование 24-битного кадра в 3 байта (порядок: D0-D7, D8-D15, D16-D23)
    /// </summary>
    public byte[] ConvertFrameToBytes(uint frame)
    {
        byte[] bytes = new byte[3];
        bytes[0] = (byte)((frame >> 16) & 0xFF);    // Старший байт (D0-D7)
        bytes[1] = (byte)((frame >> 8) & 0xFF);    //  Средний байт (D8-D15)
        bytes[2] = (byte)(frame & 0xFF);          //   Младший байт (D16-D23)
        return bytes;
    }

    public uint BytesToFrame(byte[] bytes)
    {
        if (bytes.Length != 3) return 0;
        return ((uint)bytes[0] << 16) | ((uint)bytes[1] << 8) | bytes[2];
    }

    public string BytesToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "");
    }

    public string FrameToBinaryString(uint frame)
    {
        return Convert.ToString(frame, 2).PadLeft(24, '0');
    }

    public void ParseFrame(uint frame, out byte chipId, out bool isRead, out uint function, out byte[] data)
    {
        chipId = (byte)((frame >> 20) & 0x0F);
        isRead = ((frame >> 19) & 0x01) == 1;
        function = ((frame >> 16) & 0x07);

        data = new byte[2];
        data[0] = (byte)((frame >> 8) & 0xFF); // Address или старшие данные
        data[1] = (byte)(frame & 0xFF);        // Value или младшие данные
    }

    #endregion

}
