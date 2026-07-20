#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CadentCable.Abstractions;

namespace CadentCable.Core
{
    public static class CadentCableClient
    {
        public static async Task<CreateRoomResult> CreateRoomAsync(
            string serverUrl,
            CreateRoomOptions? options,
            CCRuntime runtime,
            IProtocolSerializer protocolSerializer,
            CancellationToken cancellationToken = default)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (protocolSerializer == null)
            {
                throw new ArgumentNullException(nameof(protocolSerializer));
            }

            CreateRoomOptions normalized = NormalizeOptions(options);
            Uri endpoint = UrlUtility.JoinUrl(serverUrl, Routes.Rooms);
            string requestJson = protocolSerializer.SerializeCreateRoomOptions(normalized);

            CCHttpResponse response;
            try
            {
                response = await runtime.Http
                    .PostJsonAsync(endpoint, requestJson, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CadentCableRequestException(
                    "Room creation request failed.",
                    innerException: ex);
            }

            ProtocolCreateRoomResponse parsed;
            try
            {
                parsed = protocolSerializer.DeserializeCreateRoomResponse(response.Body);
            }
            catch (Exception ex)
            {
                throw new CadentCableRequestException(
                    "Room creation response was not a valid Cadent Cable response.",
                    response.StatusCode,
                    innerException: ex);
            }

            if (!response.IsSuccessStatusCode || !parsed.Ok || parsed.Result == null)
            {
                string errorCode = parsed.Error ?? "http_error";
                throw new CadentCableRequestException(
                    "Room creation failed: " + errorCode,
                    response.StatusCode,
                    errorCode);
            }

            return parsed.Result;
        }

        private static CreateRoomOptions NormalizeOptions(CreateRoomOptions? options)
        {
            options = options ?? new CreateRoomOptions();

            double ratio = options.ApprovalRatio;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                ratio = 0.0;
            }
            else
            {
                ratio = Math.Max(0.0, Math.Min(ratio, 1.0));
            }

            return new CreateRoomOptions
            {
                RoomId = string.IsNullOrWhiteSpace(options.RoomId)
                    ? null
                    : options.RoomId.Trim().ToUpperInvariant(),
                RoomMode = options.RoomMode,
                ApprovalRatio = ratio,
            };
        }
    }
}
