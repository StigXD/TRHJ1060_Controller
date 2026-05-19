using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

public class TRHJ2011CommandBuilder
{
    #region Channel Switch Functions (Table 7-8, стр. 15)

    /// <summary>
    /// Включение/выключение каналов
    /// Структура: D0-D3: Chip ID | D4: R/W (0) | D5-D7: 000 | D8-D15: NULL | D16-D23: Channel mask
    /// </summary>
    public static byte[] EnableChannelsMask(byte chipId, bool[] enabled)
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
    public static byte[] SetAmplitudePhase(
        byte chipId,
        byte channel,
        bool isTx,  // true=Tx, false=Rx
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

    private static uint ConvertAmplitudeToBits(double amplitudeDb)
    {
        // 6 бит: 0 = 0dB, 63 = 31.5dB (шаг 0.5dB)
        var value = (int)Math.Round(amplitudeDb * 2);
        return (uint)Math.Clamp(value, 0, 63);
    }

    private static uint ConvertPhaseToBits(double phaseDegrees)
    {
        // 6 бит: 0° = 0, 354.375° = 63
        var value = (int)Math.Round(phaseDegrees / 5.625);
        return (uint)(value % 64);
    }

    public static (double amplitudeDb, double phaseDegrees) ParseAmplitudePhase(uint frame)
    {
        // Извлекаем amplitude (5 бит, D13-D17)
        uint amplitudeBits = (frame >> 13) & 0x1F;
        // Извлекаем phase (6 бит, D18-D23)
        uint phaseBits = (frame >> 18) & 0x3F;

        var amplitudeDb = amplitudeBits * 0.5;   // шаг 0.5 dB
        var phaseDegrees = phaseBits * 5.625;    // шаг 5.625°
        return (amplitudeDb, phaseDegrees);
    }
    #endregion

    #region Control Register Functions (Table 13-14, стр. 17-19)

    /// <summary>
    /// Запись в контрольный регистр
    /// Структура: D0-D3: Chip ID | D4: R/W | D5-D9: 01001 | D10-D15: Address (6 бит) | D16-D23: Data (8 бит)
    /// </summary>
    public static byte[] WriteControlRegister(byte chipId, byte ctrlNumber, byte data8bit)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (ctrlNumber > 63) throw new ArgumentException("CTRL number must be 0-63");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F);               // D0-D3: Chip ID
        frame |= 0u << 4;                             // D4: Write (0)
        frame |= 0b01001u << 5;                       // D5-D9: 01001 (Control Register)
        frame |= (uint)(ctrlNumber & 0x3F) << 10;      // D10-D15: Address (6 бит)
        frame |= (uint)data8bit << 16;                 // D16-D23: Data (8 бит)

        return ConvertFrameToBytes(frame);
    }

    public static byte[] ReadControlRegister(byte chipId, byte ctrlNumber)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (ctrlNumber > 63) throw new ArgumentException("CTRL number must be 0-63");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F);               // D0-D3: Chip ID
        frame |= 1u << 4;                             // D4: Read (1)
        frame |= 0b01001u << 5;                       // D5-D9: 01001 (Control Register)
        frame |= (uint)(ctrlNumber & 0x3F) << 10;      // D10-D15: Address (6 бит)
        frame |= 0u << 16;                            // D16-D23: Data (ignored)

        return ConvertFrameToBytes(frame);
    }

    #endregion

    #region Phase Table Register (Table 16, стр. 19)

    /// <summary>
    /// Запись в таблицу фаз (64 состояния по 24 бита)
    /// Структура: D0-D3: Chip ID | D4: R/W (0) | D5-D9: 00010 | D10-D15: Address (6 бит) | D16-D17: MLSB (2 бита) | D18-D19: NULL | D20-D23: Data (4 бита)
    /// </summary>
    public static byte[] WritePhaseTable(byte chipId, byte address, byte mlsb, byte data4bit)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (address > 63) throw new ArgumentException("Address must be 0-63");
        if (mlsb > 3) throw new ArgumentException("MLSB must be 0-3");
        if (data4bit > 15) throw new ArgumentException("Data must be 0-15");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F);               // D0-D3: Chip ID
        frame |= 0u << 4;                             // D4: Write (0)
        frame |= 0b00010u << 5;                       // D5-D9: 00010 (Phase Table)
        frame |= (uint)(address & 0x3F) << 10;         // D10-D15: Address (6 бит)
        frame |= (uint)(mlsb & 0x03) << 16;            // D16-D17: MLSB (2 бита)
        frame |= 0u << 18;                             // D18-D19: NULL
        frame |= (uint)(data4bit & 0x0F) << 20;        // D20-D23: Data (4 бита)

        return ConvertFrameToBytes(frame);
    }

    #endregion

    #region Signal Register (Table 17-18, стр. 20-21)

    /// <summary>
    /// Чтение сигнального регистра
    /// Структура: D0-D3: Chip ID | D4: Read (1) | D5-D8: 0101 | D9-D15: Address (7 бит) | D16-D23: Data (ignored)
    /// </summary>
    public static byte[] ReadSignalRegister(byte chipId, byte address7bit)
    {
        if (chipId > 15) throw new ArgumentException("Chip ID must be 0-15");
        if (address7bit > 127) throw new ArgumentException("Address must be 0-127");

        uint frame = 0;
        frame |= (uint)(chipId & 0x0F);               // D0-D3: Chip ID
        frame |= 1u << 4;                             // D4: Read (1)
        frame |= 0b0101u << 5;                        // D5-D8: 0101 (Signal Register)
        frame |= (uint)(address7bit & 0x7F) << 9;      // D9-D15: Address (7 бит)
        frame |= 0u << 16;                            // D16-D23: Data (ignored)

        return ConvertFrameToBytes(frame);
    }

    /// <summary>
    /// Подготовка к чтению температуры (стр. 21)
    /// </summary>
    public static byte[][] PrepareTemperatureReading(byte chipId)
    {
        return new byte[][]
        {
            WriteControlRegister(chipId, 63, 0x00),   // CTRL63: ADC input selection
            WriteControlRegister(chipId, 16, 0x01),   // CTRL16: ADC enable
        };
    }

    /// <summary>
    /// Полный цикл чтения температуры
    /// </summary>
    public static byte[][] BuildTemperatureReadSequence(byte chipId)
    {
        var sequence = new byte[3][];
        sequence[0] = WriteControlRegister(chipId, 63, 0x00);  // CTRL63
        sequence[1] = WriteControlRegister(chipId, 16, 0x01);  // CTRL16
        sequence[2] = ReadSignalRegister(chipId, 0x07);        // SIG07
        return sequence;
    }

    /// <summary>
    /// Парсинг температуры из ответа (10 бит, формула из даташита)
    /// </summary>
    public static float ParseTemperatureResponse(byte[] response)
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

    #region Utility Methods

    /// <summary>
    /// Преобразование 24-битного кадра в 3 байта (порядок: D0-D7, D8-D15, D16-D23)
    /// </summary>
    private static byte[] ConvertFrameToBytes(uint frame)
    {
        byte[] bytes = new byte[3];
        bytes[0] = (byte)(frame & 0xFF);           // D0-D7 (младшие 8 бит)
        bytes[1] = (byte)((frame >> 8) & 0xFF);    // D8-D15
        bytes[2] = (byte)((frame >> 16) & 0xFF);   // D16-D23 (старшие 8 бит)
        return bytes;
    }

    public static uint BytesToFrame(byte[] bytes)
    {
        if (bytes.Length != 3) return 0;
        return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16));
    }

    public static string BytesToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "");
    }

    #endregion
}
