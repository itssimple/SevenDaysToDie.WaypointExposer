using AllocsFixes.JSON;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SevenDaysToDie
{
    public class WaypointExposer : IModApi, IConsoleServer
    {
        private readonly HttpListener _listener = new HttpListener();

        public WaypointExposer()
        {
            int controlPanelPort = GamePrefs.GetInt(EnumUtils.Parse<EnumGamePrefs>("ControlPanelPort", false));
            if (controlPanelPort < 1 || controlPanelPort > 65533)
            {
                Log.Out("Webserver not started (ControlPanelPort not within 1-65533)");
            }
            else
            {
                _listener.Prefixes.Add(string.Format("http://*:{0}/", controlPanelPort + 3));
                _listener.Start();

                _listener.BeginGetContext(new AsyncCallback(HandleWaypointRequest), _listener);

                Log.Out("Started webserver for waypoints on port {0}", controlPanelPort + 3);
            }
        }

        private void HandleWaypointRequest(IAsyncResult ar)
        {
            if (!_listener.IsListening)
            {
                return;
            }

            HttpListenerContext context = _listener.EndGetContext(ar);
            _listener.BeginGetContext(new AsyncCallback(HandleWaypointRequest), _listener);

            try
            {
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod == "OPTIONS")
                {
                    response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
                    response.AddHeader("Access-Control-Max-Age", "1728000");
                }
                response.AppendHeader("Access-Control-Allow-Origin", "*");

                if (GameManager.Instance.World == null)
                {
                    response.StatusCode = 503;

                    return;
                }

                JSONArray waypointArray = new JSONArray();

                foreach (var wp in WaypointContainer.Instance.Waypoints)
                {
                    var pos = new JSONObject();

                    pos.Add("x", new JSONNumber(wp.WaypointPosition.x));
                    pos.Add("y", new JSONNumber(wp.WaypointPosition.y));
                    pos.Add("z", new JSONNumber(wp.WaypointPosition.z));

                    var point = new JSONObject();

                    point.Add("id", new JSONString(wp.Id.ToString()));
                    point.Add("name", new JSONString(wp.Name));
                    point.Add("ownerPlayerId", new JSONString(wp.OwnerPlayerId.ToString()));
                    point.Add("pos", pos);

                    waypointArray.Add(point);
                }

                StringBuilder stringBuilder = new StringBuilder();
                waypointArray.ToString(stringBuilder, false, 0);

                var bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());

                response.ContentLength64 = bytes.LongLength;
                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            } finally
            {
                if (context != null && !context.Response.SendChunked)
                {
                    context.Response.Close();
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                Log.Out("Caught exception when disconnecting: {0}", ex.ToString());
            }
        }

        public void InitMod()
        {
            ModEvents.SavePlayerData.RegisterHandler(
                (clientInfo, playerDataFile) =>
                {
                    WaypointContainer.Instance.SaveWaypoints(playerDataFile.id, playerDataFile.waypoints.List);
                }
            );
        }

        public void SendLine(string _line)
        {
        }

        public void SendLog(string _msg, string _trace, UnityEngine.LogType _type)
        {
        }
    }

    public class WaypointContainer
    {
        private static WaypointContainer instance;

        public List<MapWaypoint> Waypoints { get; set; } = new List<MapWaypoint>();

        public static WaypointContainer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WaypointContainer();
                }
                return instance;
            }
        }

        internal void SaveWaypoints(int playerId, List<Waypoint> waypoints)
        {
            Waypoints.RemoveAll(wp => wp.OwnerPlayerId == playerId);

            foreach (var waypoint in waypoints)
            {
                Waypoints.Add(new MapWaypoint
                {
                    Id = waypoint.entityId,
                    Name = waypoint.name,
                    WaypointPosition = waypoint.pos,
                    OwnerPlayerId = playerId
                });
            }
        }
    }

    public class MapWaypoint
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int OwnerPlayerId { get; set; }
        public Vector3i WaypointPosition { get; set; }
    }
}
