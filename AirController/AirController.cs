﻿using UnityEngine;
using System.Collections;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;

namespace SwordGC.AirController
{
    public class AirController : MonoBehaviour
    {
        /// <summary>
        /// Because we really do want one instance
        /// </summary>
        public static AirController Instance;

        /// <summary>
        /// TOGETHER: All devices get marked hero.
        /// SEPERATE: Only the hero device will be marked hero.
        /// </summary>
        public enum HEROMODE { TOGETHER, SEPERATE }
        public HEROMODE heroMode = HEROMODE.TOGETHER;

        /// <summary>
        /// AUTO: A device will automatically try to claim a player
        /// CUSTOM: A device won't claim a player (for a join now screen?). 
        /// 
        /// Note: When set to custom you can easily claim a player by sending a button called "claim" from the device
        /// </summary>
        public enum JOINMODE { AUTO, CUSTOM }
        public JOINMODE joinMode = JOINMODE.AUTO;

        /// <summary>
        /// AUTO: The system will create a player for each device.
        /// LIMITED: The system will create the specified amount of players and will only allow that many
        /// </summary>
        public enum MAXPLAYERSMODE { AUTO, LIMITED }
        public MAXPLAYERSMODE maxPlayersMode = MAXPLAYERSMODE.AUTO;

        /// <summary>
        /// Becomes true when the AirConsole plugin is ready
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Becomes true when OnPremium is called
        /// </summary>
        public bool HasHero { get; private set; }

        /// <summary>
        /// The maximum amount of players when maxPlayersMode is set to LIMITED
        /// </summary>
        public int maxPlayers = 4;

        /// <summary>
        /// Turns all debugs on and off
        /// </summary>
        public bool debug = true;
        
        /// <summary>
        /// Contains all players
        /// </summary>
        public Dictionary<int, Player> Players { get; protected set; }

        /// <summary>
        /// The amount of players that are created but not claimed
        /// </summary>
        public int PlayersAvailable
        {
            get
            {
                int i = 0;
                foreach (Player p in Players.Values)
                {
                    if (p.state != Player.STATE.CLAIMED) i++;
                }
                return i;
            }
        }

        /// <summary>
        /// Contains all devices
        /// </summary>
        public Dictionary<int, Device> Devices { get; protected set; }

        public AirController ()
        {
            Players = new Dictionary<int, Player>();
            Devices = new Dictionary<int, Device>();
        }

        #region UNITY_CALLBACKS
        void OnEnable()
        {
            AirConsole.instance.onReady += OnReady;
            AirConsole.instance.onMessage += OnMessage;
            AirConsole.instance.onConnect += OnConnect;
            AirConsole.instance.onDisconnect += OnDisconnect;
            AirConsole.instance.onDeviceStateChange += OnDeviceStateChange;
            AirConsole.instance.onCustomDeviceStateChange += OnCustomDeviceStateChange;
            AirConsole.instance.onDeviceProfileChange += OnDeviceProfileChange;
            AirConsole.instance.onAdShow += OnAdShow;
            AirConsole.instance.onAdComplete += OnAdComplete;
            AirConsole.instance.onGameEnd += OnGameEnd;
            AirConsole.instance.onPremium += OnPremium;
        }

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(gameObject);

            ResetPlayers();
        }

        void LateUpdate()
        {
            ResetInput();
        }
        #endregion

        #region AIRCONSOLE_CALLBACKS
        protected virtual void OnReady(string code)
        {
            IsReady = true;
        }

        protected virtual void OnMessage(int from, JToken data)
        {
            GetDevice(from).Input.Process(data);

            if (joinMode == JOINMODE.CUSTOM && GetDevice(from).Input.GetKeyDown("claim"))
            {
                ClaimPlayer(from);
                UpdateDeviceStates();
            }
        }

        protected virtual void OnConnect(int deviceId)
        {
            InternalDebug("Device: " + deviceId + " connected. " + AirConsole.instance.GetNickname(deviceId));

            ConnectDevice(deviceId);
        }

        protected virtual void OnDisconnect(int deviceId)
        {
            InternalDebug("Device: " + deviceId + " disconnected.");

            DisconnectDevice(deviceId);
        }

        protected virtual void OnDeviceStateChange(int deviceId, JToken data)
        {
            InternalDebug("Device State Change on device: " + deviceId + ", data: " + data);
        }

        protected virtual void OnCustomDeviceStateChange(int deviceId, JToken custom_data)
        {
            InternalDebug("Custom Device State Change on device: " + deviceId + ", data: " + custom_data);
        }

        protected virtual void OnDeviceProfileChange(int deviceId)
        {
            InternalDebug("Device " + deviceId + " made changes to its profile.");
        }

        protected virtual void OnAdShow()
        {
            InternalDebug("On Ad Show");
        }

        protected virtual void OnAdComplete(bool adWasShown)
        {
            InternalDebug("Ad Complete. Ad was shown: " + adWasShown + "");
        }

        protected virtual void OnGameEnd()
        {
            InternalDebug("OnGameEnd is called");
        }

        protected virtual void OnPremium(int deviceId)
        {
            InternalDebug("OnPremium: " + deviceId);

            HasHero = true;
            GetDevice(deviceId).IsHero = true;
            UpdateDeviceStates();
        }
        #endregion

        #region PLAYER_FUNCTIONS
        /// <summary>
        /// Called when a new player is needed, override this function to insert your own Player extended class
        /// </summary>
        public virtual Player GetNewPlayer (int playerId)
        {
            return new Player(playerId);
        }

        /// <summary>
        /// Returns a player object
        /// </summary>
        public Player GetPlayer(int playerId)
        {
            return Players.ContainsKey(playerId) ? Players[playerId] : null;
        }

        /// <summary>
        /// Creates and adds a new player
        /// </summary>
        private Player CreatePlayer (int playerId)
        {
            Player p = GetNewPlayer(playerId);
            Players.Add(playerId, p);
            return p;
        }

        /// <summary>
        /// Clears all players
        /// </summary>
        public void ResetPlayers()
        {
            Players = new Dictionary<int, Player>();

            if (maxPlayersMode == MAXPLAYERSMODE.LIMITED)
            {
                // populate players
                for (int i = 0; i < maxPlayers; i++)
                {
                    CreatePlayer(i);
                }
            }
        }

        /// <summary>
        /// Claims a Player for a device
        /// </summary>
        public void ClaimPlayer(int deviceId)
        {
            // trying to find a player to claim
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].state == Player.STATE.UNCLAIMED)
                {
                    Players[i].Claim(deviceId);

                    InternalDebug("Device " + deviceId + " claimed a player");
                    return;
                }
            }

            if (maxPlayersMode == MAXPLAYERSMODE.AUTO)
            {
                // no player found, new one
                CreatePlayer(Players.Count).Claim(deviceId);

                InternalDebug("Device " + deviceId + " claimed a new player");
                return;
            }
            else
            {
                InternalDebug("Device " + deviceId + " vailed to claim a player");
            }
        }

        /// <summary>
        /// Tries to reconnect a Player to a device
        /// </summary>
        public bool ReconnectWithPlayer (int deviceId)
        {
            // trying to find a player to reconnect
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].state == Player.STATE.DISCONNECTED && Players[i].DeviceId == deviceId)
                {
                    Players[i].Claim(deviceId);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region DEVICE_FUNCTIONS
        /// <summary>
        /// Safe function to get a device
        /// </summary>
        public Device GetDevice (int deviceId)
        {
            return Devices.ContainsKey(deviceId) ? Devices[deviceId] : null;
        }
    
        /// <summary>
        /// Creates a device
        /// </summary>
        private void CreateDevice(int deviceId)
        {
            if (!Devices.ContainsKey(deviceId)) Devices.Add(deviceId, GetNewDevice(deviceId));
            UpdateDeviceStates();
        }

        /// <summary>
        /// Called when a new device is needed, override this function to insert your own Device extended class
        /// </summary>
        protected virtual Device GetNewDevice (int deviceId)
        {
            return new Device(deviceId);
        }
        
        /// <summary>
        /// Checks if a device has a player object
        /// </summary>
        public bool DeviceHasPlayer(int deviceId)
        {
            foreach (Player p in Players.Values)
            {
                if (p.DeviceId == deviceId) return true;
            }
            return false;
        }

        /// <summary>
        /// Return the player based on a deviceId
        /// </summary>
        public Player GetPlayerFromDevice(int deviceId)
        {
            foreach (Player p in Players.Values)
            {
                if (p.DeviceId == deviceId) return p;
            }
            return null;
        }


        /// <summary>
        /// (re)connects a device
        /// </summary>
        private void ConnectDevice (int deviceId)
        {
            if (ReconnectWithPlayer(deviceId))
            {
                InternalDebug("Reconnected " + deviceId + " with player");
            }
            else if (joinMode == JOINMODE.AUTO)
            {
                ClaimPlayer(deviceId);
            }

            // create device
            CreateDevice(deviceId);
        }

        /// <summary>
        /// Disconnects a device
        /// </summary>
        private void DisconnectDevice(int deviceId)
        {
            if (Devices.ContainsKey(deviceId)) {
                if (DeviceHasPlayer(deviceId) && GetDevice(deviceId).Player.state == Player.STATE.CLAIMED)
                {
                    GetDevice(deviceId).Player.Disconnect();
                }

                Devices.Remove(deviceId);
                UpdateDeviceStates();
            }
        }

        /// <summary>
        /// Sends the current device states to the devices
        /// </summary>
        public void UpdateDeviceStates()
        {
            InternalDebug("UpdateDeviceStates");

            StartCoroutine(SendDeviceStates());
        }

        /// <summary>
        /// The actual state sender
        /// </summary>
        private IEnumerator SendDeviceStates()
        {
            while (!IsReady)
            {
                yield return new WaitForEndOfFrame();
            }

            JSONObject j = new JSONObject(JSONObject.Type.OBJECT);

            foreach (int i in Devices.Keys)
            {
                j.AddField(i.ToString(), Devices[i].GetJson());
            }
            AirConsole.instance.SetCustomDeviceState(j.Print());
        }
        #endregion

        /// <summary>
        /// Resets the input, is called on LateUpdate
        /// </summary>
        public void ResetInput()
        {
            foreach (Device d in Devices.Values)
            {
                d.Input.Reset();
            }
        }

        /// <summary>
        /// Wrapper function for easy turning on and off debugging
        /// </summary>
        protected void InternalDebug (object obj)
        {
            if(debug) Debug.Log("AirController: " + obj.ToString());
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("GameObject/Create Other/AirController")]
        static void MenuCreator ()
        {
            AirController AC = new GameObject("AirController").AddComponent<AirController>();

            // Register the creation in the undo system
            UnityEditor.Undo.RegisterCreatedObjectUndo(AC.gameObject, "Create " + AC.gameObject.name);
            UnityEditor.Selection.activeObject = AC.gameObject;
        }
#endif
    }
}