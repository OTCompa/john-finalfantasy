# John Finalfantasy
John Finalfantasy is a Dalamud plugin for FINAL FANTASY XIV that masks the player's name on the party list and/or most of the other occurrences.
This same feature can also be applied to other players in the same party as the player.  
This plugin is heavily based on a discontinued plugin known as [Nomina Occulta](https://git.anna.lgbt/anna/NominaOcculta) by [anna](https://anna.lgbt/)

## Features
- Masking names in the party list for player and/or party members
  - Manually specify what names to change to
- Replace all text occurrences of the __full name__ of the player and/or party members

## Drawbacks
Most of the drawbacks of this plugin are related to the second feature, which replaces all text occurrences of the full name of a player.
- When replacing all text occurrences, this program only searches for the full name of the player(s).
- Any abbreviated versions (like an abbreviated nameplate) will not be replaced.
  - I could technically implement this if people are interested in this, but likely only for only first/last name abbreviated, not both, since that may cause issues.
- Anywhere the partial name appears (such as the party portrait preview when you instance into a duty with a party) will also not be replaced.
- The party chat will have a slight visual bug if the original name greatly differs in length from the masked name.
- If the original name of a player is a common phrase, you may find unintended instances of the name being replaced.

## Installation
1. Open Dalamud settings in-game `/xlsettings`
2. Navigate to the "Experimental" tab
3. Under the "Custom Plugin Repositories" section, add the following entry:
    ```
    https://raw.githubusercontent.com/OTCompa/frey-s-dalamud-plugins/refs/heads/main/plogon.json
    ```
4. Save the settings change and you may find the plugin as "John Finalfantasy" in the plugin installer.
