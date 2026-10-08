using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using UnityEngine;

namespace Bastion3D
{
    public sealed class CampaignReplay : MonoBehaviour
    {
        private BastionApplication _app;
        private MaximalBastion.Gameplay.CampaignReplayController _controller;
        private ReviewCapture _capture;
        private readonly List<string> _checks = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private string _directory;
        private int _wave, _peakEnemies, _peakTowers;
        private bool _aborted;

        public void Initialize(BastionApplication app)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--campaign-replay")) return;
            _app = app;
            _directory = Argument(args, "--capture-directory") ?? Path.Combine(Application.persistentDataPath, "CampaignReplay");
            Directory.CreateDirectory(_directory);
            var path = Argument(args, "--replay-file") ?? Path.Combine(Application.streamingAssetsPath, "Verification/campaign-replay.json");
            _controller = new MaximalBastion.Gameplay.CampaignReplayController(File.ReadAllText(path));
            _capture = ReviewCapture.Initialize(app.CameraRig.GetComponent<Camera>(), app.Interface);
            Application.logMessageReceived += Log;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            var host = _app.Host;
            host.AutoWave = false;
            host.Attach(_controller.CreateSession(host.Content));
            host.VerificationRate = 8;
            host.BeforeStep += BeforeStep;
            host.AfterStep += AfterStep;
            float timeout = Time.realtimeSinceStartup + 600;
            while (!_aborted && host.Tick < _controller.Data.TickCount + 7200 && !host.Session.IsDefeat && !host.Session.IsVictory && Time.realtimeSinceStartup < timeout)
                yield return null;
            host.Paused = true;
            host.BeforeStep -= BeforeStep;
            host.AfterStep -= AfterStep;
            bool won = host.Session.IsVictory && host.Session.CurrentWave == 30 && _controller.CommandsComplete;
            if (!won) _errors.Add("The authored campaign did not reach the expected complete victory.");
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            _capture.Capture(Path.Combine(_directory, won ? "campaign-victory.png" : "campaign-failure.png"));
            if (won)
            {
                if (!host.History.GetEntries().Any()) _errors.Add("Completed run was not recorded in history.");
                if (!host.Command(GameCommandType.ContinueEndless)) _errors.Add("Continue Endless rejected after victory.");
                if (!host.Command(GameCommandType.StartWave)) _errors.Add("Wave 31 did not start.");
                else _checks.Add("Continue Endless starts wave 31 through validated commands.");
            }
            File.WriteAllText(Path.Combine(_directory, "campaign-report.txt"),
                $"Victory: {won}\nEmbedded Unity runtime: {Application.unityVersion} / {Environment.Version}\nTicks: {host.Tick}\nAccepted commands: {_controller.AcceptedCommands}/{_controller.Data.Commands.Count}\n" +
                $"Remaining lives: {host.Session.Economy.Lives}\nCredits: {host.Session.Economy.Credits}\nPeak enemies: {_peakEnemies}\nPeak towers: {_peakTowers}\n" +
                $"Wave-boundary waits: {_controller.TotalDelayTicks} ticks\nDeferred tactical commands: {_controller.DeferredCommands}\nPending commands: {_controller.PendingCommands}\n" +
                "Accelerated fixed-step acceptance driver with explicit legal waits and retries; not a lockstep or rendering performance benchmark.\n" + string.Join("\n", _checks.Concat(_controller.Events)) + "\nErrors: " + _errors.Count + "\n" + string.Join("\n", _errors));
            Application.Quit(won && _errors.Count == 0 ? 0 : 1);
        }

        private void BeforeStep()
        {
            var host = _app.Host;
            if (!_controller.PrepareTick(host.Session, host.Tick, command => host.Command(command)))
            {
                _errors.Add(_controller.Failure);
                _aborted = true; host.Paused = true;
            }
        }
        private void AfterStep()
        {
            var session = _app.Host.Session;
            _peakEnemies = Mathf.Max(_peakEnemies, session.Enemies.Count);
            _peakTowers = Mathf.Max(_peakTowers, session.Towers.Count);
            if (session.CurrentWave != _wave)
            {
                _wave = session.CurrentWave;
                _checks.Add($"Wave {_wave}: tick {_app.Host.Tick}, lives {session.Economy.Lives}, credits {session.Economy.Credits}, towers {session.Towers.Count}");
                File.WriteAllText(Path.Combine(_directory, "campaign-progress.txt"), string.Join("\n", _checks));
            }
        }
        private static string Argument(string[] args, string key)
        { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        private void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) { _errors.Add(message); _aborted = true; } }
        private void OnDestroy() => Application.logMessageReceived -= Log;
    }
}
