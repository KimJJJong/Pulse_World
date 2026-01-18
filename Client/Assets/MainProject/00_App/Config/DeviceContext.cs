//using UnityEngine;

//namespace Client.Net.Auth
//{
//    public sealed class DeviceContext
//    {
//        const string KDeviceId = "client.deviceId";

//        public string DeviceId { get; }
//        public string ClientVersion { get; }

//        public DeviceContext(string clientVersion)
//        {
//            ClientVersion = clientVersion;

//            var id = PlayerPrefs.GetString(KDeviceId, "");
//            if (string.IsNullOrEmpty(id))
//            {
//                id = System.Guid.NewGuid().ToString("N");
//                PlayerPrefs.SetString(KDeviceId, id);
//                PlayerPrefs.Save();
//            }
//            DeviceId = id;
//        }
//    }
//}
