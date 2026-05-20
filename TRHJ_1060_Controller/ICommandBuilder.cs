using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

public interface ICommandBuilder
{
    public abstract byte[] EnableChannelsMask(byte chipId, bool[] enabled);
    public abstract byte[] SetAmplitudePhase(byte chipId, byte channel, int mode, double amplitudeDb, double phaseDegrees);
    public abstract (double amplitudeDb, double phaseDegrees) ParseAmplitudePhase(uint amplitudeBits, uint phaseBits);
    public abstract byte[] WriteControlRegister(byte chipId, byte registerAddress, byte registerValue);
    public abstract byte[] ReadControlRegister(byte chipId, byte registerAddress);
    public abstract byte[] ReadSignalRegister(byte chipId, byte registerAddress);
    public abstract byte[][] PrepareTemperatureReading(byte chipId);
    public abstract byte[][] BuildTemperatureReadSequence(byte chipId);
    public abstract float ParseTemperatureResponse(byte[] response);
    public abstract byte[] ResetChip(byte chipId);
    public abstract byte[] InitializeDefaultRegisters(byte chipId);
    public abstract byte[] SetHighSpeedMode(byte chipId, bool highSpeed);
    public abstract byte[] ConvertFrameToBytes(uint frame);
    public abstract uint BytesToFrame(byte[] bytes);
    public abstract string FrameToBinaryString(uint frame);
    public abstract string BytesToHexString(byte[] bytes);
    public abstract void ParseFrame(uint frame, out byte chipId, out bool isRead, out uint function, out byte[] data);
}
