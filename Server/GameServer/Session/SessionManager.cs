using ServerCore;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

    class SessionManager
    {
        static SessionManager _session = new SessionManager();
        public static SessionManager Instance { get { return _session; } }

        int _sessionId = 0;
        Dictionary<int, PacketSession> _sessions = new Dictionary<int, PacketSession>();
    
        object _lock = new object();

        public T Generate<T>() where T : PacketSession, new()
        {
            lock (_lock)
            {
                int sessionId = ++_sessionId;

                T session = new T();
                session.SessionID = sessionId;

                _sessions.Add(sessionId, session);
            Console.WriteLine($"[SessionManager] Connected: {sessionId} ({typeof(T).Name})");

            return session;

            }
        }


        public PacketSession SessionFind(int id)
        {
            lock (_lock)
            {
            PacketSession session = null;
                _sessions.TryGetValue(id, out session);
                return session;
            }
        }
    public int Count => _sessions.Count;
    public IEnumerable<PacketSession> All()=> _sessions.Values.ToArray();
    // 룸 단위 Session 탐색 : 여기서 이친구 사용할 일을 없는게 좋을듯? : 외부에서 룸을 찾을 일?? 
    public IEnumerable<PacketSession> ByMatch(string matchId)
    => string.IsNullOrEmpty(matchId)
       ? Array.Empty<PacketSession>()
       : _sessions.Values.Where(s => s.MatchId == matchId).ToArray();

    public void Remove(PacketSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session.SessionID);
            }
        }
    }