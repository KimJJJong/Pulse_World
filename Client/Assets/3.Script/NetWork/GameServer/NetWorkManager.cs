using Client;
using ServerCore;
using System;
using System.Net;
using UnityEngine;

public class NetWorkManager : MonoBehaviour
{
    static NetWorkManager _instance;
    public static NetWorkManager Instance => _instance;

    [Header("Session State")]
    public string MatchId; // TODO: 로비에서 받은 값 채워넣기
    public string Uid;     // TODO: 로비에서 받은 값 채워넣기

    [Header("Join Cache")]
    string _ticket;
    int _protoVer;

    ServerSession _session = new ServerSession();
    Connector _connector = new Connector();

    void Awake()
    {
        if (_instance == null) { _instance = this; DontDestroyOnLoad(gameObject); }
        else if (_instance != this) Destroy(gameObject);
    }

    // WS : game.begin  응답 Fun
    public void ConnectAndJoin(string host, int port, string ticket, int protoVer)
    {
        MatchId = host;
        Uid = "port";

        _ticket = ticket;
        _protoVer = protoVer;

        IPAddress ip = IPAddress.TryParse(host, out var parsed) ? parsed : Dns.GetHostEntry(host).AddressList[0];
        IPEndPoint endPoint = new IPEndPoint(ip, port);

        _connector.Connect( endPoint,
                         () => {
                             _session.OnConnectedAction = () => MainThreadDispatcher.Post(SendJoin);
                             return _session; },
                         1);

    }

    public void SendJoin()
    {
        CS_Handshake join = new CS_Handshake
        {
            clientNonce = MatchId,    // netWork
            ticketId = _ticket,
            //uid = Uid,        //network
            //ticket = _ticket,    //network
            //protoVer = 1,//NetConfig.ProtoVer,  //local               
            //nonce = Guid.NewGuid().ToString("N"),
            //clientVer = Application.version,    //local
            //platform = Application.platform.ToString() //local
        };
        //Debug.Log("SendJoin");
        _session.Send(join.Write());
    }

    public void Send(ArraySegment<byte> sendBuff) => _session.Send(sendBuff);

    void Update()
    {
        var packet = PacketQueue.Instance.Pop();
        if (packet != null)
            PacketManager.Instance.HandlePacket(_session, packet);
    }
}