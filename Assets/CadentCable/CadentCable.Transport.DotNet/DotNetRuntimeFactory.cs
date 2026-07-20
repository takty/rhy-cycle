#nullable enable

using CadentCable.Abstractions;

namespace CadentCable.Transport.DotNet
{
    public static class DotNetRuntimeFactory
    {
        public static CCRuntime Create()
        {
            return new CCRuntime(
                new DotNetClock(),
                new DotNetTimer(),
                new DotNetWebSocketFactory(),
                new DotNetHttpTransport(),
                ownsComponents: true);
        }
    }
}
