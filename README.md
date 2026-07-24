# Return By Death

An audio / gameplay modification for **Lethal Company** that implements a Subaru-style "Return By Death" checkpoint system. When you die, time rewind-restores your player state, physical environment, and inventory back to your last saved checkpoint complete with visual and physics cleanup.

---

## Key Features

Replaces game audio events with the iconic sound effect from  *Re:Zero* .

* **The Witch's Call:** Triggers instantly when encountering a **Forest Giant** (old witch sound) or spotting a **Dead Body** (new witch sound).
* **Return by death:** Replaces the **Teleport** sound with return by death sound, works with teleport trap in lethal things mod
* **Subaru's phone ring:** Replaces the **Icecream Truck** sound with subaru's phone ring in the white whale scene.

Add a new mechanic (soon be an entity)  *Return by death*

* **Instant Rewind on Death**: Restores your health, stamina, camera orientation, and player metrics to the exact moment your checkpoint was saved.
* **Inventory Persistence**: Restores scrap items, held objects, and active inventory slot selections upon respawning.
* **Environmental & Hazard Cleanup**: Automatically cleans up local explosion marks, mine trigger states, and flashbang decals created during the failed timeline.
* **HUD & Visor Integrity**: Restores sanity UI filters, fear levels, visor cracks, and audio ambience to reflect your saved state accurately.

---

## How It Works

1. **State Snapshotting**:
   While you play, the mod continuously captures high-fidelity snapshots of your player character (health, status effects, inventory references, transform coordinates, and environment states).
2. **Trigger-Flush Teleportation**:
   Upon taking lethal damage:

* The system momentarily disables physical character controllers and teleports the player out-of-bounds (OOB) for a fraction of a second.
* This forces Unity's physics engine to flush active trigger states (like pit-fall killzones) and issue proper exit events.

3. **Timeline Rewind**:

* The player is repositioned to the saved checkpoint.
* Local visual effects (such as flashbang explosion decals or landmine states) are reset or destroyed.
* Internal trigger states on map hazards are refreshed so they can interact with you naturally again.
* Inventory scrap data and UI HUD elements are synced back to the snapshot state.

---

## Manual Installation

1. Ensure you have **[BepInEx](https://github.com/BepInEx/BepInEx)** installed for Lethal Company.
2. Download the latest release of `Re:zero_ReturnByDeath`.
3. Extract and place the mod folder into your `Lethal Company/BepInEx/plugins/` directory.
4. Launch the game.

---

## Current Scope & Compatibility

* **Singleplayer / Client-Side Focus**: Designed primarily for single-player testing and local client-state restoration.
* **Modded Equipment Support**: Uses deep object lookup to dynamically handle both vanilla and modded items (e.g., custom grenades or scrap).
* **Multiplayer Note**: Inventory and world item synchronization across non-host clients in multiplayer sessions is currently a work in progress—items restored on the local client may require dropping or re-equipping to fully sync with teammates.
