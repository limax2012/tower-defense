using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Rendering;

internal sealed class MainMenuBattleScene
{
    private const float FixedStepSeconds = 1f / 60f;
    private readonly GameContent _content;
    private readonly GameRenderer _renderer = new();
    private LaneBattle _left;
    private LaneBattle _right;
    private float _accumulator;

    public int EnemiesKilled => _left.EnemiesKilled + _right.EnemiesKilled;
    public int EnemiesEscaped => _left.EnemiesEscaped + _right.EnemiesEscaped;

    public MainMenuBattleScene(GameContent content)
    {
        _content = content;
        _left = CreateLane(leftLane: true);
        _right = CreateLane(leftLane: false);
        WarmUp(_left, 1.8f);
        WarmUp(_right, 3.2f);
    }

    public void Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return;
        _accumulator += Math.Min(elapsedSeconds, 0.25f);
        while (_accumulator >= FixedStepSeconds)
        {
            UpdateLane(ref _left, leftLane: true);
            UpdateLane(ref _right, leftLane: false);
            _accumulator -= FixedStepSeconds;
        }
    }

    public void Draw(SpriteBatch batch, PrimitiveRenderer primitives)
    {
        _renderer.DrawCombatShowcase(batch, primitives, _left.Session);
        _renderer.DrawCombatShowcase(batch, primitives, _right.Session);
    }

    private void UpdateLane(ref LaneBattle lane, bool leftLane)
    {
        lane.Session.Update(FixedStepSeconds);
        if (!lane.Session.IsVictory && !lane.Session.IsDefeat) return;

        var completedKills = lane.EnemiesKilled;
        var completedEscapes = lane.EnemiesEscaped;
        lane = CreateLane(leftLane, completedKills, completedEscapes);
    }

    private static void WarmUp(LaneBattle lane, float seconds)
    {
        var ticks = (int)MathF.Round(seconds / FixedStepSeconds);
        for (var index = 0; index < ticks; index++) lane.Session.Update(FixedStepSeconds);
    }

    private LaneBattle CreateLane(bool leftLane, int previousKills = 0, int previousEscapes = 0)
    {
        var mapId = leftLane ? "menu_left" : "menu_right";
        var waveSetId = $"{mapId}_waves";
        var pathX = leftLane ? 155f : 1125f;
        var startY = leftLane ? 74f : 664f;
        var endY = leftLane ? 664f : 74f;
        var sourcePath = _content.Maps.TryGetValue(leftLane ? "foundry_loop" : "relay_divide", out var sourceMap)
            ? sourceMap.PathVisual
            : new PathVisualData { Style = leftLane ? "foundry" : "surge" };
        var map = new MapDefinition
        {
            Id = mapId,
            DisplayName = "Menu Defense",
            LogicalSize = new LogicalSizeData { Width = GameConstants.LogicalWidth, Height = GameConstants.LogicalHeight },
            Background = new BackgroundData { Base = "#F4F5F8", Accent = "#E7EDF5", Motif = "none" },
            PathVisual = new PathVisualData
            {
                Style = sourcePath.Style,
                Base = sourcePath.Base,
                Accent = sourcePath.Accent,
                Secondary = sourcePath.Secondary
            },
            Spawn = new PointData { X = pathX, Y = startY },
            Goal = new PointData { X = pathX, Y = endY },
            PathWidth = 56,
            Path =
            [
                new PointData { X = pathX, Y = startY },
                new PointData { X = pathX, Y = endY }
            ],
            WaveSet = waveSetId,
            StartingCredits = 0,
            StartingLives = 24
        };
        var waveSet = new WaveSetDefinition
        {
            Id = waveSetId,
            MapId = mapId,
            Waves = [CreateWave(leftLane)]
        };
        var maps = new Dictionary<string, MapDefinition>(_content.Maps, StringComparer.OrdinalIgnoreCase)
        {
            [mapId] = map
        };
        var waveSets = new Dictionary<string, WaveSetDefinition>(_content.WaveSets, StringComparer.OrdinalIgnoreCase)
        {
            [waveSetId] = waveSet
        };
        var laneContent = new GameContent
        {
            Towers = _content.Towers,
            Enemies = _content.Enemies,
            Map = map,
            Waves = waveSet,
            Maps = maps,
            WaveSets = waveSets,
            Difficulties = _content.Difficulties,
            Challenges = _content.Challenges,
            Tactics = _content.Tactics
        };
        var session = new GameSession(laneContent, mapId, DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        AddTowers(session, leftLane);
        var lane = new LaneBattle(session, previousKills, previousEscapes);
        session.EnemyKilled += _ => lane.EnemiesKilled++;
        session.EnemyEscaped += _ => lane.EnemiesEscaped++;
        session.StartNextWave(false);
        return lane;
    }

    private static WaveDefinition CreateWave(bool leftLane) => new()
    {
        Number = 1,
        Archetype = "Menu Skirmish",
        HealthMultiplier = leftLane ? 1.05f : 1.10f,
        SpeedMultiplier = 0.90f,
        Groups = leftLane
            ?
            [
                new WaveGroupDefinition { EnemyId = "t1_crawler", Count = 4, SpawnInterval = 1.05f },
                new WaveGroupDefinition { EnemyId = "t2_runner", Count = 3, SpawnInterval = 0.90f, DelayBefore = 0.35f },
                new WaveGroupDefinition { EnemyId = "t3_brute", Count = 2, SpawnInterval = 1.40f, DelayBefore = 0.40f }
            ]
            :
            [
                new WaveGroupDefinition { EnemyId = "t2_runner", Count = 4, SpawnInterval = 0.90f },
                new WaveGroupDefinition { EnemyId = "t1_crawler", Count = 3, SpawnInterval = 1.05f, DelayBefore = 0.30f },
                new WaveGroupDefinition { EnemyId = "t4_aegis", Count = 2, SpawnInterval = 1.55f, DelayBefore = 0.45f }
            ]
    };

    private static void AddTowers(GameSession session, bool leftLane)
    {
        var placements = leftLane
            ? new (string Id, Vector2 Position)[]
            {
                ("needle_turret", new Vector2(67, 205)),
                ("ember_coil", new Vector2(243, 390)),
                ("watchtower", new Vector2(67, 558))
            }
            :
            [
                ("frost_spire", new Vector2(1037, 185)),
                ("shard_fan", new Vector2(1213, 370)),
                ("breaker_cannon", new Vector2(1037, 548))
            ];

        for (var index = 0; index < placements.Length; index++)
        {
            if (!session.Content.Towers.TryGetValue(placements[index].Id, out var definition)) continue;
            session.Towers.Add(new TowerInstance(index + 1, definition, placements[index].Position));
        }
    }

    private sealed class LaneBattle
    {
        public GameSession Session { get; }
        public int EnemiesKilled { get; set; }
        public int EnemiesEscaped { get; set; }

        public LaneBattle(GameSession session, int enemiesKilled, int enemiesEscaped)
        {
            Session = session;
            EnemiesKilled = enemiesKilled;
            EnemiesEscaped = enemiesEscaped;
        }
    }
}
