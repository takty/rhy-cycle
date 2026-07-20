#nullable enable

using System.Diagnostics;
using CadentCable.Abstractions;

namespace CadentCable.Transport.DotNet
{
    public sealed class DotNetClock : IClock
    {
        public double Now()
        {
            return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
        }
    }
}
