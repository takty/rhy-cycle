#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CadentCable.Abstractions;

namespace CadentCable.Transport.DotNet
{
    public sealed class DotNetTimer : ITimer
    {
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }
}
