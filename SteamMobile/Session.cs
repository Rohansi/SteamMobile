﻿using System.Collections.Generic;
using System.Linq;

namespace SteamMobile
{
    public class Session
    {
        public Account Account { get; private set; }
        public bool IsActive { get; private set; }

        private readonly object _sync = new object();
        private List<Connection> _connections;
        private OrderedSet<string> _rooms;
        private bool _firstConnection;
        private float _timeWithoutConnection;
        private bool _isMobile;

        public Session(Account account)
        {
            Account = account;
            IsActive = true;

            _connections = new List<Connection>();

            var defaultRoom = Program.Settings.DefaultRoom;
            var roomsList = new List<string>(Account.Rooms ?? new string[0]);

            roomsList.RemoveAll(r => Program.RoomManager.Get(r) == null);

            var defaultIdx = roomsList.IndexOf(defaultRoom);
            if (roomsList.Count == 0)
            {
                roomsList.Add(defaultRoom);
            }
            else if (defaultIdx == -1)
            {
                roomsList.Insert(0, defaultRoom);
            }
            else if (defaultIdx > 0)
            {
                roomsList.RemoveAt(defaultIdx);
                roomsList.Insert(0, defaultRoom);
            }

            if (!roomsList.SequenceEqual(Account.Rooms ?? new string[0]))
            {
                Account.Rooms = roomsList.ToArray();
                Account.Save();
            }

            _rooms = new OrderedSet<string>(Account.Rooms);
            _firstConnection = true;
            _timeWithoutConnection = 0;
        }

        public bool IsInRoom(string roomName)
        {
            lock (_sync)
            {
                roomName = (roomName ?? "").ToLower();
                return _rooms.Contains(roomName);
            }
        }

        public void Add(Connection connection)
        {
            List<string> rooms;

            lock (_sync)
            {
                if (_connections.Contains(connection))
                    return;

                _connections.Add(connection);

                connection.Session = this;
                _firstConnection = false;
                rooms = _rooms.ToList();
            }

            foreach (var roomName in rooms)
            {
                var room = Program.RoomManager.Get(roomName);
                connection.SendJoinRoom(room);

                if (_firstConnection)
                    room.SessionEnter(this);
            }
        }

        public void Remove(Connection connection)
        {
            lock (_sync)
            {
                _connections.Remove(connection);
            }
        }

        public void Update(float delta)
        {
            lock (_sync)
            {
                _connections.RemoveAll(conn => !conn.Connected);

                if (_connections.Count > 0)
                {
                    _isMobile = _connections.Any(c => c.IsMobile);
                    _timeWithoutConnection = 0;
                    return;
                }

                float timeout = _isMobile ? 2.5f : 0.5f;
                
                _timeWithoutConnection += delta;
                IsActive = _timeWithoutConnection < (timeout * 60);
            }
        }

        public bool Join(string roomName)
        {
            roomName = (roomName ?? "").ToLower();

            if (_rooms.Contains(roomName))
                return true;

            var room = Program.RoomManager.Get(roomName);
            if (room == null)
                return false;

            lock (_sync)
            {
                _rooms.Add(room.RoomInfo.ShortName);
                room.SessionEnter(this);

                foreach (var conn in _connections)
                {
                    conn.SendJoinRoom(room);
                }

                Account.Rooms = _rooms.ToArray();
                Account.Save();
            }

            return true;
        }

        public bool Leave(string roomName)
        {
            roomName = (roomName ?? "").ToLower();

            if (roomName == Program.Settings.DefaultRoom)
                return true;

            var room = Program.RoomManager.Get(roomName);
            if (room == null)
                return true;

            lock (_sync)
            {
                if (!_rooms.Contains(roomName))
                    return true;

                room.SessionLeft(this);
                _rooms.Remove(roomName);

                foreach (var conn in _connections)
                {
                    conn.SendLeaveRoom(room);
                }
            
                Account.Rooms = _rooms.ToArray();
                Account.Save();
            }

            return true;
        }

        public void Send(Packet packet)
        {
            var packetStr = Packet.WriteToMessage(packet);
            Send(packetStr);
        }

        public void Send(string data)
        {
            lock (_sync)
            {
                foreach (var conn in _connections)
                {
                    conn.Send(data);
                }
            }
        }
    }
}
