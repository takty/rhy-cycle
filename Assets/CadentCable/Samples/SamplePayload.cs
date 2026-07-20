#nullable enable

using System;

namespace CadentCable.Samples
{
    [Serializable]
    public sealed class SamplePayload
    {
        public string Action { get; set; } = string.Empty;
        public bool Pressed { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
