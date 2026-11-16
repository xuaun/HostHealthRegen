using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace HostHealthRegen;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "Xuaun.HostHealthRegen";
    public const string Name = "HostHealthRegen";
    public const string Version = "1.0.0";

    // How often the damage watch and the tick clock run.
    private const float PollSeconds = 0.25f;

    internal static ManualLogSource Log = null!;

    private ConfigEntry<bool> _enabled = null!;
    private ConfigEntry<float> _interval = null!;
    private ConfigEntry<int> _baseAmount = null!;
    private ConfigEntry<int> _bonusPerUpgrade = null!;
    private ConfigEntry<float> _delayAfterDamage = null!;
    private ConfigEntry<bool> _slowerWithMorePlayers = null!;
    private ConfigEntry<int> _maxHealthPercent = null!;
    private ConfigEntry<bool> _inShop = null!;
    private ConfigEntry<bool> _inTruckLobby = null!;
    private ConfigEntry<bool> _healEffect = null!;
    private ConfigEntry<bool> _debugLogs = null!;

    private readonly Dictionary<PlayerAvatar, int> _lastHealth = new();
    private readonly Dictionary<PlayerAvatar, float> _lastDamageTime = new();
    private float _pollTimer;
    private float _tickTimer;

    private void Awake()
    {
        Log = Logger;

        // Keep the plugin object alive across scene loads.
        transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        _enabled = Config.Bind("General", "Enabled", true,
            "Turns the regeneration on or off.");
        _interval = Config.Bind("Regeneration", "IntervalSeconds", 20f,
            new ConfigDescription("Seconds between regeneration ticks.", new AcceptableValueRange<float>(1f, 300f)));
        _baseAmount = Config.Bind("Regeneration", "BaseAmount", 1,
            new ConfigDescription("Health restored per tick, before the upgrade bonus.", new AcceptableValueRange<int>(0, 50)));
        _bonusPerUpgrade = Config.Bind("Regeneration", "BonusPerHealthUpgrade", 1,
            new ConfigDescription("Extra health per tick for each health upgrade the player owns. 0 gives everyone the same amount.", new AcceptableValueRange<int>(0, 10)));
        _delayAfterDamage = Config.Bind("Regeneration", "DelayAfterDamageSeconds", 0f,
            new ConfigDescription("A player who took damage is skipped until this many seconds have passed. 0 turns it off.", new AcceptableValueRange<float>(0f, 120f)));
        _slowerWithMorePlayers = Config.Bind("Regeneration", "SlowerWithMorePlayers", false,
            "Multiplies the interval by the number of living players.");
        _maxHealthPercent = Config.Bind("Regeneration", "MaxHealthPercent", 100,
            new ConfigDescription("Regeneration stops at this percentage of the player's max health.", new AcceptableValueRange<int>(1, 100)));
        _inShop = Config.Bind("Where", "RegenInShop", false,
            "Also regenerate in the shop.");
        _inTruckLobby = Config.Bind("Where", "RegenInTruckLobby", false,
            "Also regenerate in the truck lobby between levels.");
        _healEffect = Config.Bind("Effects", "HealEffect", false,
            "Plays the game's heal sound and screen effect on every tick.");
        _debugLogs = Config.Bind("Debug", "DebugLogs", false,
            "Logs every regeneration tick.");

        Log.LogInfo($"{Name} {Version} loaded.");
    }

    private void Update()
    {
        _pollTimer += Time.deltaTime;
        if (_pollTimer < PollSeconds)
        {
            return;
        }
        float elapsed = _pollTimer;
        _pollTimer = 0f;

        if (!_enabled.Value || !IsHostInPlayableScene())
        {
            ResetState();
            return;
        }

        List<PlayerAvatar> players = GameDirector.instance.PlayerList;
        int alive = WatchDamage(players);

        float interval = Mathf.Max(1f, _interval.Value);
        if (_slowerWithMorePlayers.Value)
        {
            interval *= Mathf.Max(1, alive);
        }

        _tickTimer += elapsed;
        if (_tickTimer < interval)
        {
            return;
        }
        _tickTimer = 0f;
        HealAll(players);
    }

    // True when this machine decides the heals and the current scene allows them.
    private bool IsHostInPlayableScene()
    {
        if (!GameManager.instance || !RunManager.instance || !GameDirector.instance || !LevelGenerator.Instance)
        {
            return false;
        }
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !LevelGenerator.Instance.Generated)
        {
            return false;
        }
        if (GameDirector.instance.currentState != GameDirector.gameState.Main)
        {
            return false;
        }
        if (SemiFunc.RunIsShop())
        {
            return _inShop.Value;
        }
        if (SemiFunc.RunIsLobby())
        {
            return _inTruckLobby.Value;
        }
        return SemiFunc.RunIsLevel() && !SemiFunc.RunIsArena();
    }

    // Records who lost health since the last poll and returns the number of living players.
    private int WatchDamage(List<PlayerAvatar> players)
    {
        int alive = 0;
        foreach (PlayerAvatar player in players)
        {
            if (!IsAlive(player))
            {
                continue;
            }
            alive++;
            int health = player.playerHealth.health;
            if (_lastHealth.TryGetValue(player, out int previous) && health < previous)
            {
                _lastDamageTime[player] = Time.time;
            }
            _lastHealth[player] = health;
        }
        return alive;
    }

    private void HealAll(List<PlayerAvatar> players)
    {
        foreach (PlayerAvatar player in players)
        {
            if (!IsAlive(player))
            {
                continue;
            }
            float delay = _delayAfterDamage.Value;
            if (delay > 0f && _lastDamageTime.TryGetValue(player, out float damagedAt) && Time.time - damagedAt < delay)
            {
                continue;
            }

            PlayerHealth playerHealth = player.playerHealth;
            int cap = Mathf.CeilToInt(playerHealth.maxHealth * (_maxHealthPercent.Value / 100f));
            int missing = cap - playerHealth.health;
            if (missing <= 0)
            {
                continue;
            }

            int upgrades = 0;
            if (StatsManager.instance && player.steamID != null)
            {
                StatsManager.instance.playerUpgradeHealth.TryGetValue(player.steamID, out upgrades);
            }
            int amount = Mathf.Min(_baseAmount.Value + _bonusPerUpgrade.Value * upgrades, missing);
            if (amount <= 0)
            {
                continue;
            }

            // The game's own heal: applied by the avatar's owner and synced to everyone.
            playerHealth.HealOther(amount, _healEffect.Value);

            if (_debugLogs.Value)
            {
                Log.LogInfo($"{player.playerName}: +{amount} ({playerHealth.health}/{playerHealth.maxHealth} before the heal)");
            }
        }
    }

    private static bool IsAlive(PlayerAvatar player)
    {
        return player && player.playerHealth && !player.deadSet && !player.isDisabled && player.playerHealth.health > 0;
    }

    private void ResetState()
    {
        _tickTimer = 0f;
        if (_lastHealth.Count > 0)
        {
            _lastHealth.Clear();
            _lastDamageTime.Clear();
        }
    }
}
