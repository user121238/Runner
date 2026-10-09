using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Runner.Business.Services.Ble;

public class WatchAuthService
{
    private readonly Action<string> _logAction;
    private readonly Func<byte[], Task<byte[]?>> _sendCommand;

    public WatchAuthService(Action<string> logAction, Func<byte[], Task<byte[]?>> sendCommand)
    {
        _logAction = logAction;
        _sendCommand = sendCommand;
    }

    public async Task<bool> AuthenticateAsync(byte[] preSharedKey)
    {
        if (preSharedKey.Length != 16)
        {
            Log($"Invalid key length: {preSharedKey.Length}, expected 16");
            return false;
        }

        var privKey = DeviceEncryptInterop.GeneratePrivateKey();
        Log($"Generated private key ({privKey.Length} bytes)");

        var pubKey = DeviceEncryptInterop.GeneratePublicKey(privKey);
        if (pubKey == null || pubKey.Length != 48)
        {
            Log("Failed to generate public key");
            return false;
        }
        Log($"Generated public key ({pubKey.Length} bytes)");

        var response = await _sendCommand(BuildRequestAuth(pubKey));
        if (response == null || response.Length < 64)
        {
            Log($"RequestAuth failed or invalid response (len={response?.Length})");
            return false;
        }

        // FDAB response: [16-byte random] [48-byte public key] (no status byte)
        // FEE0 response: [status=1] [16-byte random] [48-byte public key]
        int offset = (response.Length == 65) ? 1 : 0;

        var random = new byte[16];
        Array.Copy(response, offset, random, 0, 16);
        var watchPubKey = new byte[48];
        Array.Copy(response, offset + 16, watchPubKey, 0, 48);
        Log($"Got challenge: random({random.Length}) watchPubKey({watchPubKey.Length})");

        var sharedSecret = DeviceEncryptInterop.GenerateSharedSecret(privKey, watchPubKey);
        if (sharedSecret == null)
        {
            Log("Failed to compute shared secret");
            return false;
        }
        Log($"Shared secret ({sharedSecret.Length} bytes)");

        var xorResult = new byte[16];
        for (int i = 0; i < 16; i++)
            xorResult[i] = (byte)(sharedSecret[8 + i] ^ preSharedKey[i]);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = preSharedKey;

        var block1 = aes.EncryptEcb(random, PaddingMode.None);

        aes.Key = xorResult;
        var block2 = aes.EncryptEcb(random, PaddingMode.None);

        var signature = new byte[32];
        Array.Copy(block1, 0, signature, 0, 16);
        Array.Copy(block2, 0, signature, 16, 16);
        Log($"Signature ({signature.Length} bytes)");

        var verifyResponse = await _sendCommand(BuildVerifySignature(signature));
        if (verifyResponse == null || verifyResponse.Length < 1)
        {
            Log("VerifySignature failed or no response");
            return false;
        }

        var authStatus = verifyResponse[0];
        if (authStatus != 1)
        {
            Log($"VerifySignature rejected: status={authStatus}");
            return false;
        }

        Log("Authentication successful!");
        return true;
    }

    private static byte[] BuildRequestAuth(byte[] pubKey)
    {
        // FEE0 format: 0x04 + length(2) + type(1) + pubkey(48)
        var cmd = new byte[1 + 2 + 1 + 48];
        cmd[0] = 0x04;
        cmd[1] = 0x02; cmd[2] = 0x00;
        cmd[3] = 0x02;
        Array.Copy(pubKey, 0, cmd, 4, 48);
        return cmd;
    }

    private static byte[] BuildVerifySignature(byte[] signature)
    {
        // FDAB protocol: just send the 32-byte signature directly
        return signature;
    }

    private void Log(string message)
    {
        _logAction($"[WatchAuth] {message}");
    }
}
