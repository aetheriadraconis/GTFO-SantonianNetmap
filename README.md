
# Overview

- This mod adds a new terminal command, "NMAP", that allows you to mark resources, consumables and stationary objects of interest in the current zone (and not only, see later)

- The command is intended first and foremost for solo runs, when there are no second human player to man the terminal, but can be useful in multiplayer runs as well

- Marked resources can be displayed for configurable timeout, in range from 30 to 600 seconds, or permanently

- The command style, description and arguments are made to resemble existing commands with overlapping functionality, LIST, PING and QUERY and, therefore, organically integrate into the game environment



# Uses

### Mapping resources and consumables in the current zone

<pre>
NMAP -T 120 RES CON E_49
</pre>

Similarly to LIST command, full names "RESOURCES" and "CONSUMABLES" can be shortened to first 3+ letters, "RES" "CON"

Normally, consumables cannot be pinged with PING or listed, as they lack unique terminal ID's, but NMAP can help you quickly locate them


### Maping key resource item for extended time in the current zone

<pre>
NMAP -T 600 CELL_702 E_713
NMAP -T 600 KEY_PURPLE_546 E_709
</pre>

When aforementioned item is located deep in infectious fog on the distance 200m+, makes finding it in solo runs possible (R2D1)


### Mapping a number of key items in the current zone

NMAP -T 300 GLP E_12

ID's, GLP-2 canisters (R7C1), HDD's, data cubes etc.


### Assigning a permanent navigation marker to the stationary object of interest in any zone

<pre>
NMAP -P TERMINAL_862 TERMINAL_754 E_614
NMAP -P BULKHEAD_DC_320 E_614
</pre>

 - Need to quickly find a terminal with keyword in another zone during reactor scan (R2D2)? No problem.
 - Run between two terminals to complete uplink verification in OSHA non-compliant and confusing level layout? Easy.
 - Bulkhead door controller is located deep in the fog and you need to quickly return to it? We got you covered.

Permanent markers will not reset with subsequent NMAP scans and will stay put until the end of rundown, unless explicitly removed with

<pre>
NMAP -R
</pre>


# Sample on-screen output

In addition to visible navigation markers, Santonian Netmap displays useful information about mapped items. The first 3 columns resemble ones from the LIST command, but "SPECIAL NOTES" column adds extra information:

 * zone number, where SEC_DOOR_### opens passage to
 * keycard name required to access security door, if any
 * size and number of uses of the resource pack
 * misc information, such as name, gender and age of HSU (ex-)inhabitant

<pre>
\\Root\NMAP -A E_49

ID                                ITEM TYPE                         STATUS                  SPECIAL NOTES

TERMINAL_958                      Terminal                          Normal

SEC_DOOR_708                      Passage to ZONE_50                Normal
SEC_DOOR_848                      Passage to ZONE_52                Normal                  Restricted zone: KEY_RED_553

HSU_142                           Unknown                           Malfunctioning          Kristen Ávila, Female, 66
HSU_144                           Unknown                           Normal                  Preston Guevara, Male, 15
HSU_267                           Unknown                           UnPowered               Jennifer Ward, Female, 17
HSU_427                           Unknown                           Malfunctioning          Amanda Ruiz, Female, 33
HSU_451                           Unknown                           Deactivated             Traci Olivares, Female, 34
HSU_547                           Unknown                           Powered                 Carla Edwards, Female, 66

AMMOPACK_357                      Resources                         Normal                  40% [2 uses]
MEDIPACK_952                      Resources                         Normal                  100% [5 uses]
TOOL_REFILL_461                   Resources                         Normal                  40% [2 uses]

GLOW STICK                        Consumables                       Normal
LONG RANGE FLASHLIGHT             Consumables                       Normal
EXPLOSIVE TRIP MINE               Consumables                       Normal

BOX_180                           Storage                           Normal
BOX_463                           Storage                           Normal
LOCKER_104                        Storage                           Normal
LOCKER_253                        Storage                           Normal

DOOR_159                          Passage                           Normal
DOOR_365                          Passage                           Normal

Scan has finished, 21 items discovered
</pre>


# Implementation details

- Technically NMAP can assign navigation markers in milliseconds, but it will break immersion. Furthemore, to allign better with PING and introduce heavier cost for broad scans, the scan time depends on the number of items found and calculated using the following formula:

T = Tp + N*Tn

Tp = 2.0 sec
Tn = 0.5 sec

- Likewise, it's possible to mark resources, consumables and key items outside of the current zone or _all_ the items in the level. However, the game design often encompasses looking for terminal in the current zone. Ability to mark large stationary objects (security doors,
terminal, bulkhead door controllers) outside of the current zone was left as it doesn't break immersion, yet helps with aforementioned scenarios during complex reactor scans and uplink objectives

Additionally, having too many navigation markers at the same time clutters the view and, if markers are located too far away, makes them unreadable


# Credits

Thanks to Fridolin. Santonian Netmap started as an extension to Frido's Smart Ping in order to make it integrate more seamlessly into game environment and allow configurable fadeout timeouts


# Note

NMAP is a referrence to Linux NMAP command, aka "Network Mapper" (man 1 nmap)


============ Change Log ============

v1.0.1
 - Add permanent navigation marker support

v1.0.0
 - Add README.md, Thunderstore manifest, mod icon

v0.9.9
 - Add full command-line parsing support
 - Fix Logger tag

v0.9.8
 - Add support for generators, bulkhead door controllers, doors and containers

v0.9.7
 - Add support for Terminal and HSU objects

v0.9.6
 - Add support for remote zone scans

v0.9.5
 - Add dynamic scan timeout support

v0.9.4
 - Add consumables mapping support

v0.9.3
 - Fix removal of the key item navigation markers

v0.9.2
 - Introduce command-line parsing function

v0.9.1
 - Refactor the code, optimize nested loops

v0.9.0
 - Initial Commit
