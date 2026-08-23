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
    private readonly Random _random;
    private LaneBattle _left;
    private LaneBattle _right;
    private float _accumulator;

    public int EnemiesKilled => _left.EnemiesKilled + _right.EnemiesKilled;
    public int EnemiesEscaped => _left.EnemiesEscaped + _right.EnemiesEscaped;
    public IReadOnlyList<int> TowerLevels => _left.Session.Towers.Concat(_right.Session.Towers)
        .Select(tower => tower.LevelIndex + 1).ToArray();
    public IReadOnlyList<string> TowerKinds => _left.Session.Towers.Concat(_right.Session.Towers)
        .Select(tower => tower.Definition.Id).ToArray();
    public IReadOnlyList<int> TowerCounts => [_left.Session.Towers.Count, _right.Session.Towers.Count];
    public IReadOnlyList<int> EnemyCounts => [_left.EnemyCount, _right.EnemyCount];

    public MainMenuBattleScene(GameContent content, int? randomSeed = null)
    {
        _content = content;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
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
        var wave = CreateWave(leftLane);
        var waveSet = new WaveSetDefinition
        {
            Id = waveSetId,
            MapId = mapId,
            Waves = [wave]
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
        var lane = new LaneBattle(session, previousKills, previousEscapes,
            wave.Groups.Sum(group => group.Count));
        session.EnemyKilled += _ => lane.EnemiesKilled++;
        session.EnemyEscaped += _ => lane.EnemiesEscaped++;
        session.StartNextWave(false);
        return lane;
    }

    private WaveDefinition CreateWave(bool leftLane)
    {
        var enemyCount = _random.Next(3, 6);
        var pool = leftLane
            ? new[] { "t1_crawler", "t1_crawler", "t2_runner", "t3_brute" }
            : new[] { "t1_crawler", "t2_runner", "t2_runner", "t3_brute" };
        var groups = new List<WaveGroupDefinition>(enemyCount);
        for (var index = 0; index < enemyCount; index++)
        {
            var enemyId = index switch
            {
                0 => leftLane ? "t1_crawler" : "t2_runner",
                _ when index == enemyCount - 1 => leftLane ? "t5_regenerator" : "t4_aegis",
                _ => pool[_random.Next(pool.Length)]
            };
            groups.Add(new WaveGroupDefinition
            {
                EnemyId = enemyId,
                Count = 1,
                SpawnInterval = 1.15f + (_random.NextSingle() * 0.35f),
                DelayBefore = index == 0 ? 0f : 0.25f + (_random.NextSingle() * 0.35f)
            });
        }

        return new WaveDefinition
        {
            Number = 1,
            Archetype = "Menu Skirmish",
            HealthMultiplier = leftLane ? 1.30f : 1.35f,
            SpeedMultiplier = 0.90f,
            Groups = groups
        };
    }

    private void AddTowers(GameSession session, bool leftLane)
    {
        var positions = leftLane
            ? new[]
            {
                new Vector2(67, 160),
                new Vector2(243, 245),
                new Vector2(67, 330),
                new Vector2(243, 415),
                new Vector2(67, 500),
                new Vector2(243, 585)
            }
            : new[]
            {
                new Vector2(1213, 160),
                new Vector2(1037, 245),
                new Vector2(1213, 330),
                new Vector2(1037, 415),
                new Vector2(1213, 500),
                new Vector2(1037, 585)
            };
        Shuffle(positions);
        var towerCount = _random.Next(3, 6);
        var candidates = session.Content.Towers.Values
            .Where(tower => !tower.Id.Equals("signal_beacon", StringComparison.OrdinalIgnoreCase))
            .OrderBy(tower => tower.Id)
            .ToList();
        Shuffle(candidates);
        var chosen = candidates.Take(towerCount).ToList();

        for (var index = 0; index < Math.Min(positions.Length, chosen.Count); index++)
        {
            var tower = new TowerInstance(index + 1, chosen[index], positions[index]);
            ApplyLevel(tower, _random.Next(1, 4));
            session.Towers.Add(tower);
        }
    }

    private void ApplyLevel(TowerInstance tower, int level)
    {
        if (level >= 2)
        {
            if (tower.Definition.Tier2Doctrines.Count > 0)
                tower.TryChooseDoctrine(tower.Definition.Tier2Doctrines[_random.Next(tower.Definition.Tier2Doctrines.Count)].Id);
            else
                tower.TryUpgrade();
        }

        if (level < 3) return;
        if (tower.Definition.Specializations.Count > 0)
            tower.TrySpecialize(tower.Definition.Specializations[_random.Next(tower.Definition.Specializations.Count)].Id);
        else
            tower.TryUpgrade();
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private sealed class LaneBattle
    {
        public GameSession Session { get; }
        public int EnemiesKilled { get; set; }
        public int EnemiesEscaped { get; set; }
        public int EnemyCount { get; }

        public LaneBattle(GameSession session, int enemiesKilled, int enemiesEscaped, int enemyCount)
        {
            Session = session;
            EnemiesKilled = enemiesKilled;
            EnemiesEscaped = enemiesEscaped;
            EnemyCount = enemyCount;
        }
    }
}
