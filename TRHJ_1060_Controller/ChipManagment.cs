using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

public class ChipManagment
{
    private STM32Communicator _communicator;
    private SerialPortManager _serialPortManager;
    private ChipTypes _chipTypes;
    private object _commandBuilder;
    private EventLogger _logger;

    public ChipManagment(ChipTypes chipType, SerialPortManager serialPort, EventLogger logger)
    {
        _chipTypes = chipType;
        _serialPortManager = serialPort;
        _communicator = new STM32Communicator(serialPort);
        _logger = logger ?? new EventLogger();
    }

    public async Task<string> InitializeChipAsync(byte chipId, )
    {
        try
        {
            _logger.LogInfo($"Инициализация чипа {chipId}...");
            var command = _commandBuilder.WriteControlRegister( chipId );
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"INIT ChipId={chipId}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка инициализации чипа {chipId}: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ResetChipAsync(byte chipId)
    {
        try
        {
            _logger.LogInfo($"Сброс чипа {chipId}...");
            var command = TRHJ1060CommandBuilder.ResetChip(chipId);
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"RESET ChipId={chipId}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка сброса чипа {chipId}: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SetParametersAsync(byte chipId, int mode, byte channel, double att, double phase)
    {
        try
        {
            _logger.LogInfo($"Установка параметров: ChipId={chipId}, Mode={mode}, Channel={channel}, Att={att}dB, Phase={phase}°");
            
            var command = TRHJ1060CommandBuilder.SetAmplitudePhase(chipId, channel, mode, att, phase);
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"SET ChipId={chipId}, Ch={channel}, {mode}, Att={att}, Phase={phase}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка установки параметров: {ex.Message}");
            throw;
        }
    }

    public async Task<string> EnableChannelAsync(byte chipId, int mode, int channel, bool[] enableChannels)
    {
        try
        {
            _logger.LogInfo($"Включение канала: ChipId={chipId}, Channel={channel}");
            
            var command = TRHJ1060CommandBuilder.EnableAllChannelsMask(chipId, enableChannels);
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"ENABLE ChipId={chipId}, Ch={channel}, Mode={mode}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка включения канала: {ex.Message}");
            throw;
        }
    }

    public async Task<string> DisableChannelAsync(byte chipId, int channel, bool[] enableChannels)
    {
        try
        {
            _logger.LogInfo($"Отключение канала: ChipId={chipId}, Channel={channel}");
            
            // Отключаем канал
            var command = TRHJ1060CommandBuilder.EnableAllChannelsMask(chipId, enableChannels);
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"DISABLE ChipId={chipId}, Ch={channel}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка отключения канала: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SetAllChannelsAsync(byte chipId, int mode, double att, double phase)
    {
        try
        {
            _logger.LogInfo($"Установка всех каналов: ChipId={chipId}, Att={att}dB, Phase={phase}°");
            
            // Отправляем команду для каждого канала (0-3)
            var responses = new List<string>();
            for (byte i = 0; i < 4; i++)
            {
                var command = TRHJ1060CommandBuilder.SetAmplitudePhase(chipId, i, mode, att, phase);
                var response = await _communicator.SendCommandAsync(command);
                responses.Add(response ?? $"OK (Ch{i})");
            }
            
            var combinedResponse = string.Join("; ", responses);
            _logger.LogCommand($"SET ALL CHANNELS ChipId={chipId}, Att={att}, Phase={phase}");
            _logger.LogResponse(combinedResponse);
            return combinedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка установки всех каналов: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SwitchOnAllChannelsAsync(byte chipId)
    {
        try
        {
            _logger.LogInfo($"Включение всех каналов: ChipId={chipId}");
            
            var enabled = new bool[] { true, true, true, true };
            var command = TRHJ1060CommandBuilder.EnableAllChannelsMask(chipId, enabled);
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"SWITCH ON ALL CHANNELS ChipId={chipId}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка включения всех каналов: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SwitchOffAllChannelsAsync(byte chipId)
    {
        try
        {
            _logger.LogInfo($"Отключение всех каналов: ChipId={chipId}");
            
            var enabled = new bool[] { false, false, false, false };
            var command = TRHJ1060CommandBuilder.EnableAllChannelsMask(chipId, enabled);
            var response = await _communicator.SendCommandAsync(command);
            _logger.LogCommand($"SWITCH OFF ALL CHANNELS ChipId={chipId}");
            _logger.LogResponse(response ?? "OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка отключения всех каналов: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SetAllChipsAsync(double att, double phase, int mode)
    {
        try
        {
            _logger.LogInfo($"Установка параметров для всех чипов: Att={att}dB, Phase={phase}°");
            
            var responses = new List<string>();
            for (byte chipId = 0; chipId < 16; chipId++)
            {
                for (byte channel = 0; channel < 4; channel++)
                {
                    var command = TRHJ1060CommandBuilder.SetAmplitudePhase(chipId, channel, mode, att, phase);
                    var response = await _communicator.SendCommandAsync(command);
                    responses.Add(response ?? $"OK (ChipId={chipId}, Ch={channel})");
                }
            }
            
            var combinedResponse = $"Обновлено 64 канала. Последний ответ: {responses.LastOrDefault() ?? "OK"}";
            _logger.LogCommand($"SET ALL CHIPS");
            _logger.LogResponse(combinedResponse);
            return combinedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка установки всех чипов: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SwitchOnAllChipsAsync()
    {
        try
        {
            _logger.LogInfo($"Включение всех чипов");
            
            var responses = new List<string>();
            for (byte chipId = 0; chipId < 16; chipId++)
            {
                var enabled = new bool[] { true, true, true, true };
                var command = TRHJ1060CommandBuilder.EnableAllChannelsMask(chipId, enabled);
                var response = await _communicator.SendCommandAsync(command);
                responses.Add(response ?? $"OK (ChipId={chipId})");
            }
            
            var combinedResponse = $"Включено 16 чипов. Последний ответ: {responses.LastOrDefault() ?? "OK"}";
            _logger.LogCommand($"SWITCH ON ALL CHIPS");
            _logger.LogResponse(combinedResponse);
            return combinedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка включения всех чипов: {ex.Message}");
            throw;
        }
    }

    public async Task<string> SwitchOffAllChipsAsync()
    {
        try
        {
            _logger.LogInfo($"Отключение всех чипов");
            
            var responses = new List<string>();
            for (byte chipId = 0; chipId < 16; chipId++)
            {
                var enabled = new bool[] { false, false, false, false };
                var command = TRHJ1060CommandBuilder.EnableAllChannelsMask(chipId, enabled);
                var response = await _communicator.SendCommandAsync(command);
                responses.Add(response ?? $"OK (ChipId={chipId})");
            }
            
            var combinedResponse = $"Отключено 16 чипов. Последний ответ: {responses.LastOrDefault() ?? "OK"}";
            _logger.LogCommand($"SWITCH OFF ALL CHIPS");
            _logger.LogResponse(combinedResponse);
            return combinedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка отключения всех чипов: {ex.Message}");
            throw;
        }
    }

    public async Task SendRawCommandAsync(string hexCommand)
    {
        try
        {
            _logger.LogCommand(hexCommand);
            var response = await _communicator.SendCommandAsync(hexCommand);
            _logger.LogResponse(response ?? "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка отправки команды: {ex.Message}");
            throw;
        }
    }

    public List<string> GetTelemetry()
    {
        var telemetry = new List<string>();
        return telemetry;
    }

    public void Exit() 
    {
        _serialPortManager.Dispose();
    }
}
