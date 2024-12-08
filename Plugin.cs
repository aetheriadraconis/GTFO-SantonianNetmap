using AK;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

using Player;
using UnityEngine;
using Localization;
using LevelGeneration;

namespace Santonian
{
    using ItemInLevel_MarkerContext             = MarkerContext<ItemInLevel>;

    using Terminal_MarkerContext                = MarkerContext<LG_ComputerTerminal>;
    using HSU_MarkerContext                     = MarkerContext<LG_HSU>;
    using Container_MarkerContext               = MarkerContext<LG_ResourceContainer_Storage>;
    using Door_MarkerContext                    = MarkerContext<LG_WeakDoor>;
    using SecurityDoor_MarkerContext            = MarkerContext<LG_SecurityDoor>;
    using BulkheadDC_MarkerContext              = MarkerContext<LG_BulkheadDoorController_Core>;
    using PowerGenerator_MarkerContext          = MarkerContext<LG_PowerGenerator_Core>;

    public static class Config
    {
        public const string command             = "NMAP";
        public const string commandDescription  = "Map resources in the current zone";

        public const float  markerIconScale     = 0.6f;
        public const float  consumableIconScale = 0.4f;
        public const float  markerAlpha         = 0.4f;

        public const float  baseScanTime        = 2.0f;
        public const float  itemScanTime        = 0.5f;

        public const float  resetTime           = 2.0f;

        public const int    markerFadeOutDelay  = 10;

        public const int    defaultTimeout      = 60;
        public const int    minTimeout          = 30;
        public const int    maxTimeout          = 600;

        public const string outputFormat        = "{0,-34} {1,-34} {2,-24}";
        public const string exOutputFormat      = "{0,-34} {1,-34} {2,-24} {3, -34}";

        // Debug option, allows scan for resource packs, consumables and key items outside of the current zone
        public const bool   externalScan        = false;
    }

    [BepInPlugin("com.aetheria.santonian.netmap", "Netmap", "1.0.1")]
    public class Netmap : BasePlugin
    {
        // During plugin initialization we're building a full list of items of interest that are present in
        // level and store them in the following lists
        public static readonly List<ItemInLevel>                    __resourceItemsInLevelList      = new();
        public static readonly List<ItemInLevel>                    __keyItemsInLevelList           = new();
        public static readonly List<ConsumablePickup_Core>          __consumableItemsInLevelList    = new();

        public static readonly List<LG_ComputerTerminal>            __terminalsInLevelList          = new();
        public static readonly List<LG_HSU>                         __hsuInLevelList                = new();
        public static readonly List<LG_BulkheadDoorController_Core> __bulkheadsInLevelList          = new();
        public static readonly List<LG_PowerGenerator_Core>         __generatorsInLevelList         = new();

        public static readonly List<LG_ResourceContainer_Storage>   __containersInLevelList         = new();
        public static readonly List<LG_WeakDoor>                    __doorsInLevelList              = new();
        public static readonly List<LG_SecurityDoor>                __securityDoorsInLevelList      = new();


        // These are marker context lists that represent active markers of each type. Active markers match
        // user input query and are generally visible to the player, except for the short time frame when the
        // "Initializing Santonian Holomap" message is displayed in the terminal, when the markers are already
        // present, but are hidden
        public static readonly List<ItemInLevel_MarkerContext>      resourceMarkersList             = new();
        public static readonly List<ItemInLevel_MarkerContext>      keyItemMarkersList              = new();
        public static readonly List<ItemInLevel_MarkerContext>      consumableMarkersList           = new();

        public static readonly List<Terminal_MarkerContext>         terminalMarkersList             = new();
        public static readonly List<HSU_MarkerContext>              hsuMarkersList                  = new();
        public static readonly List<BulkheadDC_MarkerContext>       bulkheadMarkersList             = new();
        public static readonly List<PowerGenerator_MarkerContext>   generatorMarkersList            = new();

        public static readonly List<Container_MarkerContext>        containerMarkersList            = new();
        public static readonly List<Door_MarkerContext>             doorMarkersList                 = new();
        public static readonly List<SecurityDoor_MarkerContext>     securityDoorMarkersList         = new();


        // Permanent marker context lists that survive subsequent NMAP entries and can be removed only by
        // running "NMAP -R" are available only for stationary level objects
        public static readonly List<Terminal_MarkerContext>         permTerminalMarkersList         = new();
        public static readonly List<HSU_MarkerContext>              permHsuMarkersList              = new();
        public static readonly List<BulkheadDC_MarkerContext>       permBulkheadMarkersList         = new();
        public static readonly List<PowerGenerator_MarkerContext>   permGeneratorMarkersList        = new();

        public static readonly List<Container_MarkerContext>        permContainerMarkersList        = new();
        public static readonly List<Door_MarkerContext>             permDoorMarkersList             = new();
        public static readonly List<SecurityDoor_MarkerContext>     permSecurityDoorMarkersList     = new();

        // Lock used to synchronize all ItemInLevel marker lists access. ItemInLevel objects are special, as
        // they can be picked up and handled by players, so the lock time should be absolutely minimal
        public static readonly object __itemInLevelMarkersLock                                      = new();

        // Lock used to synchronize all stationary objects marker lists access. LG_ComputerTerminal, LG_HSU,
        // LG_BulkheadDoorController_core, LG_PowerGenerator_Core, LG_ResourceContainer_Storage, LG_WeakDoor,
        // LG_SecurityDoor
        public static readonly object __objectsInLevelMarkersLock                                   = new();



        public Harmony? HarmonyInstance { get; private set; }

        public static void Initialize()
        {
            Logger.Info("Santonian Holographic Netmap initialization");

            // Clear all item and markers lists, as they persist between consequtive rundowns within single game session
            __resourceItemsInLevelList.Clear();     resourceMarkersList.Clear();
            __keyItemsInLevelList.Clear();          keyItemMarkersList.Clear();
            __consumableItemsInLevelList.Clear();   consumableMarkersList.Clear();

            __terminalsInLevelList.Clear();         terminalMarkersList.Clear();        permTerminalMarkersList.Clear();
            __hsuInLevelList.Clear();               hsuMarkersList.Clear();             permHsuMarkersList.Clear();
            __bulkheadsInLevelList.Clear();         bulkheadMarkersList.Clear();        permBulkheadMarkersList.Clear();
            __generatorsInLevelList.Clear();        generatorMarkersList.Clear();       permGeneratorMarkersList.Clear();

            __containersInLevelList.Clear();        containerMarkersList.Clear();       permContainerMarkersList.Clear();
            __doorsInLevelList.Clear();             doorMarkersList.Clear();            permDoorMarkersList.Clear();
            __securityDoorsInLevelList.Clear();     securityDoorMarkersList.Clear();    permSecurityDoorMarkersList.Clear();


            foreach (ItemInLevel item in UnityEngine.Object.FindObjectsOfType<ItemInLevel>()) {

                if (item == null)       { continue; }

                LG_GenericTerminalItem terminalItem = item.GetComponentInChildren<LG_GenericTerminalItem>();
                if (terminalItem == null)  { continue; }

                string name = terminalItem.TerminalItemKey;
                if (name == null)       { continue; }

                if (name.StartsWith("KEY_")     || name.StartsWith("ID_")       || name.StartsWith("BULKHEAD_KEY_") ||
                    name.StartsWith("CELL_")    || name.StartsWith("OSIP_")     || name.StartsWith("FOG_TURBINE")   ||
                    name.StartsWith("GLP-2_")   || name.StartsWith("PD_")       || name.StartsWith("DATA_CUBE_")    ||
                    name.StartsWith("HDD_")     || name.StartsWith("PLANT_")) {

                    __keyItemsInLevelList.Add(item);

                } else if (name.Contains("PACK") || name.StartsWith("TOOL_REFILL")) {
                    __resourceItemsInLevelList.Add(item);
                }
            }

            foreach (ConsumablePickup_Core item in UnityEngine.Object.FindObjectsOfType<ConsumablePickup_Core>()) {

                if (item != null) {
                    __consumableItemsInLevelList.Add(item);
                }
            }

            foreach (LG_ComputerTerminal terminal in UnityEngine.Object.FindObjectsOfType<LG_ComputerTerminal>()) {

                if (terminal != null) {
                    __terminalsInLevelList.Add(terminal);
                }
            }

            foreach (LG_HSU hsu in UnityEngine.Object.FindObjectsOfType<LG_HSU>()) {

                if (hsu != null) {
                    __hsuInLevelList.Add(hsu);
                }
            }

            foreach (LG_BulkheadDoorController_Core bulkhead in UnityEngine.Object.FindObjectsOfType<LG_BulkheadDoorController_Core>()) {

                if (bulkhead != null) {
                    __bulkheadsInLevelList.Add(bulkhead);
                }
            }

            foreach (LG_PowerGenerator_Core generator in UnityEngine.Object.FindObjectsOfType<LG_PowerGenerator_Core>()) {

                if (generator != null) {
                    __generatorsInLevelList.Add(generator);
                }
            }

            foreach (LG_ResourceContainer_Storage container in UnityEngine.Object.FindObjectsOfType<LG_ResourceContainer_Storage>()) {

                if (container != null) {

                    LG_GenericTerminalItem terminalItem = container.GetComponentInChildren<LG_GenericTerminalItem>();
                    if (terminalItem != null && terminalItem.TerminalItemKey != null)  {
                        __containersInLevelList.Add(container);
                    }
                }
            }

            foreach (LG_WeakDoor door in UnityEngine.Object.FindObjectsOfType<LG_WeakDoor>()) {

                if (door != null) {
                    __doorsInLevelList.Add(door);
                }
            }

            foreach (LG_SecurityDoor door in UnityEngine.Object.FindObjectsOfType<LG_SecurityDoor>()) {

                if (door != null) {
                    __securityDoorsInLevelList.Add(door);
                }
            }
        }

        public override void Load() {
            Logger.Info("Santonian netmap enabled!");

            HarmonyInstance = new Harmony("com.aetheria.santonian.netmap");
            HarmonyInstance.PatchAll();
            LG_Factory.OnFactoryBuildDone += (Il2CppSystem.Action)Netmap.Initialize;
        }
    }

    public class MarkerContext<T> : IComparable<MarkerContext<T>> where T : UnityEngine.Component
    {
        public T item { get; }

        public string               name        { get; }
        public string               title       { get; }
        public eNavMarkerStyle      style       { get; }
        public int                  timeout     { get; }
        public float                iconScale   { get; }
        public UnityEngine.Color    color       { get; }
        public bool                 permanent   { get; }

        private NavMarker?          marker      = null;

        public MarkerContext(T _item, string _name, string _title, eNavMarkerStyle _style, int _timeout, float _iconScale, UnityEngine.Color _color, bool _permanent = false)
        {
            item = _item;
            title = _title;
            name = _name;
            style = _style;
            timeout = _permanent ? Int32.MaxValue : _timeout;
            iconScale = _iconScale;
            color = _color;
            permanent = _permanent;
        }

        public bool IsActive() { return marker != null; }

        public int CompareTo(MarkerContext<T>? lhs) {
            if (lhs == null) { return 1; }

            return string.Compare(name, lhs.name, StringComparison.Ordinal);
        }

        public void Enable()
        {
            if (marker != null) { return; }

            Logger.Debug(" ** enabling " + name + " navigation marker");

            marker = GuiManager.NavMarkerLayer.PrepareGenericMarker(item.gameObject);

            marker.SetTitle(title);
            marker.SetStyle(style);
            marker.SetColor(color);
            marker.SetIconScale(iconScale);
            marker.SetAlpha(Config.markerAlpha);
            marker.SetVisible(true);

            marker.FadeOutOverTime(timeout - Config.markerFadeOutDelay, Config.markerFadeOutDelay);

            marker.PersistentBetweenRestarts = permanent;

            marker.m_fadeRoutine = CoroutineManager.StartCoroutine(GuiManager.NavMarkerLayer.FadeMarkerOverTime
                (marker, marker.name, UnityEngine.Random.Range(0.1f, 0.5f), timeout, false), null);
        }

        public void Disable()
        {
            if (marker == null) { return; }

            Logger.Debug(" ** disabling " + name + " navigation marker");

            marker.SetVisible(false);
            marker.SetState(NavMarkerState.Inactive);

            CoroutineManager.StopCoroutine(marker.m_fadeRoutine);
        }

        public void Remove()
        {
            if (marker == null) { return; }

            Disable();

            GuiManager.NavMarkerLayer.RemoveMarker(marker);
            marker = null;
        }
    }

    public class NavigationMarkerPatch
    {
        private static void RemoveMarkerFromList(ItemInLevel __instance, List<ItemInLevel_MarkerContext> __list)
        {
            foreach (ItemInLevel_MarkerContext marker in __list) {

                if (__instance == marker.item) {

                    marker.Remove();
                    Netmap.resourceMarkersList.Remove(marker);

                    break;
                }
            }
        }

        public static void RemoveNavigationMarker(ItemInLevel __instance)
        {
            Logger.Debug(" ** removing navigation marker for " + __instance.PublicName);

            lock(Netmap.__itemInLevelMarkersLock) {
                RemoveMarkerFromList(__instance, Netmap.resourceMarkersList);
                RemoveMarkerFromList(__instance, Netmap.keyItemMarkersList);
                RemoveMarkerFromList(__instance, Netmap.consumableMarkersList);
            }
        }
    }

    [HarmonyPatch(typeof(ItemInLevel), nameof(ItemInLevel.OnPickedUp))]
    public class ItemInLevel_OnPickUp : NavigationMarkerPatch
    {
        static void Postfix(ItemInLevel __instance, PlayerAgent player, InventorySlot slot, AmmoType ammoType) {
            Logger.Debug(" ## ItemInLevel.OnPickedUp");
            RemoveNavigationMarker(__instance);
        }
    }

    [HarmonyPatch(typeof(KeyItemPickup_Core), nameof(KeyItemPickup_Core.OnInteractionPickUp))]
    public class KeyItem_OnPickUp : NavigationMarkerPatch
    {
        static void Postfix(ItemInLevel __instance, PlayerAgent player) {
            Logger.Debug(" ## KeyItemPickup_Core.OnInteractionPickUp");
            RemoveNavigationMarker(__instance);
        }
    }

    [HarmonyPatch(typeof(LG_ComputerTerminalCommandInterpreter), nameof(LG_ComputerTerminalCommandInterpreter.SetupCommands))]
    public class NetmapPatch
    {
        static void Postfix(LG_ComputerTerminalCommandInterpreter __instance) {
            __instance.AddCommand(TERM_Command.InvalidCommand, Config.command, new LocalizedText {
                UntranslatedText = Config.commandDescription,
                Id = 0u
            },TERM_CommandRule.Normal);
        }
    }

    [HarmonyPatch(typeof(LG_ComputerTerminalCommandInterpreter), nameof(LG_ComputerTerminalCommandInterpreter.ReceiveCommand))]
    public class NetmapOverridePatch
    {
/*
        \\Root\PING
        ERROR: No item found with name

        USAGE: PING [NAME] [-T]

        Attributes:
        [NAME]          Name of item
        [-T]            Optional attribute. Pings the specified item until stopped. To stop - input Control-C

        Example: PING KEY_123 -T

        Note that this terminal ONLY can locate items in the current zone: <b> ZONE_49 </b>


        PING FAILURE: Out of range. HSU_### is not in the same security zone as this terminal
        Are you in correct security zone? Use command QUERY HSU_### for more information about the item


        \\Root\QUERY

        Error: Missing parameter

        Usage: QUERY [ID]

        Attributes:
        [ID]            The ID of the item

        Example: QUERY HSU_123


        \\Root\LIST
        WARNING: Running LIST with no filter is not recommended. Use -A to override

        Usage: LIST [FILTERS]

        Attributes:
        [FILTERS]       One or more items to get a smaller list of items
        [-A]            Optional attribute. Lists all items

        Example: LIST HSU ZONE_1, will only display data from Zone_1 containing 'HSU'
*/

        private static void Usage(LG_ComputerTerminalCommandInterpreter __instance, int currentZone, string errorMessage = "")
        {
            __instance.AddOutput(errorMessage);

            __instance.AddOutput("WARNING: Running NMAP with no filter is not recommended. Use -A to override");

            __instance.AddOutput("Usage: NMAP [-A] [-P|-T sec] [-R] [FILTERS] ZONE");

            __instance.AddOutput("Attributes:", false);
            __instance.AddOutput("[-P]           Optional attribute. Assigns permanent navigation marker to the stationary object", false);
            __instance.AddOutput("[-T sec]       Optional attribute. Sets expiration time for navigation marker in range [30..600] seconds");

            __instance.AddOutput("[-A]           Optional attribute. Maps all identifiable objects in the current zone (NOT RECOMMENDED)");

            __instance.AddOutput("[-R]           Optional attribute. Removes all permanent navigation markers");

            __instance.AddOutput("[FILTERS]      One or more regular items, groups of items or stationary objects to map", false);
            __instance.AddOutput("ZONE           Security zone to limit mapping operation to");

            __instance.AddOutput("Examples:", false);
            __instance.AddOutput("  NMAP -T 120 RES CON ZONE_123    Map resource packs and consumables in the current zone for 2 minutes", false);
            __instance.AddOutput("  NMAP -T 30 FOAM ZONE_123        Map C-FOAM grenades and mines in the current zone for 30 seconds");

            __instance.AddOutput("  NMAP -T 600 CELL_756 ZONE_123   Assign navigation marker to CELL_756 for 10 minutes in the current zone");

            __instance.AddOutput("  NMAP -P TERMINAL_356 E_606      Assign permanent navigation marker to TERMINAL_356 in ZONE_606");

            __instance.AddOutput("This terminal can locate and map large stationary objects, security doors, hydrostasis units, generators, " +
                                 "bulkhead door controllers and other terminals outside of the current security zone");

            __instance.AddOutput("Note that this terminal ONLY can map resource packs, tools and consumables in the current zone: " +
                                 "<b>ZONE_" + currentZone + "</b>");
        }

        private static bool ParseCommandLine(LG_ComputerTerminalCommandInterpreter __instance, string inputLine,
                                             out int timeout,
                                             out bool verboseScan,
                                             out bool permanentMarker,
                                             out bool resetMarkers,
                                             out List<string> argsList,
                                             out int scanZone,
                                             out string scanZoneName)
        {
            int currentZone = __instance.m_terminal.SpawnNode.m_zone.NavInfo.Number;

            // Split the input into arguments
            string[] args = inputLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Initialize default timeout and parse other parameters
            timeout = Config.defaultTimeout;
            argsList = new();

            verboseScan = false;

            permanentMarker = false;
            resetMarkers = false;

            // Init scan zone to the current zone values
            scanZone = currentZone;
            scanZoneName = "ZONE_" + currentZone;

            // Check the minimum arguments length
            if (args.Length < 2) {
                Usage(__instance, currentZone);
                return false;
            }

            for (int i = 1; i < args.Length; i++) {
                if (args[i].ToUpper() == "-T") {

                    // The last argument shall be ZONE ##, so if there isn't enough arguments in the line, the
                    // user most probably entered something like \\Root\NMAP -T E_49, e.g. missed timeout value
                    if (i + 1 >= args.Length) {

                        Usage(__instance, currentZone, " ## invalid command line. Argument is missing parameter " +
                            "[-T num_of_seconds]");

                        return false;
                    }

                    // args[i + 1] exists and is not the last argument (zone number)
                    if (!int.TryParse(args[i + 1], out int parsedTimeout) ||
                        parsedTimeout < Config.minTimeout ||
                        parsedTimeout > Config.maxTimeout) {

                        Usage(__instance, currentZone, " ## invalid command line. [-T num_of_seconds] must be " +
                            "time-to-live in range [30..600] seconds");

                        return false;
                    }

                    // Assign timeout and skip next argumen in the arguments check
                    timeout = parsedTimeout;
                    i++;

                // All other arguments are items
                } else if (args[i].ToUpper() == "-A") {
                    verboseScan = true;

                } else if (args[i].ToUpper() == "-P") {
                    permanentMarker = true;

                } else if (args[i].ToUpper() == "-R") {
                    resetMarkers = true;

                // It's logical to incorporate this check into for (int i = 1; i < args.Length - 1; i++) { } if we make
                // zone number a mandatory parameter, however it's not needed for NMAP -R
                } else if (i < args.Length - 1) {
                    argsList.Add(args[i]);
                }
            }

            // Reset command-line argument is the only one that doesn't require zone number
            if (resetMarkers) {
                Logger.Debug(" - reset markers: [enabled]");
                return true;
            }

            // If the last argument contains "E_", we assume that this is zone number
            if (!args[args.Length - 1].Contains("E_")) {
                Usage(__instance, currentZone, " ## invalid command line. Last argument shall contain ZONE_##");
                return false;
            }

            string parsedZoneName = args[args.Length - 1];
            int parsedZone = ExtractZoneNumber(parsedZoneName);

            if (parsedZoneName.IsNullOrWhiteSpace() || parsedZone <= 0) {
                Usage(__instance, currentZone, " ## invalid command line. Invalid ZONE_## provided");
                return false;
            }

            // Reformat zone name into full form - e.g. E_49 -> ZONE_49
            scanZone = parsedZone;
            scanZoneName = "ZONE_" + scanZone;

            // Account for priority in case of conflicting arguments
            if (permanentMarker)   { verboseScan = false; }

            return true;
        }

        private static int ExtractZoneNumber(string zoneName)
        {

            int _underscoreIndex = zoneName.IndexOf('_');

            if (_underscoreIndex != -1 && _underscoreIndex < zoneName.Length - 1) {

                string _zoneSubstr = zoneName.Substring(_underscoreIndex + 1);
                if (int.TryParse(_zoneSubstr, out int _zoneNumber)) {
                    return _zoneNumber;
                }
            }

            return 0;
        }

        private static bool PartialMatchOnList(string name, List<string> filterList)
        {
            foreach(string filter in filterList) { if (name.Contains(filter))   { return true; }}
            return false;
        }

        private static bool FullMatchOnList(string name, List<string> filterList)
        {
            foreach(string filter in filterList) { if (name == filter)          { return true; }}
            return false;
        }


        static bool Prefix(LG_ComputerTerminalCommandInterpreter __instance, TERM_Command cmd, string inputLine,
                                                                             string param1, string param2)
        {
            if (!inputLine.StartsWith(Config.command))  { return true; }

            int currentZone = __instance.m_terminal.SpawnNode.m_zone.NavInfo.Number;
            string currentZoneName = "ZONE_" + currentZone;

            __instance.m_terminal.IsWaitingForAnyKeyInLinePause = false;
            __instance.m_linesSinceCommand = 0;

            __instance.AddOutput("\\\\Root\\" + inputLine, false);

            if (!ParseCommandLine(__instance, inputLine,
                                 out int timeout,
                                 out bool verboseScan,
                                 out bool permanentMarker,
                                 out bool resetMarkers,
                                 out List<string> argsList,
                                 out int scanZone,
                                 out string scanZoneName)) {
                return false;
            }


            // Remove all active navigation markers
            RemoveActiveMarkers();

            // Remove all permanent navigation markers if NMAP -R was invoked
            if (resetMarkers) {
                __instance.AddOutput(TerminalLineType.ProgressWait, "Erasing navigation markers cache",
                    Config.resetTime, TerminalSoundType.LineTypeDefault, TerminalSoundType.LineTypeDefault);

                RemovePermanentMarkers();
                return false;
            }


            List<string> filtersList = new();

            bool keyItemsScan = false;
            bool resourcesScan = false;
            bool consumablesScan = false;

            foreach (string filter in argsList) {

                // Minimum shortcut length is 3 symbols, e.g. "RES", "CON", "KEY"; if it's shorter, ignore
                if (filter.Length < 3)                        { continue; }

                else if ("RESOURCES".StartsWith(filter))      { resourcesScan = true;   }
                else if ("CONSUMABLES".StartsWith(filter))    { consumablesScan = true; }

                else                                          { filtersList.Add(filter);      }
            }

            // NMAP -T 120 E_49             - all scans are enabled (default)
            // NMAP -T 120 RES E_49         - only resources scan is enabled (override)
            // NMAP -T 120 CON E_49         - only consumables scan is enabled (override)

            // NMAP -T 120 RES CON E_49     - scan for resources and consumables in ZONE_49, key items are disabled,
            //                                TTL 120 sec
            // NMAP -T 300 RES CON KEY E_50 - scan for resources, consumables and items that have substring "KEY"
            //                                in thei name, ZONE_50, TTL 300 sec
            // NMAP -T 600 CELL DOOR E_49   - all wide area scans are disabled, we're looking for "CELL" and "DOOR"
            //                                in all lists, TTL 600 sec

            // NMAP -P CELL_768 E_710       - assigns permanent navigation marker to the CELL_768 (exact match) in
            //                                ZONE_710

            // Note: no separate "key items" scan key word as the player shall know what are they looking for,
            // KEY, CELL, ID, GLP-2, HDD, FOG_TURBINE, DATA_CUBE etc.

            if (filtersList.Count() == 0 && !resourcesScan && !consumablesScan) {
                keyItemsScan = true;    resourcesScan = true;   consumablesScan = true;
            }

            bool targetZoneScan = (currentZone == scanZone) || Config.externalScan;

            Logger.Debug(" - timeout: " + timeout + " sec");
            Logger.Debug(" - zone: " + scanZone);
            Logger.Debug(" - verbose scan: "     + (verboseScan     ? "[enabled]" : "[disabled]"));
            Logger.Debug(" - permanent marker: " + (permanentMarker ? "[enabled]" : "[disabled]"));
            Logger.Debug(" - key items scan: "   + (keyItemsScan    ? "[enabled]" : "[disabled]"));
            Logger.Debug(" - resources scan: "   + (resourcesScan   ? "[enabled]" : "[disabled]"));
            Logger.Debug(" - consumables scan: " + (consumablesScan ? "[enabled]" : "[disabled]"));
            Logger.Debug(" - items list: ");

            foreach (string item in filtersList) {
                Logger.Debug("     - " + item);
            }

            // Scan for resources
            if (targetZoneScan) foreach (ItemInLevel item in Netmap.__resourceItemsInLevelList) {

                try {
                    if (item.internalSync.GetCurrentState().status == ePickupItemStatus.PickedUp)   { continue; }

                    LG_GenericTerminalItem terminalItem = item.GetComponentInChildren<LG_GenericTerminalItem>();
                    if (!terminalItem.FloorItemLocation.Contains(scanZoneName))                     { continue; }

                    string name = terminalItem.TerminalItemKey;
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingLoot;

                    if (name.StartsWith("MEDIPACK")) {
                        style = eNavMarkerStyle.PlayerPingHealth;

                    } else if (name.StartsWith("AMMOPACK")) {
                        style = eNavMarkerStyle.PlayerPingAmmo;

                    } else if (name.StartsWith("TOOL_REFILL")) {
                        style = eNavMarkerStyle.PlayerPingToolRefill;

                    } else if (name.StartsWith("DISINFECT_PACK")) {
                        style = eNavMarkerStyle.PlayerPingDisinfection;
                    }

                    if (item.GetCustomData().ammo >= 20) {
                        title += "\n(" + item.GetCustomData().ammo/20 + " Uses)";
                    }

                    if (verboseScan || resourcesScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__itemInLevelMarkersLock) {
                            Netmap.resourceMarkersList.Add(new ItemInLevel_MarkerContext(item, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.white));
                        }
                    }

                } catch (Exception e)  {
                    Logger.Error(" ## " + e);
                }
            }

            // Scan for key items
            if (targetZoneScan) foreach (ItemInLevel item in Netmap.__keyItemsInLevelList) {

                try {
                    if (item.internalSync.GetCurrentState().status == ePickupItemStatus.PickedUp)   { continue; }

                    LG_GenericTerminalItem terminalItem = item.GetComponentInChildren<LG_GenericTerminalItem>();
                    string name = terminalItem.TerminalItemKey;

                    if (!terminalItem.FloorItemLocation.Contains(scanZoneName))                     { continue; }

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingLoot;
                    string title = name;

                    if (name.StartsWith("KEY_")) {
                        style = eNavMarkerStyle.PlayerPingPickupObjectiveItem;
                    }

                    if (verboseScan || keyItemsScan || PartialMatchOnList(name, filtersList))    {
                        lock (Netmap.__itemInLevelMarkersLock) {
                            Netmap.keyItemMarkersList.Add(new ItemInLevel_MarkerContext(item, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.white));
                        }
                    }
                } catch (Exception e)  {
                    Logger.Error(" ## " + e);
                }
            }

            if (targetZoneScan) foreach (ConsumablePickup_Core item in Netmap.__consumableItemsInLevelList) {
                try {
                    if (item.internalSync.GetCurrentState().status == ePickupItemStatus.PickedUp)   { continue; }

                    UnityEngine.Color color = UnityEngine.Color.white;

                    LG_Area area = FindParentArea(item);
                    if (area == null || area.m_zone.NavInfo.Number != scanZone)                     { continue; }

                    string name = item.PublicName.ToUpper();
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingCarryItem;
                    if (name == "LONG RANGE FLASHLIGHT") {
                        style = eNavMarkerStyle.PlayerPingLookat;

                    } else if (name == "GLOW STICK") {
                        style = eNavMarkerStyle.PlayerPingConsumable;
                        color = UnityEngine.Color.grey;

                    } else if (name == "C-FOAM GRENADE") {
                        style = eNavMarkerStyle.PlayerPingCarryItem;

                    } else if (name == "EXPLOSIVE TRIP MINE") {
                        style = eNavMarkerStyle.PlayerPingCarryItem;

                    } else if (name == "LOCK MELTER") {
                        style = eNavMarkerStyle.PlayerPingCarryItem;

                    } else if (name == "FOG REPELLER") {
                        style = eNavMarkerStyle.PlayerPingConsumable;
                    }

                    if (verboseScan || consumablesScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__itemInLevelMarkersLock) {
                            Netmap.consumableMarkersList.Add(new ItemInLevel_MarkerContext(item, name, title,
                                style, timeout, Config.markerIconScale, color));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }

            foreach (LG_ComputerTerminal terminal in Netmap.__terminalsInLevelList) {

                try {
                    if (!terminal.m_terminalItem.FloorItemLocation.Contains(scanZoneName))          { continue; }

                    string name = terminal.PublicName.ToUpper();
                    string title = name;

                    UnityEngine.Color color = UnityEngine.Color.green;
                    if (terminal.m_isWardenObjective) {
                        color = UnityEngine.Color.magenta;
                    }

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingTerminal;

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permTerminalMarkersList.Add(new Terminal_MarkerContext(terminal, name, title,
                                style, timeout, Config.markerIconScale, color, permanentMarker));
                        }
                    } else if (verboseScan || keyItemsScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.terminalMarkersList.Add(new Terminal_MarkerContext(terminal, name, title,
                                style, timeout, Config.markerIconScale, color));
                        }
                    }
                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }

            foreach (LG_HSU hsu in Netmap.__hsuInLevelList) {

                try {
                    if (!hsu.m_terminalItem.FloorItemLocation.Contains(scanZoneName))               { continue; }

                    string name = hsu.PublicName.ToUpper();
                    string title = name;

                    UnityEngine.Color color = UnityEngine.Color.grey;
                    if (hsu.m_isWardenObjective) {
                        color = UnityEngine.Color.magenta;
                    }

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingHSU;

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permHsuMarkersList.Add(new HSU_MarkerContext(hsu, name, title,
                                style, timeout, Config.markerIconScale, color, permanentMarker));
                        }
                    } else if (verboseScan || keyItemsScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.hsuMarkersList.Add(new HSU_MarkerContext(hsu, name, title,
                                style, timeout, Config.markerIconScale, color));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }

            foreach (LG_BulkheadDoorController_Core bulkhead in Netmap.__bulkheadsInLevelList) {

                try {
                    if (!bulkhead.m_terminalItem.FloorItemLocation.Contains(scanZoneName))          { continue; }

                    string name = bulkhead.PublicName.ToUpper();
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingBulkheadDC;

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permBulkheadMarkersList.Add(new BulkheadDC_MarkerContext(bulkhead, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.green, permanentMarker));
                        }
                    } else if (verboseScan || keyItemsScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.bulkheadMarkersList.Add(new BulkheadDC_MarkerContext(bulkhead, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.green));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }

            foreach (LG_PowerGenerator_Core generator in Netmap.__generatorsInLevelList) {

                try {
                    if (!generator.m_terminalItem.FloorItemLocation.Contains(scanZoneName))         { continue; }

                    string name = generator.PublicName.ToUpper();
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingGenerator;

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permGeneratorMarkersList.Add(new PowerGenerator_MarkerContext(generator, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.yellow, permanentMarker));
                        }
                    } else if (verboseScan || keyItemsScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.generatorMarkersList.Add(new PowerGenerator_MarkerContext(generator, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.yellow));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }

            foreach (LG_ResourceContainer_Storage container in Netmap.__containersInLevelList) {

                try {
                    LG_GenericTerminalItem terminalItem = container.GetComponentInChildren<LG_GenericTerminalItem>();

                    if (!terminalItem.FloorItemLocation.Contains(scanZoneName))                     { continue; }

                    string name = terminalItem.TerminalItemKey;
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingResourceLocker;
                    if (container.m_storageSlots.Length < 4) {
                        style = eNavMarkerStyle.PlayerPingResourceBox;
                    }

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permContainerMarkersList.Add(new Container_MarkerContext(container, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.grey, permanentMarker));
                        }
                    } else if (verboseScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.containerMarkersList.Add(new Container_MarkerContext(container, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.grey));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }

            foreach (LG_WeakDoor door in Netmap.__doorsInLevelList) {

                try {
                    LG_GenericTerminalItem terminalItem = door.GetComponentInChildren<LG_GenericTerminalItem>();

                    if (!door.m_terminalItem.FloorItemLocation.Contains(scanZoneName))              { continue; }

                    string name = terminalItem.TerminalItemKey;
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingDoor;

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permDoorMarkersList.Add(new Door_MarkerContext(door, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.grey, permanentMarker));
                        }
                    } else if (verboseScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.doorMarkersList.Add(new Door_MarkerContext(door, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.grey));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }


            foreach (LG_SecurityDoor door in Netmap.__securityDoorsInLevelList) {

                try {
                    LG_GenericTerminalItem terminalItem = door.GetComponentInChildren<LG_GenericTerminalItem>();

                    if (!door.m_terminalItem.FloorItemLocation.Contains(scanZoneName))              { continue; }

                    string name = terminalItem.TerminalItemKey;
                    string title = name;

                    eNavMarkerStyle style = eNavMarkerStyle.PlayerPingSecurityDoor;

                    if (door.m_securityDoorType == eSecurityDoorType.Apex) {
                        style = eNavMarkerStyle.PlayerPingApexDoor;
                    } else if (door.m_securityDoorType == eSecurityDoorType.Bulkhead) {
                        style = eNavMarkerStyle.PlayerPingBulkheadDoor;
                    }

                    if (permanentMarker && FullMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.permSecurityDoorMarkersList.Add(new SecurityDoor_MarkerContext(door, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.white, permanentMarker));
                        }
                    } else if (verboseScan || keyItemsScan || PartialMatchOnList(name, filtersList)) {
                        lock (Netmap.__objectsInLevelMarkersLock) {
                            Netmap.securityDoorMarkersList.Add(new SecurityDoor_MarkerContext(door, name, title,
                                style, timeout, Config.markerIconScale, UnityEngine.Color.white));
                        }
                    }

                } catch (Exception e) {
                    Logger.Error(" ## " + e);
                }
            }



            // Total number of scanned items with active markers
            int scannedItemsCount = TotalScannedItemsCount();

            // Dynamic scanning time to penalize player for too broad scans

            // This behavior incentivizes player to use targeted scans. If base scan time is 2 sec and item scan time
            // is 500 ms overall scan time for a single element will be 2.5 sec: NMAP -T 120 CELL_798 E_712

            // If current zone contains 6 resource packs, the following search will take 2 + 6*0.5 = 5 sec:
            // NMAP -T 120 RES E_49

            // If current zone contains 6 resource packs, 2 key items and 8 consumables, the following search will take
            // 2 + 16*0.5 = 10 sec: NMAP -T 600 RES CONS E_113
            float scanTime = Config.baseScanTime + scannedItemsCount*Config.itemScanTime;

            __instance.AddOutput(TerminalLineType.ProgressWait, "Initalizing Santonian Holographic Netmap",
                scanTime, TerminalSoundType.LineTypeDefault, TerminalSoundType.LineTypeDefault);

            __instance.OnEndOfQueue = new Action(delegate () {

                __instance.AddOutput(string.Format(Config.exOutputFormat, "ID", "ITEM TYPE", "STATUS", "SPECIAL NOTES"), true);

                // Whether or not an extra newline is required after block of same type entries
                bool newline = false;

                lock (Netmap.__itemInLevelMarkersLock) {
                    Netmap.keyItemMarkersList.Sort();

                    // Key items list
                    foreach (ItemInLevel_MarkerContext marker in Netmap.keyItemMarkersList) {
                        var terminalItem = marker.item.GetComponentInChildren<LG_GenericTerminalItem>();

                        __instance.AddOutput(string.Format(Config.outputFormat, marker.name, terminalItem.FloorItemType,
                            terminalItem.FloorItemStatus), false);

                        newline = true;

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);
                }

                lock (Netmap.__objectsInLevelMarkersLock) {
                    Netmap.bulkheadMarkersList.Sort();
                    Netmap.generatorMarkersList.Sort();
                    Netmap.terminalMarkersList.Sort();
                    Netmap.hsuMarkersList.Sort();
                    Netmap.securityDoorMarkersList.Sort();

                    // Bulkhead DC's list
                    foreach (BulkheadDC_MarkerContext marker in Netmap.bulkheadMarkersList.Concat(Netmap.permBulkheadMarkersList)) {

                        var terminalItem = marker.item.m_terminalItem;

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            __instance.AddOutput(string.Format(Config.outputFormat, marker.name, terminalItem.FloorItemType,
                                terminalItem.FloorItemStatus), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);

                    // Power generators list
                    foreach (PowerGenerator_MarkerContext marker in Netmap.generatorMarkersList.Concat(Netmap.permGeneratorMarkersList)) {

                        var terminalItem = marker.item.m_terminalItem;

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            __instance.AddOutput(string.Format(Config.outputFormat, marker.name, terminalItem.FloorItemType,
                                terminalItem.FloorItemStatus), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);

                    // Terminals list
                    foreach (Terminal_MarkerContext marker in Netmap.terminalMarkersList.Concat(Netmap.permTerminalMarkersList)) {
                        var terminalItem = marker.item.m_terminalItem;

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            __instance.AddOutput(string.Format(Config.outputFormat, marker.name, terminalItem.FloorItemType,
                                terminalItem.FloorItemStatus), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);


                    // HSU's list
                    foreach (HSU_MarkerContext marker in Netmap.hsuMarkersList.Concat(Netmap.permHsuMarkersList)) {

                        var terminalItem = marker.item.m_terminalItem;

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            string hsuSubjectInfo = marker.item.m_subjectFirstName + " " + marker.item.m_subjectLastname;

                            hsuSubjectInfo += ", " + (marker.item.m_subjectIsFemale ? "Female" : "Male");
                            hsuSubjectInfo += ", " + marker.item.m_age;

                            __instance.AddOutput(string.Format(Config.exOutputFormat, marker.name, terminalItem.FloorItemType,
                                terminalItem.FloorItemStatus, hsuSubjectInfo), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);

                    // Security doors list
                    foreach (SecurityDoor_MarkerContext marker in Netmap.securityDoorMarkersList.Concat(Netmap.permSecurityDoorMarkersList)) {

                        var terminalItem = marker.item.m_terminalItem;

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            string accessKeyName = "";

                            GateKeyItem accessKey = marker.item.m_keyItem;
                            if (accessKey != null) {
                                accessKeyName = "Restricted zone: " + accessKey.m_keyName + "_" + accessKey.m_keyNum;
                            }

                            // Default FloorItemType of the LG_SecurityDoor is "Passage", so we're modifying it to more
                            // useful string: "Passage to ZONE_792"
                            string type = terminalItem.FloorItemType.ToString();

                            if (marker.item.LinkedToZoneData.Alias != 0) {
                                type = "Passage to ZONE_" + marker.item.LinkedToZoneData.Alias;
                            }

                            __instance.AddOutput(string.Format(Config.exOutputFormat, marker.name, type, terminalItem.FloorItemStatus,
                                accessKeyName), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);
                }

                lock (Netmap.__itemInLevelMarkersLock) {

                    Netmap.resourceMarkersList.Sort();
                    Netmap.consumableMarkersList.Sort();

                    // Resources list
                    foreach (ItemInLevel_MarkerContext marker in Netmap.resourceMarkersList) {

                        var terminalItem = marker.item.GetComponentInChildren<LG_GenericTerminalItem>();
                        var customData = marker.item.GetCustomData();

                        string capacity = customData.ammo.ToString() + "% [";

                        if (customData.ammo >= 20) {
                            capacity += customData.ammo/20 + " uses";
                        }

                        capacity += "]";

                        __instance.AddOutput(string.Format(Config.exOutputFormat, marker.name, terminalItem.FloorItemType,
                            terminalItem.FloorItemStatus, capacity), false);

                        newline = true;
                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);

                    // Consumables list
                    foreach (ItemInLevel_MarkerContext marker in Netmap.consumableMarkersList) {
                        __instance.AddOutput(string.Format(Config.outputFormat, marker.name, "Consumables", "Normal"), false);

                        newline = true;
                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);
                }

                lock (Netmap.__objectsInLevelMarkersLock) {

                    Netmap.containerMarkersList.Sort();
                    Netmap.doorMarkersList.Sort();

                    // Weak containers, boxes and lockers
                    foreach (Container_MarkerContext marker in Netmap.containerMarkersList) {

                        var terminalItem = marker.item.GetComponentInChildren<LG_GenericTerminalItem>();

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            __instance.AddOutput(string.Format(Config.outputFormat, marker.name, terminalItem.FloorItemType,
                                terminalItem.FloorItemStatus), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);

                    // Weak doors list
                    foreach (Door_MarkerContext marker in Netmap.doorMarkersList.Concat(Netmap.permDoorMarkersList)) {

                        var terminalItem = marker.item.m_terminalItem;

                        if (terminalItem.FloorItemLocation.Contains(scanZoneName)) {
                            __instance.AddOutput(string.Format(Config.outputFormat, marker.name, terminalItem.FloorItemType,
                                terminalItem.FloorItemStatus), false);

                            newline = true;
                        }

                        marker.Enable();
                    }

                    TerminalNewline(__instance, ref newline);
                }

                // Ping and summary
                CellSound.Post(EVENTS.TERMINAL_PING_MARKER_SFX, __instance.m_terminal.transform.position);
                __instance.AddOutput("Scan has finished, " + scannedItemsCount + " items discovered", false);
            });

            return false;
        }

        private static void TerminalNewline(LG_ComputerTerminalCommandInterpreter __instance, ref bool newline)
        {
            if (newline) { __instance.AddOutput(" ", false); newline = false; }
        }

        private static int TotalScannedItemsCount()
        {
            int scannedItemsCount = 0;

            lock(Netmap.__itemInLevelMarkersLock) {
                scannedItemsCount += Netmap.keyItemMarkersList.Count();
                scannedItemsCount += Netmap.resourceMarkersList.Count();
                scannedItemsCount += Netmap.consumableMarkersList.Count();
            }

            lock(Netmap.__objectsInLevelMarkersLock) {
                scannedItemsCount += Netmap.bulkheadMarkersList.Count()     + Netmap.permBulkheadMarkersList.Count();
                scannedItemsCount += Netmap.generatorMarkersList.Count()    + Netmap.permGeneratorMarkersList.Count();
                scannedItemsCount += Netmap.terminalMarkersList.Count()     + Netmap.permTerminalMarkersList.Count();
                scannedItemsCount += Netmap.hsuMarkersList.Count()          + Netmap.permHsuMarkersList.Count();
                scannedItemsCount += Netmap.securityDoorMarkersList.Count() + Netmap.permSecurityDoorMarkersList.Count();

                scannedItemsCount += Netmap.containerMarkersList.Count()    + Netmap.permContainerMarkersList.Count();
                scannedItemsCount += Netmap.doorMarkersList.Count()         + Netmap.permDoorMarkersList.Count();
            }

            return scannedItemsCount;
        }

        private static LG_Area FindParentArea(ItemInLevel item)
        {
            Transform current = item.gameObject.transform;

            while (current != null) {
                LG_Area area = current.GetComponent<LG_Area>();
                if (area != null) {
                    return area;
                }

                current = current.parent;
            }

            throw new MemberNotFoundException("No LG_Area parent class found for ItemInLevel provided");
        }

        private static void RemoveActiveMarkers() {
            Logger.Info(" ** removing all active navigation markers");

            lock(Netmap.__itemInLevelMarkersLock) {
                foreach (ItemInLevel_MarkerContext marker in Netmap.resourceMarkersList)        { marker.Remove(); }
                foreach (ItemInLevel_MarkerContext marker in Netmap.keyItemMarkersList)         { marker.Remove(); }
                foreach (ItemInLevel_MarkerContext marker in Netmap.consumableMarkersList)      { marker.Remove(); }

                Netmap.resourceMarkersList.Clear();
                Netmap.keyItemMarkersList.Clear();
                Netmap.consumableMarkersList.Clear();
            }

            lock(Netmap.__objectsInLevelMarkersLock) {
                foreach (Terminal_MarkerContext marker in Netmap.terminalMarkersList)           { marker.Remove(); }
                foreach (HSU_MarkerContext marker in Netmap.hsuMarkersList)                     { marker.Remove(); }
                foreach (BulkheadDC_MarkerContext marker in Netmap.bulkheadMarkersList)         { marker.Remove(); }
                foreach (PowerGenerator_MarkerContext marker in Netmap.generatorMarkersList)    { marker.Remove(); }

                foreach (Container_MarkerContext marker in Netmap.containerMarkersList)         { marker.Remove(); }

                foreach (Door_MarkerContext marker in Netmap.doorMarkersList)                   { marker.Remove(); }
                foreach (SecurityDoor_MarkerContext marker in Netmap.securityDoorMarkersList)   { marker.Remove(); }

                Netmap.terminalMarkersList.Clear();
                Netmap.hsuMarkersList.Clear();
                Netmap.bulkheadMarkersList.Clear();
                Netmap.generatorMarkersList.Clear();

                Netmap.containerMarkersList.Clear();

                Netmap.doorMarkersList.Clear();
                Netmap.securityDoorMarkersList.Clear();
            }
        }

        private static void RemovePermanentMarkers() {
            Logger.Info(" ** removing all permanent navigation markers");

            lock(Netmap.__objectsInLevelMarkersLock) {
                foreach (Terminal_MarkerContext marker in Netmap.permTerminalMarkersList)           { marker.Remove(); }
                foreach (HSU_MarkerContext marker in Netmap.permHsuMarkersList)                     { marker.Remove(); }
                foreach (BulkheadDC_MarkerContext marker in Netmap.permBulkheadMarkersList)         { marker.Remove(); }
                foreach (PowerGenerator_MarkerContext marker in Netmap.permGeneratorMarkersList)    { marker.Remove(); }

                foreach (Container_MarkerContext marker in Netmap.permContainerMarkersList)         { marker.Remove(); }

                foreach (Door_MarkerContext marker in Netmap.permDoorMarkersList)                   { marker.Remove(); }
                foreach (SecurityDoor_MarkerContext marker in Netmap.permSecurityDoorMarkersList)   { marker.Remove(); }

                Netmap.permTerminalMarkersList.Clear();
                Netmap.permHsuMarkersList.Clear();
                Netmap.permBulkheadMarkersList.Clear();
                Netmap.permGeneratorMarkersList.Clear();

                Netmap.permDoorMarkersList.Clear();
                Netmap.permSecurityDoorMarkersList.Clear();
            }
        }
    }
}

