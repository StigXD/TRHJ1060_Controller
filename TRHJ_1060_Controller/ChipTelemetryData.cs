using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

class ChipTelemetryData : INotifyPropertyChanged
{
    private ChipTypes _chipType;
    private string _temp = "None";

    private Dictionary<Channels, (string att, string phase, string outputPower)>[] _deviceTelemetry = new Dictionary<Channels, (string att, string phase, string outputPower)>[16];

    public event PropertyChangedEventHandler PropertyChanged;

    public ChipTelemetryData(ChipTypes chipType)
    {
        _chipType = chipType;

        for (var i = 0; i < 16; i++)
        {
            _deviceTelemetry[i] = new Dictionary<Channels, (string att, string phase, string outputPower)>
            {
                [Channels.channel0] = ("0.0", "0.0", "0.0"),
                [Channels.channel1] = ("0.0", "0.0", "0.0"),
                [Channels.channel2] = ("0.0", "0.0", "0.0"),
                [Channels.channel3] = ("0.0", "0.0", "0.0"),
                [Channels.channel4] = ("0.0", "0.0", "0.0"),
                [Channels.channel5] = ("0.0", "0.0", "0.0"),
                [Channels.channel6] = ("0.0", "0.0", "0.0"),
                [Channels.channel7] = ("0.0", "0.0", "0.0")
            };
        }
    }

    public ChipTypes DeviceType
    {
        get => _chipType;
    }

    public string Temp
    {
        get => _temp;
        set
        {
            if (_temp != value)
            {
                _temp = value;
                OnPropertyChanged();
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void RefreshTelemetry(List<string[]> allTelemetry, int id)
    {
        var countChannel = Channels.channel0;
        foreach (var channel in allTelemetry)
        {
            _deviceTelemetry[id][countChannel] = (channel[(int)countChannel], channel[(int)countChannel + 1], channel[(int)countChannel + 2]);
            countChannel++;
        }
    }

    public string[] GetTelemetryChipId(int idChip)
    {
        var telemetryData = _deviceTelemetry[idChip];
        var telemetryList = new List<string>();
        
        foreach (var kvp in telemetryData)
        {
            telemetryList.Add(kvp.Value.att);
            telemetryList.Add(kvp.Value.phase);
            telemetryList.Add(kvp.Value.outputPower);
        }
        
        return telemetryList.ToArray();
    }
}