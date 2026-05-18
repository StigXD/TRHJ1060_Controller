using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TRHJ_1060_Controller;

public class ChipParameters
{
    public ChipTypes ChipTypes;
    public int ChipId { get; set; }
    public string ChipMode { get; set; } = string.Empty;
    public Dictionary<Channels, (string att, string phase)> ChannelParameters { get; set; } = new Dictionary<Channels, (string att, string phase)>();
}
