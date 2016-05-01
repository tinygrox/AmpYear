﻿/**
 * AYController.cs
 *
 * AmpYear power management.
 * (C) Copyright 2015, Jamie Leighton
 * The original code and concept of AmpYear rights go to SodiumEyes on the Kerbal Space Program Forums, which was covered by GNU License GPL (no version stated).
 * As such this code continues to be covered by GNU GPL license.
 * Parts of this code were copied from Fusebox by the user ratzap on the Kerbal Space Program Forums, which is covered by GNU License GPLv2.
 * Concepts which are common to the Game Kerbal Space Program for which there are common code interfaces as such some of those concepts used
 * by this program were based on:
 * Thunder Aerospace Corporation's Life Support for Kerbal Space Program.
 * Written by Taranis Elsu.
 * (C) Copyright 2013, Taranis Elsu
 * Which is licensed under the Attribution-NonCommercial-ShareAlike 3.0 (CC BY-NC-SA 3.0)
 * creative commons license. See <http://creativecommons.org/licenses/by-nc-sa/3.0/legalcode>
 * for full details.
 *
 * Thanks go to both ratzap and Taranis Elsu for their code.
 * Kerbal Space Program is Copyright (C) 2013 Squad. See http://kerbalspaceprogram.com/. This
 * project is in no way associated with nor endorsed by Squad.
 *
 *  This file is part of AmpYear.
 *
 *  AmpYear is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  AmpYear is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with AmpYear.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using UnityEngine;
using RSTUtils;

namespace AY
{
    public partial class AYController : MonoBehaviour, ISavable
    {
        #region Iayaddon

        public List<Part> CrewablePartList
        {
            get
            {
                return crewablePartList;
            }
        }

        public bool[] SubsystemToggle
        {
            get
            {
                return _subsystemToggle;
            }
        }

        public bool HasPower
        {
            get
            {
                return hasPower;
            }
        }

        public bool ManagerisActive
        {
            get
            {
                return ManagerIsActive;
            }
        }

        public bool DeBugging
        {
            get
            {
                return AYsettings.debugging;
            }
        }

        #endregion Iayaddon
        
        public static AYController Instance { get; private set; }

        public AYController()
        {
            Utilities.Log("AYController Constructor");
            Instance = this;
        }

        //AmpYear Properties
        public List<Part> crewablePartList = new List<Part>();
        public List<string> PartsToDelete = new List<string>();
        public List<Part> ReactionWheels = new List<Part>();
        public List<Part> PartsModuleCommand = new List<Part>(); 

        public class ReactionWheelPower
        {
            public float RollTorque { get; set; }
            public float PitchTorque { get; set; }
            public float YawTorque { get; set; }
            public ReactionWheelPower(float roll, float pitch, float yaw)
            {
                RollTorque = roll;
                PitchTorque = pitch;
                YawTorque = yaw;
            }
        }

        public Dictionary<String, ReactionWheelPower> WheelDfltRotPowerMap = new Dictionary<string, ReactionWheelPower>();
        public List<ProtoCrewMember> VslRstr = new List<ProtoCrewMember>();
        public static float SasAdditionalRotPower = 0.0f;
        public static double TurningFactor = 0.0;
        public static double TotalElectricCharge = 0.0;
        public static double TotalElectricChargeFlowOff = 0.0;
        public static double TotalElectricChargeCapacity = 0.0;
        public static double TotalReservePower = 0.0;
        public static double TotalReservePowerFlowOff = 0.0;
        public static double TotalReservePowerCapacity = 0.0;
        public static double TotalPowerDrain = 0.0;
        public static double TotalPowerProduced = 0.0;
        public bool hasPower = true;
        public bool HasReservePower = true;
        public bool HasRcs = false;
        public float currentRCSThrust = 0.0f;
        public float currentPoweredRCSDrain = 0.0f;
        private double _sasPwrDrain = 0;
        public static Guid Currentvesselid;
        public int TotalClimateParts = 0;
        public int MaxCrew = 0;
        private uint rootPartID;
        private bool RT2UnderControl = true;
        
        private bool _reenableRcs = false;  //When RCS is disabled by ESP this flag is set to true once it is ok to re-activate.
        private bool _reenableSas = false;  //When SAS is disabled by ESP this flag is set to true once it is ok to re-activate.

        //ESP Processing vars
        internal static bool Emergencypowerdownactivated = false;  //Set to true if ESP power down has been triggered.
        internal bool Emergencypowerdownprevactivated = false; //Set to true if ESP power down has been previously triggered and reset has not.
        internal static bool Emergencypowerdownreset = false; //Set to true if ESP power down has previously been triggered and it is now ok to reset.
        internal bool Emergencypowerdownresetprevactivated = false; //Set to true if ESP power reset has been previously triggered and power down has not.
        internal ESPPriority _espPriority = ESPPriority.LOW;  //The current ESP Part Priority setting.
        internal bool ESPPriorityHighProcessed = false;  //True if High Priority parts have been processed by ESP during a power down.
        internal bool ESPPriorityMediumProcessed = false; //True if Medium Priority parts have been processed by ESP during a power down.
        internal bool ESPPriorityLowProcessed = false;  //True if Low Priority parts have been processed by ESP during a power down.
        internal bool ESPPriorityHighResetProcessed = false;  //True if High Priority parts have been processed by ESP during a power reset.
        internal bool ESPPriorityMediumResetProcessed = false; //True if Medium Priority parts have been processed by ESP during a power reset.
        internal bool ESPPriorityLowResetProcessed = false;  //True if Low Priority parts have been processed by ESP during a power reset.
        internal PowerState PowerState; //Stores the current power state
        
        PartResourceDefinition definition = PartResourceLibrary.Instance.GetDefinition(MAIN_POWER_NAME);
        
        //Constants
        public const double MANAGER_ACTIVE_DRAIN = 1.0 / 60.0;
        public const double RCS_DRAIN = 1.0 / 60.0;
        public const float POWER_UP_DELAY = 10f;
        public const double SAS_BASE_DRAIN = 1.0 / 60.0;
        public const double POWER_TURN_DRAIN_FACTOR = 1.0 / 5.0;
        public const float SAS_POWER_TURN_TORQUE_FACTOR = 0.25f;
        public const float CLIMATE_HEAT_RATE = 1f;
        public const double CLIMATE_CAPACITY_DRAIN_FACTOR = 0.5;
        public const double RECHARGE_RESERVE_RATE = 30.0 / 60.0;
        public const double RECHARGE_OVERFLOW_AVOID_FACTOR = 1.0;
        public const String MAIN_POWER_NAME = "ElectricCharge";
        public const String RESERVE_POWER_NAME = "ReservePower";
        public const String SUBSYS_STATE_LABEL = "Subsys";
        public const String GUI_SECTION_LABEL = "Section";

        //AmpYear Savable settings
        internal AYSettings AYsettings;
        internal AYGameSettings AYgameSettings;

        //GuiVisibility
        private bool _visible = false;
        private bool _mouseDownResize;
        private bool _mouseDownHorizScl1;
        private bool _mouseDownHorizScl2;
        private bool _gamePaused = false;
        private bool _hideUI = false;

        public Boolean GuiVisible
        {
            get { return _visible; }
            set
            {
                _visible = value;      //Set the private variable
            }
        }

        public void Awake()
        {
            Utilities.Log("AYController Awake in {0}" , HighLogic.LoadedScene.ToString());
            AYsettings = AmpYear.Instance.AYsettings;
            AYgameSettings = AmpYear.Instance.AYgameSettings;
            _fwindowId = Utilities.getnextrandomInt();
            _ewindowId = _fwindowId + 1; 
            _wwindowId = _ewindowId + 1;
            _swindowId = _wwindowId + 1;
            _dwindowId = _swindowId + 1;
            _SOIwindowId = _dwindowId + 1;
            _eplPartName = Mathf.Round((_epLwindowPos.width - 28f) * .3f);
            _eplPartModuleName = Mathf.Round((_epLwindowPos.width - 28f) * .4f);
            _eplec = Mathf.Round((_epLwindowPos.width - 28f) * .17f);
            _eplProdListHeight = _eplConsListHeight = Mathf.Round((_epLwindowPos.height - 170) * .5f);
            GameEvents.OnVesselRollout.Add(OnVesselRollout);
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            Utilities.Log_Debug("AYController Awake complete");
        }

        private void OnGuiAppLauncherReady()
        {
            Utilities.Log_Debug("OnGUIAppLauncherReady");
            if (ApplicationLauncher.Ready && _stockToolbarButton == null)
            {
                Utilities.Log_Debug("Adding AppLauncherButton");
                _stockToolbarButton = ApplicationLauncher.Instance.AddModApplication(OnAppLaunchToggle, OnAppLaunchToggle, DummyVoid,
                                          DummyVoid, DummyVoid, DummyVoid, ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH |
                                          ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                                          Textures.IconGreenOff);
            }
        }

        private void DummyVoid()
        {
        }

        public void OnAppLaunchToggle()
        {
            GuiVisible = !GuiVisible;
            if (AYsettings.UseAppLauncher == true)
                _stockToolbarButton.SetTexture(GuiVisible ? Textures.IconGreenOn : Textures.IconGreenOff);
        }

        public void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
        {
            if (_stockToolbarButton != null)
            {
                ApplicationLauncherButton[] lstButtons = FindObjectsOfType<ApplicationLauncherButton>();
                Utilities.Log_Debug("AmpYear AppLauncher: Destroying Button-Button Count:" + lstButtons.Length);
                ApplicationLauncher.Instance.RemoveModApplication(_stockToolbarButton);
                _stockToolbarButton = null;
            }
        }

        public void Start()
        {
            Utilities.Log_Debug("AYController Start");
            Utilities.Log_Debug("AYcontroller ToolbarAvailable=" + ToolbarManager.ToolbarAvailable + ",UseAppLauncher=" + AYsettings.UseAppLauncher);
            if (ToolbarManager.ToolbarAvailable && AYsettings.UseAppLauncher == false)
            {
                _button1 = ToolbarManager.Instance.add("AmpYear", "button1");
                _button1.TexturePath = Textures.PathToolbarIconsPath + "/AYGreenOffTB";
                _button1.ToolTip = "AmpYear";
                _button1.Visibility = new GameScenesVisibility(GameScenes.FLIGHT, GameScenes.EDITOR);
                _button1.OnClick += (e) =>
                {
                    GuiVisible = !GuiVisible;
                    if (GuiVisible)
                        _button1.TexturePath = Textures.PathToolbarIconsPath + "/AYGreenOnTB";
                    else
                        _button1.TexturePath = Textures.PathToolbarIconsPath + "/AYGreenOffTB";
                };
            }
            else
            {
                // Set up the stock toolbar
                Utilities.Log_Debug("Adding onGUIAppLauncher callbacks");
                if (ApplicationLauncher.Ready)
                {
                    OnGuiAppLauncherReady();
                }
                else
                    GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
            }

            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
            Utilities.setScaledScreen();

            // Find out which mods are present
            ALPresent = Utilities.IsModInstalled("AviationLights");
            NFEPresent = Utilities.IsModInstalled("NearFutureElectrical");  
            NFSPresent = Utilities.IsModInstalled("NearFutureSolar"); 
            KASPresent = Utilities.IsModInstalled("KAS"); 
            _rt2Present = Utilities.IsModInstalled("RemoteTech"); 
            ScSPresent = Utilities.IsModInstalled("SCANsat"); 
            TelPresent = Utilities.IsModInstalled("Telemachus"); 
            TACLPresent = Utilities.IsModInstalled("TacLifeSupport");
            AntRPresent = Utilities.IsModInstalled("AntennaRange");
            KISEPresent = Utilities.IsModInstalled("Interstellar");
            TFCPresent = Utilities.IsModInstalled("ToggleFuelCell"); 
            KKPresent = Utilities.IsModInstalled("KabinKraziness"); 
            DFPresent = Utilities.IsModInstalled("DeepFreeze");
            KPBSPresent = Utilities.IsModInstalled("PlanetarySurfaceStructures");
            USILSPresent = Utilities.IsModInstalled("USILifeSupport");
            IONRCSPresent = Utilities.IsModInstalled("IONRCS");
            Utilities.Log_Debug(KKPresent ? "KabinKraziness present" : "KabinKraziness NOT present");
            //if (KKPresent)  //Moved to FixedUpdate
            //{
            //    Utilities.Log_Debug("KabinKraziness present");
            //    KKWrapper.InitKKWrapper();
            //    if (!KKWrapper.APIReady)
            //    {
            //        KKPresent = false;
            //    }
            //}
            if (DFPresent)
            {
                Utilities.Log_Debug("DeepFreeze present");
                DFWrapper.InitDFWrapper();
                if (!DFWrapper.APIReady)
                {
                    DFPresent = false;
                    Utilities.Log("Ampyear - Near Future Solar Interface Failed");
                }
            }
            if (ALPresent)
            {
                Utilities.Log_Debug("Aviation Lights present");
                ALWrapper.InitTALWrapper();
                if (!ALWrapper.APIReady)
                {
                    ALPresent = false;
                    Utilities.Log("Ampyear - Aviation Lights Interface Failed");
                }
            }
            if (NFEPresent)
                Utilities.Log_Debug("Near Future Electric present");

            if (NFSPresent)
            {
                Utilities.Log_Debug("Near Future Solar present");
                NFSWrapper.InitNFSWrapper();
                if (!NFSWrapper.APIReady)
                {
                    NFSPresent = false;
                    Utilities.Log("Ampyear - Near Future Electric Interface Failed");
                }
            }

            if (KASPresent)
            {
                Utilities.Log_Debug("KAS present");
                KASWrapper.InitKASWrapper();
                if (!KASWrapper.APIReady)
                {
                    KASPresent = false;
                    Utilities.Log("Ampyear - KAS Interface Failed");
                }
            }
            if (_rt2Present)
            {
                Utilities.Log_Debug("RT2 present");
                RTWrapper.InitTRWrapper();
                if (!RTWrapper.APIReady)
                {
                    _rt2Present = false;
                    Utilities.Log("Ampyear - RT2 Interface Failed");
                }
            }
            if (ScSPresent)
            {
                Utilities.Log_Debug("SCANSat present");
                ScanSatWrapper.InitSCANsatWrapper();
                if (!ScanSatWrapper.APIReady)
                {
                    ScSPresent = false;
                    Utilities.Log("Ampyear - SCANSat Interface Failed");
                }
            }
            if (TelPresent)
            {
                Utilities.Log_Debug("Telemachus present");
                TeleWrapper.InitTALWrapper();
                if (!TeleWrapper.APIReady)
                {
                    TelPresent = false;
                    Utilities.Log("Ampyear - Telemachus Interface Failed");
                }
            }
            if (TACLPresent)
            {
                Utilities.Log_Debug("TAC LS present");
                TACLSWrapper.InitTACLSWrapper();
                if (!TACLSWrapper.APIReady)
                {
                    TACLPresent = false;
                    Utilities.Log("Ampyear - TAC LS Interface Failed");
                }
            }
            if (USILSPresent)
            {
                Utilities.Log_Debug("USI LS present");
                USILSWrapper.InitUSILSWrapper();
                if (!USILSWrapper.APIReady)
                {
                    USILSPresent = false;
                    Utilities.Log("Ampyear - USI LS Interface Failed");
                }
            }
            if (KPBSPresent)
            {
                Utilities.Log_Debug("KPBS present");
                KPBSWrapper.InitKPBSWrapper();
                if (!KPBSWrapper.APIReady)
                {
                    KPBSPresent = false;
                    Utilities.Log("Ampyear - KPBS Interface Failed");
                }
            }
            if (AntRPresent)
                Utilities.Log_Debug("AntennaRange present");
            if (KISEPresent)
            {
                Utilities.Log_Debug("Interstellar present");
                KSPIEWrapper.InitKSPIEWrapper();
                if (!KSPIEWrapper.APIReady)
                {
                    KISEPresent = false;
                    Utilities.Log("Ampyear - Interstellar Interface Failed");
                }
            }
                
            if (TFCPresent)
                Utilities.Log_Debug("ToggleFuelCell present");

            if (IONRCSPresent)
            {
                Utilities.Log_Debug("IONRCS present");
                IONRCSWrapper.InitIONRCSWrapper();
                if (!IONRCSWrapper.APIReady)
                {
                    IONRCSPresent = false;
                    Utilities.Log("Ampyear - IONRCS Interface Failed");
                }
            }

            //check if inflight and active vessel set currentvesselid and load config settings for this vessel
            if (FlightGlobals.fetch != null && FlightGlobals.ActiveVessel != null)
            {
                Currentvesselid = FlightGlobals.ActiveVessel.id;
                OnVesselLoad(FlightGlobals.ActiveVessel);
            }

            //Create DarkBodies list
            _darkBodies.Clear();
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                _darkBodies.Add(FlightGlobals.Bodies[i]);
            }

            //Set the default SolarPanel SOI Target and DarkSide Target to HomeBody Index (can be changed in the GUI by the user)
            _selectedDarkTarget = _selectedSolarSOITarget = FlightGlobals.GetHomeBodyIndex();
            _bodyTarget = FlightGlobals.Bodies[_selectedDarkTarget];

            // add callbacks for vessel load and change
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselLoaded.Add(OnVesselLoad);
            GameEvents.onCrewBoardVessel.Add(OnCrewBoardVessel);
            GameEvents.onGamePause.Add(GamePaused);
            GameEvents.onGameUnpause.Add(GameUnPaused);
            GameEvents.onHideUI.Add(onHideUI);
            GameEvents.onShowUI.Add(onShowUI);
            GameEvents.onGUIEngineersReportReady.Add(AddTests);

            Utilities.Log_Debug("AYController Start complete"); 
        }

        internal void AddTests()
        {
            Utilities.Log_Debug("Adding AY Engineer Test");
            PreFlightTests.IDesignConcern AYtest = new AYEngReport();
            EngineersReport.Instance.AddTest(AYtest);
        }

        public void OnDestroy()
        {
            Utilities.Log_Debug("AYcontroller ToolbarAvailable=" + ToolbarManager.ToolbarAvailable.ToString() + ",UseAppLauncher=" + AYsettings.UseAppLauncher.ToString());
            if (ToolbarManager.ToolbarAvailable && AYsettings.UseAppLauncher == false)
            {
                _button1.Destroy();
            }
            else
            {
                // Set up the stock toolbar
                Utilities.Log_Debug("Removing onGUIAppLauncher callbacks");
                GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
                if (_stockToolbarButton != null)
                {
                    ApplicationLauncher.Instance.RemoveModApplication(_stockToolbarButton);
                    _stockToolbarButton = null;
                }
            }
            if (GuiVisible) GuiVisible = !GuiVisible;
            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequestedForAppLauncher);
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselLoaded.Remove(OnVesselLoad);
            GameEvents.onCrewBoardVessel.Remove(OnCrewBoardVessel);
            GameEvents.onGamePause.Remove(GamePaused);
            GameEvents.onGameUnpause.Remove(GameUnPaused);
            GameEvents.onHideUI.Remove(onHideUI);
            GameEvents.onShowUI.Remove(onShowUI);
            GameEvents.onGUIEngineersReportReady.Remove(AddTests);
            GameEvents.OnVesselRollout.Remove(OnVesselRollout);
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
        }

        private void OnVesselRollout(ShipConstruct Ship)
        {
            Utilities.Log("OnVesselRollout");

        }
        private void OnVesselCreate(Vessel Vessel)
        {
            Utilities.Log("OnVesselCreate");
        }

        private void GamePaused()
        {
            _gamePaused = true;
        }

        private void GameUnPaused()
        {
            _gamePaused = false;
        }

        private void onHideUI()
        {
            _hideUI = true;
        }

        private void onShowUI()
        {
            _hideUI = false;
        }

        private void FixedUpdate()
        {
            if (Time.timeSinceLevelLoad < 3.0f) // Check not loading level
            {
                return; 
            }
            
            if (!Utilities.GameModeisFlight && !Utilities.GameModeisEditor) return; // Only execute Update in Flight or Editor Scene
            
            if ((FlightGlobals.ready && FlightGlobals.ActiveVessel != null) || HighLogic.LoadedSceneIsEditor)
            {
                //Timing for KabinKraziness must be here not in Start.
                if (KKPresent && !KKWrapper.APIReady)
                {
                    Utilities.Log_Debug("KabinKraziness present");
                    KKWrapper.InitKKWrapper();
                    if (!KKWrapper.APIReady)
                    {
                        KKPresent = false;
                        Utilities.Log("Ampyear - KabinKraziness Interface Failed");
                    }
                }
                
                //Set Remote Tech connection status if installed
                if (_rt2Present)
                {
                    if (RTWrapper.APIReady && FlightGlobals.ActiveVessel != null)
                    {
                        RT2UnderControl = RTWrapper.RTactualAPI.HasLocalControl(FlightGlobals.ActiveVessel.id) ||
                                          RTWrapper.RTactualAPI.HasAnyConnection(FlightGlobals.ActiveVessel.id);
                    }
                    else
                    {
                        RT2UnderControl = true;
                        Utilities.Log_Debug("Remote Tech installed but unable to check active connections, assume we have a connection");
                    }
                }
                //get current vessel parts list
                List<Part> parts = new List<Part> { };
                if (Utilities.GameModeisFlight)
                {
                    try
                    {
                        parts = FlightGlobals.ActiveVessel.Parts;
                        rootPartID = 1111;
                        rootPartID = FlightGlobals.ActiveVessel.rootPart.craftID;
                        if (parts == null)
                        {
                            Utilities.Log("In Flight but couldn't get parts list");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("Reference"))
                        {
                            Utilities.Log("NullRef occurred getting parts list");
                        }
                        else
                        {
                            Utilities.Log_Debug("Error occurred getting parts list " + ex.Message);
                        }
                        return;
                    }
                    CheckVslUpdate();
                }
                else
                    try
                    {
                        //parts = EditorLogic.SortedShipList;
                        parts = EditorLogic.fetch.ship.parts;
                        rootPartID = 1111;
                        if (EditorLogic.RootPart != null)
                            rootPartID = EditorLogic.RootPart.craftID;
                        if (parts == null)
                        {
                            Utilities.Log_Debug("In Editor but couldn't get parts list");
                            return;
                        }
                    }
                    catch (Exception Ex)
                    {
                        if (Ex.Message.Contains("Reference"))
                        {
                            Utilities.Log("NullRef occurred getting parts list");
                        }
                        else
                        {
                            Utilities.Log_Debug("Error occurred getting parts list " + Ex.Message);
                        }
                        return;
                    }
                //Compile information about the vessel and its parts
                // zero accumulators
                SasAdditionalRotPower = 0.0f;
                TotalElectricCharge = 0.0;
                TotalElectricChargeFlowOff = 0.0;
                TotalElectricChargeCapacity = 0.0;
                TotalReservePower = 0.0;
                TotalReservePowerFlowOff = 0.0;
                TotalReservePowerCapacity = 0.0;
                TotalClimateParts = 0;
                TotalPowerDrain = 0;
                TotalPowerProduced = 0;
                _sasPwrDrain = 0.0f;
                HasRcs = false;
                currentRCSThrust = 0.0f;
                currentPoweredRCSDrain = 0.0f;
                crewablePartList.Clear();
                PartsModuleCommand.Clear();
                ReactionWheels.Clear();
                MaxCrew = 0;

                try
                {
                    PartsToDelete.Clear();
                    foreach (var entry in AYVesselPartLists.VesselProdPartsList)
                    {
                        entry.Value.PrtPower = "0";
                        entry.Value.PrtPowerF = 0;
                        entry.Value.PrtActive = false;
                        if (!PartsToDelete.Contains(entry.Key))
                            PartsToDelete.Add(entry.Key);
                    }
                    foreach (var entry in AYVesselPartLists.VesselConsPartsList)
                    {
                        entry.Value.PrtPower = "0";
                        entry.Value.PrtPowerF = 0;
                        entry.Value.PrtActive = false;
                        if (!PartsToDelete.Contains(entry.Key))
                            PartsToDelete.Add(entry.Key);
                    }
                    VslRstr.Clear(); //clear the vessel roster

                    //Begin calcs
                    if (Utilities.GameModeisFlight) // if in flight compile the vessel roster
                        VslRstr = FlightGlobals.ActiveVessel.GetVesselCrew();

                    //loop through all parts in the parts list of the vessel
                    foreach (Part currentPart in parts)
                    {
                        if (currentPart.CrewCapacity > 0)
                        {
                            crewablePartList.Add(currentPart);
                            MaxCrew += currentPart.CrewCapacity;
                        }
                        bool hasAlternator = false;
                        bool currentEngActive = false;
                        double altRate = 0f;

                        //loop through all the modules in the current part
                        foreach (PartModule module in currentPart.Modules)
                        {
                            //Check if the current module is a stock part module and process it.
                            //Returned values : Bool true if it was a stock module or false if it was not.
                            // hasAlternator is true if the module had an Alternator.
                            bool outhasAlternator = false;
                            bool outcurrentEngActive = false;
                            double outaltRate = 0f;

                            bool wasStockModule = ProcessStockPartModule(currentPart, module, hasAlternator, currentEngActive, altRate,
                                out outhasAlternator, out outcurrentEngActive, out outaltRate);

                            hasAlternator = outhasAlternator;
                            currentEngActive = outcurrentEngActive;
                            altRate = outaltRate;

                            //If it wasn't a stock part module, process the currently supported mod partmodules.
                            if (!wasStockModule)
                            {
                                ProcessModPartModule(currentPart, module);
                            }
                        } // end modules loop

                        //Sum up the power resources
                        if (!hasAlternator) //Ignore parts with alternators in power-capacity calculate because they don't actually store power
                        {
                            foreach (PartResource resource in currentPart.Resources)
                            {
                                if (resource.resourceName == MAIN_POWER_NAME)
                                {
                                    if (resource.flowState)
                                    {
                                        TotalElectricCharge += resource.amount;
                                    }
                                    else
                                    {
                                        TotalElectricChargeFlowOff += resource.amount;
                                    }
                                    TotalElectricChargeCapacity += resource.maxAmount;
                                }
                                else if (resource.resourceName == RESERVE_POWER_NAME)
                                {
                                    if (resource.flowState)
                                    {
                                        TotalReservePower += resource.amount;
                                    }
                                    else
                                    {
                                        TotalReservePowerFlowOff += resource.amount;
                                    }
                                    TotalReservePowerCapacity += resource.maxAmount;
                                }
                            }
                        }
                    } // end part loop
                }
                catch (Exception ex)
                {
                    Utilities.Log("Failure in Part Processing");
                    Utilities.Log("Exception: {0}", ex);
                }

                //Here we check if ESP is running and if it is we set the Processing flags so we only do it once per priority until a EC %age 
                // is crossed again.
                if (Emergencypowerdownactivated)
                {
                    switch (_espPriority)
                    {
                        case ESPPriority.HIGH:
                            ESPPriorityHighProcessed = true;
                            break;
                        case ESPPriority.MEDIUM:
                            ESPPriorityMediumProcessed = true;
                            break;
                        case ESPPriority.LOW:
                            ESPPriorityLowProcessed = true;
                            break;
                    }
                }
                if (Emergencypowerdownreset)
                {
                    switch (_espPriority)
                    {
                        case ESPPriority.HIGH:
                            ESPPriorityHighResetProcessed = true;
                            break;
                        case ESPPriority.MEDIUM:
                            ESPPriorityMediumResetProcessed = true;
                            break;
                        case ESPPriority.LOW:
                            ESPPriorityLowResetProcessed = true;
                            break;
                    }
                }

                //Do Subsystem processing, EC calcs and Emergency Shutdown Processing
                try
                {
                    SubsystemUpdate();
                }
                catch (Exception ex)
                {
                    Utilities.Log("Failure in AYSubSystem Processing");
                    Utilities.Log("Exception: {0}", ex);
                }

                //Remove parts that aren't currently attached to the current vessel.
                foreach (string part in PartsToDelete)
                {
                    if (AYVesselPartLists.VesselProdPartsList.ContainsKey(part))
                    {
                        AYVesselPartLists.VesselProdPartsList.Remove(part);
                    }
                    if (AYVesselPartLists.VesselConsPartsList.ContainsKey(part))
                    {
                        AYVesselPartLists.VesselConsPartsList.Remove(part);
                    }
                }
            } // end if active vessel not null
        }

        private void SubsystemUpdate()
        {
            //This is the Logic that Executes in Flight
            if (HighLogic.LoadedSceneIsFlight)
            {
                Vessel cv = FlightGlobals.ActiveVessel;

                if (cv.ActionGroups[KSPActionGroup.RCS] && !SubsystemPowered(Subsystem.RCS))
                {
                    Utilities.Log("RCS - disabled.");
                    //Disable RCS when the subsystem isn't powered
                    cv.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                    _reenableRcs = true;
                }

                if (KKPresent)
                {
                    KKAutopilotChk(cv);
                }
                else
                {
                    if (cv.ActionGroups[KSPActionGroup.SAS] && !SubsystemPowered(Subsystem.SAS))
                    {
                        Utilities.Log("SAS - disabled.");
                        //Disable SAS when the subsystem isn't powered
                        cv.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                        _reenableSas = true;
                    }
                }

                if (ManagerIsActive && hasPower)
                {
                    //Re-enable SAS/RCS if they were shut off by the manager and can be run again
                    if (KKPresent)
                    {
                        KKSASChk();
                    }
                    else
                    {
                        if (_reenableSas)
                        {
                            SetSubsystemEnabled(Subsystem.SAS, true);
                            _reenableSas = false;
                            Utilities.Log("SAS - enabled.");
                        }
                    }

                    if (_reenableRcs)
                    {
                        SetSubsystemEnabled(Subsystem.RCS, true);
                        _reenableRcs = false;
                        Utilities.Log("RCS - enabled.");
                    }
                }

                //Update command pod rot powers
                bool powerTurnOn = SubsystemPowered(Subsystem.POWER_TURN);

                foreach (Part reactWheel in ReactionWheels)
                {
                    ModuleReactionWheel reactWheelModule = reactWheel.FindModuleImplementing<ModuleReactionWheel>();
                    ReactionWheelPower defaultRotPower = new ReactionWheelPower(0, 0, 0);
                    WheelDfltRotPowerMap.TryGetValue(reactWheel.name, out defaultRotPower);

                    if (powerTurnOn)
                    {
                        //Apply power turn rotPower
                        reactWheelModule.RollTorque = defaultRotPower.RollTorque + SasAdditionalRotPower;
                        reactWheelModule.PitchTorque = defaultRotPower.PitchTorque + SasAdditionalRotPower;
                        reactWheelModule.YawTorque = defaultRotPower.YawTorque + SasAdditionalRotPower;
                    }
                    else //Use default rot power
                    {
                        reactWheelModule.RollTorque = defaultRotPower.RollTorque;
                        reactWheelModule.PitchTorque = defaultRotPower.PitchTorque;
                        reactWheelModule.YawTorque = defaultRotPower.YawTorque;
                    }
                }

                //Calculate total drain from subsystems
                double subsystem_drain = 0.0;
                _subsystemDrain[(int)Subsystem.RCS] -= currentPoweredRCSDrain;
                foreach (Subsystem subsystem in Enum.GetValues(typeof(Subsystem)))
                {
                    _subsystemDrain[(int)subsystem] = SubsystemCurrentDrain(subsystem);
                    subsystem_drain += _subsystemDrain[(int)subsystem];
                }

                double manager_drain = ManagerCurrentDrain; 

                double total_manager_drain = subsystem_drain + manager_drain;
                //TotalPowerDrain += total_manager_drain;

                //Recharge reserve power if main power is above a certain threshold
                if (ManagerIsActive && (TotalElectricCharge > 0) && (TotalElectricCharge / TotalElectricChargeCapacity > AYsettings.RECHARGE_RESERVE_THRESHOLD)
                    && (TotalReservePower < TotalReservePowerCapacity))
                    TransferMainToReserve(RECHARGE_RESERVE_RATE * TimeWarp.fixedDeltaTime);

                //Store the AmpYear Manager and Subsystems into the parts list
                string prtName = "AmpYear SubSystems";
                string prtPower = "";
                uint partId = CalcAmpYearMgrPartID();
                int index = 2;
                foreach (Subsystem subsystem in Enum.GetValues(typeof(Subsystem)))
                {
                    if (!KKPresent && (subsystem == Subsystem.CLIMATE || subsystem == Subsystem.MASSAGE || subsystem == Subsystem.MUSIC))
                        continue;
                    prtPower = _subsystemDrain[(int)subsystem].ToString("###0.##");
                    PwrPartList PartAdd = new PwrPartList(prtName, SubsystemName(subsystem), true, prtPower, (float)_subsystemDrain[(int)subsystem], SubsystemEnabled(subsystem), false);
                    AYVesselPartLists.AddPart(partId, PartAdd, false, false);
                    index++;
                }

                prtName = "AmpYear Manager";
                prtPower = manager_drain.ToString("####0.###");
                PwrPartList PartAdd2 = new PwrPartList(prtName, prtName, true, prtPower, (float)manager_drain, _managerEnabled, false);
                AYVesselPartLists.AddPart(partId, PartAdd2, false, false);

                //Drain main power
                Part cvp = FlightGlobals.ActiveVessel.rootPart;
                double currentTime = Planetarium.GetUniversalTime();
                double timestepDrain = total_manager_drain * TimeWarp.fixedDeltaTime;
                double minimumSufficientCharge = managerActiveDrain + 4;
                double deltaTime = Math.Min(currentTime - _timeLastElectricity, Math.Max(1, TimeWarp.fixedDeltaTime));
                double desiredElectricity = total_manager_drain * deltaTime;
                
                if (desiredElectricity > 0.0 && TimewarpIsValid) // if power required > 0 and time warp is valid
                {
                    if (TotalElectricCharge >= desiredElectricity) // if main power >= power required
                    {
                        Utilities.Log_Debug("drawing main power");
                        double totalElecreceived = Utilities.RequestResource(cvp, MAIN_POWER_NAME, desiredElectricity);  //get power
                        _timeLastElectricity = currentTime - (desiredElectricity - totalElecreceived) / total_manager_drain; //set time last power received
                        hasPower = (UnityEngine.Time.realtimeSinceStartup > _powerUpTime)
                        && (desiredElectricity <= 0.0 || totalElecreceived >= desiredElectricity * 0.99); //set hasPower > power up delay and we received power requested
                    }
                    else //not enough main power try reserve power
                    {
                        hasPower = TotalElectricCharge >= minimumSufficientCharge; //set hasPower
                        Utilities.Log_Debug("drawing reserve power");
                        if (TotalReservePower > minimumSufficientCharge && !_lockReservePower) // if reserve power available > minimum charge required
                        {
                            //If main power is insufficient, drain reserve power for manager
                            double managerTimestepDrain = manager_drain * TimeWarp.fixedDeltaTime; //we only drain manager function
                            double deltaTime2 = Math.Min(currentTime - _timeLastElectricity, Math.Max(1, TimeWarp.fixedDeltaTime));
                            double desiredElectricity2 = manager_drain * deltaTime;
                            double totalElecreceived2 = Utilities.RequestResource(cvp, RESERVE_POWER_NAME, desiredElectricity2); // get power
                            _timeLastElectricity = currentTime - (desiredElectricity2 - totalElecreceived2) / manager_drain; // set time last power received
                            HasReservePower = (UnityEngine.Time.realtimeSinceStartup > _powerUpTime)
                            && (desiredElectricity2 <= 0.0 || totalElecreceived2 >= desiredElectricity2 * 0.99); // set hasReservePower > power up delay and we received power
                        }
                        else  // not enough reservepower
                        {
                            if (_lockReservePower)
                                Utilities.Log_Debug("reserve power is isolated");
                            else
                                Utilities.Log_Debug("not enough reserve power");
                            HasReservePower = TotalReservePower > minimumSufficientCharge; //set hasReservePower
                            _timeLastElectricity += currentTime - _lastUpdate; //set time we last received electricity to current time - last update
                        }
                    }
                }
                else  // no electricity required OR time warp is too high (so we hibernate)
                {
                    Utilities.Log_Debug("Timewarp not valid or elec < 0");
                    _timeLastElectricity += currentTime - _lastUpdate;
                }
                
                //Do ESP checking and processing.
                ProcessEmergencyShutdownChecking();

                if (!hasPower) //some processing if we are out of power
                {
                    Utilities.Log_Debug("no main power processing");
                    if (UnityEngine.Time.realtimeSinceStartup > _powerUpTime)
                    //reset the power up delay - for powering back up to avoid rapid flickering of the system
                    {
                        Utilities.Log_Debug("reset power up delay");
                        _powerUpTime = UnityEngine.Time.realtimeSinceStartup + POWER_UP_DELAY;
                        Utilities.Log_Debug("powerup time = " + _powerUpTime.ToString());
                    }
                    HasReservePower = TotalReservePower > minimumSufficientCharge; //set hasReservePower
                    hasPower = TotalElectricCharge >= minimumSufficientCharge; //set hasPower
                    Utilities.Log_Debug("hasReservePower = " + HasReservePower.ToString());
                    Utilities.Log_Debug("hasPower = " + hasPower.ToString());
                }
                _lastUpdate = currentTime;
            }
            
            //This is the Logic that Executes in the Editor (VAB/SPH)
            if (HighLogic.LoadedSceneIsEditor)
            {
                //Calculate total drain from subsystems
                double subsystem_drain = 0.0;
                double manager_drain = 0.0;
                string prtName = "AmpYear SubSystems-Max";
                string prtPower = "";
                uint partId = CalcAmpYearMgrPartID();
                int index = 2;
                foreach (Subsystem subsystem in Enum.GetValues(typeof(Subsystem)))
                {
                    _subsystemDrain[(int)subsystem] = SubsystemActiveDrain(subsystem);
                    subsystem_drain += _subsystemDrain[(int)subsystem];
                    if (!KKPresent && (subsystem == Subsystem.CLIMATE || subsystem == Subsystem.MASSAGE || subsystem == Subsystem.MUSIC))
                        continue;
                    prtPower = _subsystemDrain[(int) subsystem].ToString("###0.##");
                    PwrPartList PartAdd = new PwrPartList(prtName, SubsystemName(subsystem), true, prtPower, (float)_subsystemDrain[(int)subsystem], true, false);
                    AYVesselPartLists.AddPart(partId, PartAdd, false, false);
                    index++;
                }
                manager_drain = ManagerCurrentDrain;
                
                prtName = "AmpYear Manager";
                //string PrtPower = "";
                PwrPartList PartAdd2 = new PwrPartList(prtName, prtName, true, prtPower, (float)manager_drain, true, false);
                AYVesselPartLists.AddPart(partId, PartAdd2, false, false);
                hasPower = true;
                HasReservePower = true;
            }
        }
        
        #region VesselFunctions

        //Vessel Functions Follow - to store list of vessels and store/retrieve AmpYear settings for each vessel

        private void CheckVslUpdate()
        {
            // Called every fixed update from fixedupdate - Check for vessels that have been deleted and remove from Dictionary
            // also updates current active vessel details/settings
            // adds new vessel if current active vessel is not known and updates it's details/settings
            double currentTime = Planetarium.GetUniversalTime();
            List<Vessel> allVessels = FlightGlobals.Vessels;
            var vesselsToDelete = new List<Guid>();
            //* Delete vessels that do not exist any more or have no crew
            foreach (var entry in AYgameSettings.KnownVessels)
            {
                Utilities.Log_Debug("AYController knownvessels id = " + entry.Key.ToString() + " Name = " + entry.Value.VesselName);
                Guid vesselId = entry.Key;
                VesselInfo vesselInfo = new VesselInfo(entry.Value.VesselName, currentTime);
                vesselInfo = entry.Value;
                Vessel vessel = allVessels.Find(v => v.id == vesselId);
                if (vessel == null)
                {
                    Utilities.Log_Debug("Deleting vessel " + vesselInfo.VesselName + " - vessel does not exist anymore");
                    vesselsToDelete.Add(vesselId);
                    continue;
                }
                if (vessel.loaded)
                {
                    int crewCapacity = UpdateVesselCounts(vesselInfo, vessel);
                    if (vesselInfo.NumCrew == 0)
                    {
                        Utilities.Log_Debug("Deleting vessel " + vesselInfo.VesselName + " - no crew parts anymore");
                        vesselsToDelete.Add(vesselId);
                        continue;
                    }
                }
            }
            vesselsToDelete.ForEach(id => AYgameSettings.KnownVessels.Remove(id));

            //* Add all new vessels
            foreach (Vessel vessel in allVessels.Where(v => v.loaded))
            {
                Guid vesselId = vessel.id;
                if (!AYgameSettings.KnownVessels.ContainsKey(vesselId) && Utilities.ValidVslType(vessel))
                {
                    if (vessel.FindPartModulesImplementing<ModuleCommand>().FirstOrDefault() != null)
                    {
                        Utilities.Log_Debug("New vessel: " + vessel.vesselName + " (" + vessel.id + ")");
                        VesselInfo vesselInfo = new VesselInfo(vessel.vesselName, currentTime);
                        vesselInfo.VesselType = vessel.vesselType;
                        UpdateVesselInfo(vesselInfo);
                        int crewCapacity = UpdateVesselCounts(vesselInfo, vessel);
                        AYgameSettings.KnownVessels.Add(vesselId, vesselInfo);
                    }
                }
            }

            //*Update the current vessel
            VesselInfo currvesselInfo = new VesselInfo(FlightGlobals.ActiveVessel.vesselName, currentTime);
            if (AYgameSettings.KnownVessels.TryGetValue(Currentvesselid, out currvesselInfo))
            {
                UpdateVesselInfo(currvesselInfo);
                int crewCapacity = UpdateVesselCounts(currvesselInfo, FlightGlobals.ActiveVessel);
                currvesselInfo.VesselType = FlightGlobals.ActiveVessel.vesselType;
                AYgameSettings.KnownVessels[Currentvesselid] = currvesselInfo;
            }
        }

        private void UpdateVesselInfo(VesselInfo vesselInfo)
        {
            // save current toggles to current vesselinfo
            vesselInfo.ManagerEnabled = _managerEnabled;
            vesselInfo.ShowCrew = _showCrew;
            vesselInfo.ShowParts = _showParts;
            vesselInfo.TimeLastElectricity = _timeLastElectricity;
            vesselInfo.LastUpdate = _lastUpdate;
            for (int i = 0; i < Enum.GetValues(typeof(Subsystem)).Length; i++)
            {
                vesselInfo.SubsystemToggle[i] = _subsystemToggle[i];
                vesselInfo.SubsystemDrain[i] = _subsystemDrain[i];
            }
            for (int i = 0; i < Enum.GetValues(typeof(GUISection)).Length; i++)
                vesselInfo.GuiSectionEnableFlag[i] = _guiSectionEnableFlag[i];
            vesselInfo.EmgcyShutActive = EmgcyShutActive;
            if (KKPresent)
            {
                KKUpdateVslInfo(vesselInfo);
            }
            else
            {
                vesselInfo.AutoPilotDisabled = false;
                vesselInfo.AutoPilotDisCounter = 0;
                vesselInfo.AutoPilotDisTime = 0;
            }
            vesselInfo.EmgcyShutOverride = EmgcyShutOverride;
            vesselInfo.EmgcyShutOverrideStarted = EmgcyShutOverrideStarted;
            vesselInfo.Emergencypowerdownactivated = Emergencypowerdownactivated;
            vesselInfo.Emergencypowerdownreset = Emergencypowerdownreset;
            vesselInfo.Emergencypowerdownprevactivated = Emergencypowerdownprevactivated;
            vesselInfo.Emergencypowerdownresetprevactivated = Emergencypowerdownresetprevactivated;
            vesselInfo.ESPPriorityHighProcessed = ESPPriorityHighProcessed;
            vesselInfo.ESPPriorityMediumProcessed = ESPPriorityMediumProcessed;
            vesselInfo.ESPPriorityLowProcessed = ESPPriorityLowProcessed;
            vesselInfo.ESPPriorityHighResetProcessed = ESPPriorityHighResetProcessed;
            vesselInfo.ESPPriorityMediumResetProcessed = ESPPriorityMediumResetProcessed;
            vesselInfo.ESPPriorityLowResetProcessed = ESPPriorityLowResetProcessed;
            vesselInfo.EspPriority = _espPriority;
            vesselInfo.ReenableRcs = _reenableRcs;
            vesselInfo.ReenableSas = _reenableSas;
        }

        private int UpdateVesselCounts(VesselInfo vesselInfo, Vessel vessel)
        {
            // save current toggles to current vesselinfo
            int crewCapacity = 0;
            vesselInfo.ClearAmounts(); // numCrew = 0; numOccupiedParts = 0;
            foreach (Part part in vessel.parts)
            {
                crewCapacity += part.CrewCapacity;
                if (part.protoModuleCrew.Count > 0)
                {
                    vesselInfo.NumCrew += part.protoModuleCrew.Count;
                    ++vesselInfo.NumOccupiedParts;
                }
            }
            return crewCapacity;
        }

        private void OnVesselLoad(Vessel newvessel)
        {
            Utilities.Log_Debug("AYController onVesselLoad:{0} ({1})" , newvessel.name , newvessel.id);
            if (newvessel.id != FlightGlobals.ActiveVessel.id || !Utilities.ValidVslType(newvessel))
            {
                Utilities.Log_Debug("AYController newvessel is not active vessel, or not valid type for AmpYear");
                return;
            }

            // otherwise we load the vessel settings
            Currentvesselid = newvessel.id;
            LoadVesselSettings(newvessel);
        }

        private void OnVesselChange(Vessel newvessel)
        {
            Utilities.Log_Debug("AYController onVesselChange New:{0} ({1}) Old:{2}" , newvessel.name , newvessel.id, Currentvesselid);
            Utilities.Log_Debug("AYController active vessel " + FlightGlobals.ActiveVessel.id);
            if (Currentvesselid == newvessel.id) // which would be the case if it's an EVA kerbal re-joining ship
                return;
            double currentTime = Planetarium.GetUniversalTime();
            if (KKPresent)
            {
                KKonVslChng();
            }

            // Update Old Vessel settings into Dictionary
            VesselInfo oldvslinfo = new VesselInfo(newvessel.name, currentTime);
            if (AYgameSettings.KnownVessels.TryGetValue(Currentvesselid, out oldvslinfo))
            {
                UpdateVesselInfo(oldvslinfo);
                AYgameSettings.KnownVessels[Currentvesselid] = oldvslinfo;
                Utilities.Log_Debug("Updated old vessel {0} ({1})" , AYgameSettings.KnownVessels[Currentvesselid].VesselName , Currentvesselid);
            }
            // load the settings for the newvessel
            Currentvesselid = newvessel.id;
            LoadVesselSettings(newvessel);
        }

        private void OnCrewBoardVessel(GameEvents.FromToAction<Part, Part> action)
        {
            Utilities.Log_Debug("AYController onCrewBoardVessel:{0} ({1}) Old:{2} ({3})" , action.to.vessel.name , action.to.vessel.id, action.from.vessel.name , action.from.vessel.id);
            Utilities.Log_Debug("AYController FlightGlobals.ActiveVessel:" + FlightGlobals.ActiveVessel.id);
            Currentvesselid = action.to.vessel.id;
            LoadVesselSettings(action.to.vessel);
        }

        private void LoadVesselSettings(Vessel newvessel)
        {
            double currentTime = Planetarium.GetUniversalTime();
            VesselInfo info = new VesselInfo(newvessel.name, currentTime);
            // Load New Vessel settings from Dictionary
            if (AYgameSettings.KnownVessels.TryGetValue(newvessel.id, out info))
            {
                Utilities.Log_Debug("AYController Vessel Loading Settings: {0} ({1})" , newvessel.name , newvessel.id);
                for (int i = 0; i < Enum.GetValues(typeof(Subsystem)).Length; i++)
                {
                    _subsystemToggle[i] = info.SubsystemToggle[i];
                    _subsystemDrain[i] = info.SubsystemDrain[i];
                }
                for (int i = 0; i < Enum.GetValues(typeof(GUISection)).Length; i++)
                    _guiSectionEnableFlag[i] = info.GuiSectionEnableFlag[i];
                _managerEnabled = info.ManagerEnabled;
                _showCrew = info.ShowCrew;
                EmgcyShutActive = info.EmgcyShutActive;
                _timeLastElectricity = info.TimeLastElectricity;
                _lastUpdate = info.LastUpdate;

                if (KKPresent)
                {
                    KKLoadVesselSettings(info, false);
                }
                EmgcyShutOverride = info.EmgcyShutOverride;
                EmgcyShutOverrideStarted = info.EmgcyShutOverrideStarted;
                Emergencypowerdownactivated = info.Emergencypowerdownactivated;
                Emergencypowerdownreset = info.Emergencypowerdownreset;
                Emergencypowerdownprevactivated = info.Emergencypowerdownprevactivated;
                ESPPriorityHighProcessed = info.ESPPriorityHighProcessed;
                ESPPriorityMediumProcessed = info.ESPPriorityMediumProcessed;
                ESPPriorityLowProcessed = info.ESPPriorityLowProcessed;
                _espPriority = info.EspPriority;
                _reenableRcs = info.ReenableRcs;
                _reenableSas = info.ReenableSas;
                //DbgListVesselInfo(info);
            }
            else //New Vessel not found in Dictionary so set default
            {
                if (!Utilities.ValidVslType(newvessel))
                {
                    Utilities.Log("Not a vessel type AmpYear is interested in");
                    return;
                }
                Utilities.Log_Debug("AYController Vessel Setting Default Settings");
                for (int i = 0; i < Enum.GetValues(typeof(Subsystem)).Length; i++)
                {
                    _subsystemToggle[i] = false;
                    _subsystemDrain[i] = 0.0;
                }

                for (int i = 0; i < Enum.GetValues(typeof(GUISection)).Length; i++)
                    _guiSectionEnableFlag[i] = false;
                _managerEnabled = true;
                _showCrew = false;
                EmgcyShutActive = false;
                _timeLastElectricity = 0f;
                if (KKPresent)
                {
                    KKLoadVesselSettings(info, true);
                }
            }
            if (!KKPresent) //KabinKraziness not present turn off settings for KabinKraziness on vessel
            {
                _subsystemToggle[3] = false;
                _subsystemToggle[4] = false;
                _subsystemToggle[5] = false;
                _subsystemDrain[3] = 0.0;
                _subsystemDrain[4] = 0.0;
                _subsystemDrain[5] = 0.0;
                _guiSectionEnableFlag[2] = false;
            }
            EmgcyShutOverride = false;
            EmgcyShutOverrideStarted = false;
            Emergencypowerdownactivated = false;
            Emergencypowerdownreset = false;
            Emergencypowerdownprevactivated = false;
            Emergencypowerdownresetprevactivated = false;
            ESPPriorityHighProcessed = false;
            ESPPriorityMediumProcessed = false;
            ESPPriorityLowProcessed = false;
            ESPPriorityHighResetProcessed = false;
            ESPPriorityMediumResetProcessed = false;
            ESPPriorityLowResetProcessed = false;
            _espPriority = ESPPriority.LOW;
            _reenableRcs = false;
            _reenableSas = false;
        }
        
        #endregion VesselFunctions

        #region SubSystemFunctions

        //Subsystem Functions Follow

        private bool TimewarpIsValid
        {
            get
            {
                return TimeWarp.CurrentRateIndex < 7;
            }
        }

        private double managerActiveDrain
        {
            get
            {
                return MANAGER_ACTIVE_DRAIN;
            }
        }

        private bool ManagerIsActive
        {
            get
            {
                return TimewarpIsValid && _managerEnabled && (hasPower || HasReservePower);
                //return managerEnabled && (hasPower || hasReservePower);
            }
        }

        private double ManagerCurrentDrain
        {
            get
            {
                if (ManagerIsActive)
                    return managerActiveDrain;
                else
                    return 0.0;
            }
        }

        private static string SubsystemName(Subsystem subsystem)
        {
            switch (subsystem)
            {
                case Subsystem.POWER_TURN:
                    return "Turn Booster";

                case Subsystem.SAS:
                    return "SAS";

                case Subsystem.RCS:
                    return "RCS";

                case Subsystem.CLIMATE:
                    return "Climate Control";

                case Subsystem.MUSIC:
                    return "Smooth Jazz";

                case Subsystem.MASSAGE:
                    return "Massage Chair";

                default:
                    return String.Empty;
            }
        }

        private bool SubsystemEnabled(Subsystem subsystem)
        {
            Vessel cv = FlightGlobals.ActiveVessel;
            switch (subsystem)
            {
                case Subsystem.SAS:
                    return cv.ActionGroups[KSPActionGroup.SAS];

                case Subsystem.RCS:
                    return cv.ActionGroups[KSPActionGroup.RCS];

                default:
                    return _subsystemToggle[(int)subsystem];
            }
        }

        private void SetSubsystemEnabled(Subsystem subsystem, bool enabled)
        {
            Vessel cv = FlightGlobals.ActiveVessel;
            switch (subsystem)
            {
                case Subsystem.SAS:
                    if (cv.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.StabilityAssist))
                        cv.ActionGroups.SetGroup(KSPActionGroup.SAS, enabled);
                    else
                        ScreenMessages.PostScreenMessage(cv.vesselName + " - Cannot Engage SAS - Autopilot function not available", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                case Subsystem.RCS:
                    cv.ActionGroups.SetGroup(KSPActionGroup.RCS, enabled);
                    break;

                default:
                    _subsystemToggle[(int)subsystem] = enabled;
                    break;
            }
        }
        
        private bool SubsystemActive(Subsystem subsystem)
        {
            if (!SubsystemEnabled(subsystem))
                return false;

            switch (subsystem)
            {
                //case Subsystem.CLIMATE:
                //    return totalClimateParts < crewablePartList.Count;

                default:
                    return true;
            }
        }

        private double SubsystemCurrentDrain(Subsystem subsystem)
        {
            if (subsystem == Subsystem.SAS)
            {
                if (_sasPwrDrain > 0) return SubsystemActiveDrain(subsystem) + _sasPwrDrain;
            }

            if (!SubsystemActive(subsystem) || !ManagerIsActive || !hasPower)
                return 0.0;

            switch (subsystem)
            {
                case Subsystem.RCS:
                    if (currentRCSThrust > 0.0f)
                        return SubsystemActiveDrain(subsystem);
                    else
                        return 0.0;

                //case Subsystem.POWER_TURN:
                //    return turningFactor * subsystemActiveDrain(subsystem);

                default:
                    return SubsystemActiveDrain(subsystem);
            }
        }

        private double SubsystemActiveDrain(Subsystem subsystem)
        {
            switch (subsystem)
            {
                case Subsystem.SAS:
                    return SAS_BASE_DRAIN;

                case Subsystem.RCS:
                    return RCS_DRAIN + currentPoweredRCSDrain;
                //return RCS_DRAIN;

                case Subsystem.POWER_TURN:
                    return SasAdditionalRotPower * POWER_TURN_DRAIN_FACTOR;

                case Subsystem.CLIMATE:
                    if (KKPresent)
                    {
                        double clmtrate = KKClimateActDrain();
                        return clmtrate;
                    }
                    else
                        return 0.0;

                case Subsystem.MUSIC:
                    if (Utilities.GameModeisFlight)
                        return 1.0 * crewablePartList.Count;
                    else
                        return 1.0;

                case Subsystem.MASSAGE:
                    if (KKPresent)
                    {
                        double msgrate = KKMassActDrain();
                        return msgrate;
                    }
                    else
                        return 0.0;

                default:
                    return 0.0;
            }
        }

        private bool SubsystemPowered(Subsystem subsystem)
        {
            return hasPower && ManagerIsActive && SubsystemActive(subsystem);
        }

        #endregion SubSystemFunctions

        #region ResourceFunctions

        //Resources Functions Follow
        
        private void TransferReserveToMain(double amount)
        {
            Part cvp = FlightGlobals.ActiveVessel.rootPart;

            if (amount > TotalReservePower * RECHARGE_OVERFLOW_AVOID_FACTOR)
                amount = TotalReservePower * RECHARGE_OVERFLOW_AVOID_FACTOR;

            if (amount > TotalElectricChargeCapacity - TotalElectricCharge)
                amount = TotalElectricChargeCapacity - TotalElectricCharge;

            double received = Utilities.RequestResource(cvp, RESERVE_POWER_NAME, amount);

            int transferAttempts = 0;
            while (received > 0.0 && transferAttempts < Utilities.MAX_TRANSFER_ATTEMPTS)
            {
                received += cvp.RequestResource(MAIN_POWER_NAME, -received);
                transferAttempts++;
            }
        }

        private void TransferMainToReserve(double amount)
        {
            Part cvp = FlightGlobals.ActiveVessel.rootPart;
            if (amount > TotalElectricCharge * RECHARGE_OVERFLOW_AVOID_FACTOR)
                amount = TotalElectricCharge * RECHARGE_OVERFLOW_AVOID_FACTOR;

            if (amount > TotalReservePowerCapacity - TotalReservePower)
                amount = TotalReservePowerCapacity - TotalReservePower;

            double received = Utilities.RequestResource(cvp, MAIN_POWER_NAME, amount);

            int transferAttempts = 0;
            while (received > 0.0 && transferAttempts < Utilities.MAX_TRANSFER_ATTEMPTS)
            {
                received += cvp.RequestResource(RESERVE_POWER_NAME, -received);
                transferAttempts++;
            }
        }

        #endregion ResourceFunctions

        #region Savable

        //Class Load and Save of global settings
        public void Load(ConfigNode globalNode)
        {
            Utilities.Log_Debug("AYController Load");
            _fwindowPos.x = AYsettings.FwindowPosX;
            _fwindowPos.y = AYsettings.FwindowPosY;
            _ewindowPos.x = AYsettings.EwindowPosX;
            _ewindowPos.y = AYsettings.EwindowPosY;
            _epLwindowPos.x = AYsettings.EPLwindowPosX;
            _epLwindowPos.y = AYsettings.EPLwindowPosY;
            Utilities.Log_Debug("AYController Load end");
        }

        public void Save(ConfigNode globalNode)
        {
            Utilities.Log_Debug("AYController Save");
            AYsettings.FwindowPosX = _fwindowPos.x;
            AYsettings.FwindowPosY = _fwindowPos.y;
            AYsettings.EwindowPosX = _ewindowPos.x;
            AYsettings.EwindowPosY = _ewindowPos.y;
            AYsettings.EPLwindowPosX = _epLwindowPos.x;
            AYsettings.EPLwindowPosY = _epLwindowPos.y;
            Utilities.Log_Debug("AYController Save end");
        }

        #endregion Savable

        #region KabinKrazinessInterfaces

        private double KKClimateActDrain()
        {
            //KabinKraziness Interface to Calculate the Climate Control Electrical Drain amount
            if (KKWrapper.APIReady)
            {
                if (Utilities.GameModeisFlight)
                    return CLIMATE_HEAT_RATE
                           * (crewablePartList.Count * KKWrapper.KKactualAPI.CLMT_BSE_DRN_FTR + CLIMATE_CAPACITY_DRAIN_FACTOR 
                           * FlightGlobals.ActiveVessel.GetCrewCapacity());
                else
                    return CLIMATE_HEAT_RATE
                          * (crewablePartList.Count * KKWrapper.KKactualAPI.CLMT_BSE_DRN_FTR + CLIMATE_CAPACITY_DRAIN_FACTOR);
            }
            else
            {
                return 0;
            }
            
        }

        private double KKMassActDrain()
        {
            //KabinKraziness Interface to Calculate the Massage Chairs Electrical Drain amount
            if (KKWrapper.APIReady)
            {
                if (Utilities.GameModeisFlight)
                    return KKWrapper.KKactualAPI.MSG_BSE_DRN_FTR * FlightGlobals.ActiveVessel.GetCrewCount();
                else return KKWrapper.KKactualAPI.MSG_BSE_DRN_FTR;
            }
            else
            {
                return 0;
            }
        }

        private void KKAutopilotChk(Vessel vessel)
        {
            //KabinKraziness Interface to check if the crew have Disabled the SAS
            if (KKWrapper.APIReady)
            {
                if ((vessel.ActionGroups[KSPActionGroup.SAS] && !SubsystemPowered(Subsystem.SAS)) || KKWrapper.KKactualAPI.AutoPilotDisabled)
                {
                    Utilities.Log("KKSASChk SAS - disabled.");
                    //ScreenMessages.PostScreenMessage(cv.vesselName + " - SAS must be enabled through AmpYear first", 10.0f, ScreenMessageStyle.UPPER_CENTER);
                    //Disable SAS when the subsystem isn't powered
                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                    _reenableSas = true;
                }
            }
            
        }

        private void KKSASChk()
        {
            //KabinKraziness Interface to check if the crew haven't disabled the SAS before re-enabling it
            if (KKWrapper.APIReady)
            {
                if (_reenableSas && !KKWrapper.KKactualAPI.AutoPilotDisabled)
                {
                    Utilities.Log("KKSASChk SAS - enabled.");
                    SetSubsystemEnabled(Subsystem.SAS, true);
                    _reenableSas = false;
                }
            }
        }

        private void KKLoadVesselSettings(VesselInfo info, bool isnew)
        {
            //KabinKraziness Interface to Load Vessel values for KabinKraziness, as they are stored in the vessel settings for AmpYear rather than separately.
            if (KKWrapper.APIReady)
            {
                if (isnew)
                {
                    KKWrapper.KKactualAPI.AutoPilotDisabled = false;
                    KKWrapper.KKactualAPI.autoPilotDisTime = 0f;
                    KKWrapper.KKactualAPI.autoPilotDisCounter = 0f;
                }
                else
                {
                    KKWrapper.KKactualAPI.AutoPilotDisabled = info.AutoPilotDisabled;
                    KKWrapper.KKactualAPI.autoPilotDisCounter = info.AutoPilotDisCounter;
                    KKWrapper.KKactualAPI.autoPilotDisTime = info.AutoPilotDisTime;
                }
            }
        }

        private void KKonVslChng()
        {
            //KabinKraziness Interface on Vessel change, to remove autopilot disabled countdown if it is active
            //KabinKraziness.Ikkaddon _KK = KKClient.GetKK();
            //if (_KK.AutoPilotDisabled) ScreenMessages.RemoveMessage(_KK.AutoTimer);
            if (KKWrapper.APIReady)
            {
                if (KKWrapper.KKactualAPI.AutoPilotDisabled)
                    ScreenMessages.RemoveMessage(KKWrapper.KKactualAPI.AutoTimer);
            }
        }

        private void KKUpdateVslInfo(VesselInfo vesselInfo)
        {
            //KabinKraziness Interface to Save Vessel values for KabinKraziness, as they are stored in the vessel settings for AmpYear rather than separately.
            //KabinKraziness.Ikkaddon _KK = KKClient.GetKK();
            //vesselInfo.AutoPilotDisabled = _KK.AutoPilotDisabled;
            //vesselInfo.AutoPilotDisCounter = _KK.AutoPilotDisCounter;
            //vesselInfo.AutoPilotDisTime = _KK.AutoPilotDisTime;
            if (KKWrapper.APIReady)
            {
                vesselInfo.AutoPilotDisabled = KKWrapper.KKactualAPI.AutoPilotDisabled;
                vesselInfo.AutoPilotDisCounter = KKWrapper.KKactualAPI.autoPilotDisCounter;
                vesselInfo.AutoPilotDisTime = KKWrapper.KKactualAPI.autoPilotDisTime;
            }
        }

        private void KKKrazyWrngs()
        {
            //KabinKraziness Interface to display Kraziness Warnings in the AmpYear GUI
            if (KKWrapper.APIReady)
            {
                if (KKWrapper.KKactualAPI.firstMajCrazyWarning)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Craziness Major Alert!", Textures.AlertStyle);
                    GUILayout.EndHorizontal();
                }
                else if (KKWrapper.KKactualAPI.firstMinCrazyWarning)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Craziness Minor Alert!", Textures.WarningStyle);
                    GUILayout.EndHorizontal();
                }
            }
        }

        #endregion KabinKrazinessInterfaces

        #region BodyDarkness

        //Calculate the darkness period for a body based on a roughly circular orbit with Ap = apoapsis in Km
        private double CalculatePeriod(CelestialBody body, double Ap)
        {
            double returnPeriod = 0d;
            double rA = body.Radius / 1000 + Ap;
            double GM = body.gMagnitudeAtCenter / 1000000000;
            double h = Math.Sqrt(rA * GM);
            returnPeriod = 2 * (rA * rA) / h * Math.Asin(body.Radius / 1000 / rA);
            return returnPeriod;
        }

        #endregion BodyDarkness
    }
}