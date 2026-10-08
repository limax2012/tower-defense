using System;
using System.IO;
using MaximalBastion;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Towers;
using UnityEngine;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public sealed class SimulationHost : MonoBehaviour
    {
        public const float Step = 1f / 60f;
        public GameContent Content { get; private set; }
        public GameSession Session { get; private set; }
        public SaveSlotRepository Saves { get; private set; }
        public RunHistoryRepository History { get; private set; }
        public bool Paused { get; set; }
        public bool AutoWave { get; set; }
        public bool HasFocus { get; private set; } = true;
        public bool VerificationMode { get; private set; }
        public float VerificationRate { get; set; } = 1;
        public bool IsRunning => Session != null && !Paused && (HasFocus || VerificationMode);
        public int SelectedId { get; private set; }
        public bool GeneratorSelected { get; private set; }
        public TowerInstance SelectedTower => Session?.Towers.Find(t => t.Id == SelectedId);
        public string PlacementId { get; private set; }
        public TacticalPlacementKind TacticalPlacement { get; private set; }
        public bool IsPlacing => PlacementId != null || TacticalPlacement != TacticalPlacementKind.None;
        public string Notice { get; private set; } = "";
        public float Interpolation => Mathf.Clamp01(_accumulator / Step);
        public long Tick { get; private set; }
        public event Action SessionChanged;
        public event Action BeforeStep;
        public event Action AfterStep;
        public event Action Changed;
        private float _accumulator;
        private float _noticeTime;
        private bool _recorded;
        private bool _checkpointPending;
        private long _request;
        private AuthoritativeCommandHost _commands;

        public void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            VerificationMode = Array.Exists(args, a => a == "--verify" || a == "--tour" || a == "--autoplay" || a == "--ui-review" || a == "--ui-verify" || a == "--record-backend" || a == "--combat-review" || a == "--campaign-replay");
            Content = new ContentLoader(Path.Combine(Application.streamingAssetsPath, "ContentData")).Load();
            var persistenceRoot = VerificationMode
                ? Path.Combine(Path.GetTempPath(), "MaximalBastion3DVerification", Guid.NewGuid().ToString("N"))
                : Application.persistentDataPath;
            Saves = new SaveSlotRepository(persistenceRoot);
            History = new RunHistoryRepository(persistenceRoot);
            AutoWave = PlayerPrefs.GetInt("AutoWave", 0) != 0;
        }

        public void NewRun(string difficulty = "normal", string mode = "standard") =>
            Attach(new GameSession(Content, "foundry_loop", difficulty, mode));

        public void Attach(GameSession session)
        {
            Session = session;
            _commands = new AuthoritativeCommandHost();
            _request = 0;
            _accumulator = 0;
            Tick = 0;
            _recorded = false;
            _checkpointPending = false;
            Paused = false;
            SelectedId = 0;
            GeneratorSelected = false;
            CancelPlacement();
            session.WaveCompleted += wave => _checkpointPending = true;
            SessionChanged?.Invoke();
            Changed?.Invoke();
        }

        public void LeaveRun()
        {
            if (Session != null && Session.CanSaveCheckpoint) Save(0, false);
            Session = null;
            Paused = false;
            CancelPlacement();
            SessionChanged?.Invoke();
        }

        public void Restart()
        {
            if (Session == null) return;
            NewRun(Session.DifficultyId, Session.ChallengeId);
        }

        public bool Command(GameCommandType type, int entityId = 0, string choice = null,
            SimVector? position = null, TargetMode targetMode = TargetMode.First, float speed = 1)
        {
            if (Session == null) return false;
            var p = position ?? default;
            return Command(new GameCommand
            {
                Type = type,
                EntityId = entityId, TowerDefinitionId = choice ?? "", DoctrineId = choice ?? "",
                SpecializationId = choice ?? "", X = p.X, Y = p.Y, TargetMode = targetMode, Speed = speed
            }, true);
        }

        public bool Command(GameCommand command, bool announce = false)
        {
            if (Session == null || command == null) return false;
            var result = _commands.Submit(Session, command with { PlayerId = 1, ClientRequestId = ++_request, Sequence = 0 });
            if (!result.Accepted && announce) Notify(result.Reason);
            if (result.Accepted && command.Type == GameCommandType.ContinueEndless) _recorded = false;
            if (result.Accepted || announce) Changed?.Invoke();
            return result.Accepted;
        }

        public void Select(int id, bool generator = false)
        {
            SelectedId = id;
            GeneratorSelected = generator;
            CancelPlacement();
            if (id > 0) Session?.TrySelectTower(id);
            Changed?.Invoke();
        }

        public void BeginPlacement(string id)
        {
            if (Session == null || !Session.IsTowerAvailable(id)) return;
            SelectedId = 0;
            GeneratorSelected = false;
            TacticalPlacement = TacticalPlacementKind.None;
            PlacementId = id;
            Changed?.Invoke();
        }

        public void BeginTactical(TacticalPlacementKind kind)
        {
            PlacementId = null;
            TacticalPlacement = kind;
            SelectedId = 0;
            GeneratorSelected = false;
            Changed?.Invoke();
        }

        public void CancelPlacement()
        {
            PlacementId = null;
            TacticalPlacement = TacticalPlacementKind.None;
            Session?.CancelPlacement();
            Changed?.Invoke();
        }

        public bool ResolvePlacement(SimVector cursor, out SimVector position)
        {
            position = cursor;
            if (Session == null) return false;
            if (PlacementId != null) return Session.TryResolveTowerPlacement(PlacementId, cursor, out position);
            if (TacticalPlacement == TacticalPlacementKind.PulsePlate) return Session.TryResolvePulsePlatePlacement(cursor, out position);
            if (TacticalPlacement == TacticalPlacementKind.ChargeForge) return Session.TryResolveChargeForgePlacement(cursor, out position);
            return false;
        }

        public void Place(SimVector cursor, bool repeat)
        {
            if (!ResolvePlacement(cursor, out var position)) { Notify("Choose a clear build platform."); return; }
            var type = PlacementId != null ? GameCommandType.PlaceTower :
                TacticalPlacement == TacticalPlacementKind.PulsePlate ? GameCommandType.DeployEmergencyDefense : GameCommandType.PlaceGenerator;
            if (Command(type, choice: PlacementId, position: position) && !repeat)
            {
                CancelPlacement();
                if (type == GameCommandType.PlaceTower) Select(Session.Towers[Session.Towers.Count - 1].Id);
                if (type == GameCommandType.PlaceGenerator) Select(0, true);
            }
        }

        public void Save(int slot = 1, bool announce = true)
        {
            try
            {
                if (Session == null || !Session.CanSaveCheckpoint) { if (announce) Notify("Save between waves."); return; }
                Saves.Save(Session, slot);
                if (announce) Notify("Run saved.");
            }
            catch (Exception e) { Debug.LogException(e); Notify("Save failed: " + e.Message); }
        }

        public bool Load(int slot)
        {
            try { Attach(Saves.Load(Content, slot)); return true; }
            catch (Exception e) { Debug.LogException(e); Notify("Could not load: " + e.Message); return false; }
        }

        public void Notify(string text)
        {
            Notice = text;
            _noticeTime = 4;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (_noticeTime > 0 && (_noticeTime -= Time.unscaledDeltaTime) <= 0) { Notice = ""; Changed?.Invoke(); }
            if (!IsRunning)
            {
                if (Session != null && Paused && HasFocus) Session.UpdatePausedIntermission(Mathf.Min(Time.unscaledDeltaTime, .1f));
                _accumulator = 0;
                return;
            }
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, .2f) * (VerificationMode ? Mathf.Clamp(VerificationRate, 1, 16) : 1);
            while (_accumulator >= Step && IsRunning)
            {
                BeforeStep?.Invoke();
                if (!IsRunning) break;
                Session.Update(Step);
                if (_checkpointPending)
                {
                    _checkpointPending = false;
                    if (Session.CanSaveCheckpoint) Save(0, false);
                }
                if (AutoWave) Session.TryAutoStartNextWave(true);
                Tick++;
                _accumulator -= Step;
                AfterStep?.Invoke();
                if (!_recorded && (Session.IsVictory || Session.IsDefeat))
                {
                    _recorded = true;
                    try { History.Upsert(RunHistoryEntry.FromSession(Session)); }
                    catch (Exception e) { Debug.LogException(e); Notify("Run history could not be saved."); }
                    Changed?.Invoke();
                }
            }
        }

        private void OnApplicationFocus(bool focus) { HasFocus = focus; _accumulator = 0; }
        private void OnApplicationQuit()
        {
            if (!VerificationMode) PlayerPrefs.SetInt("AutoWave", AutoWave ? 1 : 0);
            if (Session != null && Session.CanSaveCheckpoint) Save(0, false);
        }
    }
}
