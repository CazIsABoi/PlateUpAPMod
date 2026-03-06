# PlateUpAP — Archipelago Randomizer Mod for PlateUp!

PlateUpAP is an [Archipelago](https://archipelago.gg) multiworld randomizer implementation for PlateUp!. It consists of two parts that must always be kept in sync:

- **The apworld** — installed into your Archipelago client, handles item/location logic and generation
- **The companion mod** — installed into PlateUp!, handles in-game checks, item receiving, and gates

When connected, appliances, dish unlocks, day progression, and more become items in a shared multiworld pool — sent and received between you and other players across any supported games.

---

## How It Works

- Complete days, earn stars, and franchise to send checks to other players
- Receive items from the multiworld to unlock appliances, progress through days, and increase your money cap
- Progression is gated — you may need Day Leases, dish unlocks, or appliance unlocks before you can advance
- Goals include franchising a set number of times, completing a set number of days, or reaching a target day with specific dishes

---

## Requirements

- [PlateUp!](https://store.steampowered.com/app/1599600/PlateUp/) on Steam
- [Archipelago](https://archipelago.gg) client (0.6.4 or later)
- The following workshop mods (required dependencies):
  - PreferenceSystem
  - PlatePatch
  - KitchenLib
  - HarmonyX

---

## Installation

### apworld
1. Download the latest `.apworld` file from the [Releases](../../releases) page
2. Place it in your Archipelago `worlds` folder
3. Generate a multiworld yaml using the provided template

### Mod (via Steam Workshop) — Recommended
1. Subscribe to the [mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3484431423) and all required dependencies on the Steam Workshop
2. Launch PlateUp! and go to the HQ (lobby)
3. Create a config via **Options → PreferenceSystem → PlateupAP**
4. Set your Archipelago server IP in the config file located at:
   `%appdata%\..\LocalLow\It's Happening\PlateUp\PlateUpAPConfig`
5. Connect and enjoy!

### Mod (Manual Install)
1. Download the latest mod release from the [Releases](../../releases) page
2. Extract into:
   `Program Files (x86)\Steam\steamapps\common\PlateUp\PlateUp\Mods`
3. Launch PlateUp! and follow steps 2–5 above

> ⚠️ Only use one version of the mod at a time — do not have both the Workshop and manual versions installed simultaneously.

---

## Documentation

For a full breakdown of all yaml options, gates, goals, items, checks, and troubleshooting, see the **[full options & features guide](https://docs.google.com/document/d/1H_T82UsZbHI4CbvfHWnud8xZqhXH5XWc_sZkax1I8YY/edit?tab=t.0)**.

---

## Known Incompatibilities

None currently! 😃

---

## Issues & Feedback

Found a bug or have a suggestion? Open an issue on this repository or reach out in the community Discord thread.
