using CadentCable.Core;
using UnityEngine;
using UnityEngine.UI;

public sealed class FamicomReceiver : MonoBehaviour
{
    [SerializeField]
    private FamicomRelayConnection relay;
    [SerializeField]
    private RawImage qrCodeImage;

    private Texture2D qrCodeTexture;

    private async void Start()
    {
        if (relay == null)
        {
            Debug.LogError("FamicomRelayConnection is not assigned.");
            return;
        }

        relay.RoomClosed += OnRoomClosed;
        relay.Error += OnError;

        try
        {
            CreateRoomResult result =
                await relay.CreateRoomAndJoinAsync(
                    new CreateRoomOptions
                    {
                        RoomMode = RoomMode.Remote,
                        ApprovalRatio = 0,
                    });

            Debug.Log("Room ID: " + result.RoomId);

            string controllerUrl =
                "https://lab.takty.net/cc/remote/controller.html" +
                "?roomId=" +
                System.Uri.EscapeDataString(result.RoomId);

            Debug.Log("Controller URL: " + controllerUrl);

            ShowQrCode(controllerUrl);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void OnDestroy()
    {
        if (relay != null)
        {
            relay.RoomClosed -= OnRoomClosed;
            relay.Error -= OnError;
        }

        if (qrCodeTexture != null)
        {
            Destroy(qrCodeTexture);
        }
    }

    private void OnRoomClosed(RoomClosedEvent<FamicomControllerState> e)
    {
        Debug.Log("Room closed: " + e.Reason);
    }

    private void OnError(ErrorEvent<FamicomControllerState> e)
    {
        Debug.LogError(e.Code + ": " + e.Message);
    }

    private void ShowQrCode(string url)
    {
        if (qrCodeImage == null)
        {
            Debug.LogError("QR code image is not assigned.");
            return;
        }

        if (qrCodeTexture != null)
        {
            Destroy(qrCodeTexture);
        }

        qrCodeTexture = QrCodeGenerator.Generate(url);
        qrCodeImage.texture = qrCodeTexture;
        qrCodeImage.gameObject.SetActive(true);
    }
}
