#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CadentCable.Abstractions;

namespace CadentCable.Transport.DotNet
{
    public sealed class DotNetHttpTransport : ICHttpTransport, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private bool _disposed;

        public DotNetHttpTransport()
            : this(new HttpClient(), ownsHttpClient: true)
        {
        }

        public DotNetHttpTransport(HttpClient httpClient, bool ownsHttpClient = false)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = ownsHttpClient;
        }

        public async Task<CCHttpResponse> PostJsonAsync(
            Uri uri,
            string json,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _httpClient
                .PostAsync(uri, content, cancellationToken)
                .ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new CCHttpResponse((int)response.StatusCode, body);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DotNetHttpTransport));
            }
        }
    }
}
