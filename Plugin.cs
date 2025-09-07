using BepInEx;
using GorillaNetworking;
using GorillaNetworking.Store;
using GorillaTagScripts;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using static DestinyBannedServers.HarmonyPatches;

namespace DestinyBannedServers
{
    [BepInPlugin(_pluginGuid, _pluginName, _pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        /*
          
              _____            _               
             |  __ \          | |              
             | |  | | ___  ___| |_ _ __  _   _ 
             | |  | |/ _ \/ __| __| '_ \| | | |
             | |__| |  __/\__ \ |_| | | | |_| |
             |_____/ \___||___/\__|_| |_|\__, |
                                          __/ |
                                         |___/  

        Welcome to the Destiny Banned Server Source Code

        Thank you for exploring our project! You are free to use,
        modify, and distribute this source code under one condition: 
        please provide credit to 'The Destiny Team' in your project.
        We appreciate your support.

        - Moo (serializeviewbatch on discord)
                 
        */

        public static Harmony _Instance;
        public static string _titlePlayerId;
        public static bool _connected;
        public static bool _acceptedTOS;
        public const string _pluginGuid = "com.moo.destiny";
        public const string _pluginName = "Destiny Banned Servers";
        public const string _pluginVersion = "1.0";
        public const string _playfabTitleId = "F6FD8";
        public const string _photonAppIdRealtime = "70eb3ada-9c53-40f8-ba6a-1e27ba1fa69c";
        public const string _photonAppIdVoice = "f1d55e21-f77c-4c29-9340-65653545a8fd";
        public const string _photonAppVersion = "moo";
        public const string _photonRegion = "eu";
        void Start()
        {
            if (_Instance == null)
                _Instance = new Harmony(_pluginGuid);
            _Instance.PatchAll(Assembly.GetExecutingAssembly());
            if (PlayFabSettings.TitleId != _playfabTitleId && !_connected)
            {
                ConnectToServers();
                _connected = true;
            }
        }
        void Update()
        {
            if (PlayFabSettings.TitleId == _playfabTitleId && PlayFabClientAPI.IsClientLoggedIn())
            {
                GameObject _roomObject = GameObject.Find("Miscellaneous Scripts").transform.Find("PrivateUIRoom_HandRays").gameObject;
                if (_roomObject == null)
                    return;
                HandRayController _hrc = _roomObject.GetComponent<HandRayController>();
                PrivateUIRoom _pur = _roomObject.GetComponent<PrivateUIRoom>();
                if (!_acceptedTOS && _pur.inOverlay)
                {
                    _hrc.DisableHandRays();
                    _pur.overlayForcedActive = false;
                    PrivateUIRoom.StopOverlay();
                    _roomObject?.SetActive(false);
                    if (!TOSPatch.enabled)
                    {
                        GorillaTagger.Instance.tapHapticStrength = 0.5f;
                        GorillaSnapTurn.LoadSettingsFromCache();
                        TOSPatch.enabled = true;
                    }
                    _acceptedTOS = true;
                }
            }
        }
        public static void ConnectToServers()
        {
            PhotonNetworkController.Instance.disableAFKKick = true;
            PlayFabSettings.TitleId = _playfabTitleId;
            PlayFabSettings.DisableFocusTimeCollection = true;
            PlayFabSettings.CompressApiData = false;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = _photonAppIdRealtime;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = _photonAppIdVoice;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = _photonAppVersion;
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = _photonRegion;
            PhotonNetwork.ConnectUsingSettings();
            var _request = new LoginWithCustomIDRequest
            {
                CustomId = GetOrCreateDeviceId(),
                CreateAccount = true,
                TitleId = _playfabTitleId
            };
            PlayFabClientAPI.LoginWithCustomID(_request, OnLoginSuccess, OnLoginFailure);
        }
        private static string GetOrCreateDeviceId()
        {
            string _deviceId = SystemInfo.deviceUniqueIdentifier;
            if (!PlayerPrefs.HasKey(_deviceId))
            {
                PlayerPrefs.SetString(_deviceId, _deviceId);
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetString(_deviceId);
        }
        private static void OnLoginSuccess(LoginResult _result)
        {
            _titlePlayerId = _result.EntityToken?.Entity?.Id;
            Debug.Log($"[Moos Banned Servers] Login successful. Entity ID: {_titlePlayerId}");
            NetworkSystem.Instance?.SetAuthenticationValues(null);
            PhotonNetwork.ConnectUsingSettings();
            StoreUpdater.instance?.Initialize();
            CosmeticsController.instance?.Initialize();
        }

        private static void OnLoginFailure(PlayFabError _error)
        {
            if (_error.Error == PlayFabErrorCode.AccountNotFound && !PlayerPrefs.HasKey("createdOnce"))
            {
                PlayerPrefs.SetInt("createdOnce", 1);
                PlayerPrefs.Save();

                var _request = new RegisterPlayFabUserRequest
                {
                    Username = $"User_{UnityEngine.Random.Range(1000, 9999)}",
                    Password = Guid.NewGuid().ToString(),
                    TitleId = _playfabTitleId
                };

                PlayFabClientAPI.RegisterPlayFabUser(_request, _result => {}, _error => {});
            }
        }
    }

    internal static class HarmonyPatches
    {
        [HarmonyPatch(typeof(GorillaComputer), "CheckAutoBanListForPlayerName")]
        private static class CheckAutoBanListForPlayerNamePatch
        {
            private static bool Prefix(GorillaComputer __instance, string nameToCheck)
            {
                if (__instance == null)
                    return false;

                NetworkSystem.Instance?.SetMyNickName(__instance.currentName);
                __instance.savedName = __instance.currentName;
                PlayerPrefs.SetString("playerName", __instance.currentName);
                PlayerPrefs.Save();

                if (NetworkSystem.Instance?.InRoom == true)
                {
                    var _rig = GorillaTagger.Instance?.myVRRig;
                    if (_rig != null)
                    {
                        try
                        {
                            _rig.SendRPC("InitializeNoobMaterial", RpcTarget.All, new object[]
                            {
                                __instance.redValue,
                                __instance.greenValue,
                                __instance.blueValue
                            });
                        }
                        catch { }
                    }
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(GorillaLevelScreen), "UpdateText")]
        private static class GorillaLevelScreenPatch
        {
            private static bool Prefix(GorillaLevelScreen __instance, string newText, bool setToGoodMaterial)
            {
                return newText != null && __instance.TryGetComponent(out MeshRenderer _);
            }
        }
        [HarmonyPatch(typeof(GorillaComputer), "StartupScreen")]
        private static class StartupScreenPatch
        {
            private static bool Prefix(GorillaComputer __instance)
            {
                __instance.screenText.Text = "DESTINY OS\n\nWelcome to Destiny Unbanned Servers!\nThis does NOT unban you — it just connects you to our servers.\n\nPRESS ANY KEY TO BEGIN";
                return false;
            }
        }
        [HarmonyPatch(typeof(GorillaComputer), "CheckAutoBanListForRoomName")]
        private static class CheckAutoBanListForRoomNamePatch
        {
            private static bool Prefix(GorillaComputer __instance, string nameToCheck)
            {
                if (__instance == null) return false;

                int _group = FriendshipGroupDetection.Instance?.IsInParty == true ? 2 : 0;
                PhotonNetworkController.Instance?.AttemptToJoinSpecificRoom(__instance.roomToJoin, (GorillaNetworking.JoinType)_group);
                return false;
            }
        }
        [HarmonyPatch(typeof(PlayFabAuthenticator), "OnPlayFabError")]
        private static class PlayFabErrorPatch
        {
            private static bool Prefix(PlayFabError obj)
            {
                Plugin.ConnectToServers();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayFabAuthenticator), "DisplayGeneralFailureMessageOnGorillaComputerAfter1Frame")]
        private static class PatchSkipSteamRequirement
        {
            private static bool Prefix()
            {
                Plugin.ConnectToServers();
                return false;
            }
        }
        [HarmonyPatch(typeof(PlayFabAuthenticator), "AuthenticateWithPlayFab")]
        private static class SkipPlayFabAuthPatch
        {
            private static bool Prefix()
            {
                return false;
            }
        }
        [HarmonyPatch(typeof(VRRig), "IsItemAllowed")]
        private static class AllowedPatch
        {
            private static bool _force = false;
            private static List<string> _ownedItems = new List<string>();
            private static bool _hasFetchedInventory = false;

            private static void Postfix(VRRig __instance, ref bool __result, string itemName)
            {
                if (!_hasFetchedInventory)
                {
                    PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), result =>
                    {
                        _ownedItems.Clear();
                        foreach (var item in result.Inventory)
                            _ownedItems.Add(item.ItemId);

                        _hasFetchedInventory = true;
                    },
                    error => {});
                }

                if (__instance.isOfflineVRRig || _force || _ownedItems.Contains(itemName))
                {
                    __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(ModIOManager), "OnJoinedRoom")]
        private static class RoomJoinPatch
        {
            private static bool Prefix()
            {
                return false;
            }
        }
        [HarmonyPatch(typeof(LegalAgreements), "Update")]
        public class TOSPatch
        {
            public static bool enabled;
            private static bool Prefix(LegalAgreements __instance)
            {
                if (enabled)
                {
                    ControllerInputPoller.instance.leftControllerPrimary2DAxis.y = -1f;
                    __instance.scrollSpeed = 10f;
                    __instance._maxScrollSpeed = 10f;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(ModIOTermsOfUse_v1), "PostUpdate")]
        public class TOSPatch2
        {
            private static bool Prefix(ModIOTermsOfUse_v1 __instance)
            {
                if (TOSPatch.enabled)
                {
                    __instance.TurnPage(999);
                    ControllerInputPoller.instance.leftControllerPrimary2DAxis.y = -1f;
                    __instance.holdTime = 0.1f;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(AgeSlider), "PostUpdate")]
        public class TOSPatch3
        {
            private static bool Prefix(AgeSlider __instance)
            {
                if (TOSPatch.enabled)
                {
                    __instance._currentAge = 21;
                    __instance.holdTime = 0.1f;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(PrivateUIRoom), "StartOverlay")]
        public class TOSPatch4
        {
            private static bool Prefix() => !TOSPatch.enabled;
        }
        [HarmonyPatch(typeof(KIDManager), "UseKID")]
        public class TOSPatch5
        {
            private static bool Prefix(ref Task<bool> __result)
            {
                if (!TOSPatch.enabled)
                    return true;

                __result = Task.FromResult(false);
                return false;
            }
        }
    }
    internal class Always : MonoBehaviour, IConnectionCallbacks
    {
        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }
        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }
        public void OnConnected() {}
        public void OnConnectedToMaster() {}
        public void OnDisconnected(DisconnectCause _cause) { Plugin.ConnectToServers(); }
        public void OnRegionListReceived(RegionHandler _regionHandler) {}
        public void OnCustomAuthenticationResponse(Dictionary<string, object> _data) {}
        public void OnCustomAuthenticationFailed(string _debugMessage) { Plugin.ConnectToServers(); }
    }
    public class PhotonCallback : MonoBehaviourPunCallbacks
    {
        public override void OnJoinedRoom()
        {
            CosmeticsController.instance.GetCurrencyBalance();
        }
    }

}
