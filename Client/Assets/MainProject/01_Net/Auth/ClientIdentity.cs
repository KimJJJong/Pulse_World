using UnityEngine;

public sealed class ClientIdentity
{
    const string DeviceIdKey = "client.deviceId";

    public string DeviceId { get; private set; }
    public string ClientVersion { get; }

    public ClientIdentity(string clientVersion)
    {
        ClientVersion = clientVersion;
        DeviceId = LoadOrCreateDeviceId();
    }

    static string LoadOrCreateDeviceId()
    {
        var did = PlayerPrefs.GetString(DeviceIdKey, "");
        if (string.IsNullOrEmpty(did))
        {
            did = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceIdKey, did);
            PlayerPrefs.Save();
        }
        return did;
    }

    public string ResetDeviceId()
    {
        var did = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(DeviceIdKey, did);
        PlayerPrefs.Save();
        DeviceId = did;
        return did;
    }
}
