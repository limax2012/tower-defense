using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaximalBastion.Core;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using UnityEngine;
using UnityEngine.UIElements;
using SimVector = MaximalBastion.Gameplay.Vector2;

namespace Bastion3D
{
    public sealed partial class GameInterface
    {
        private readonly List<string> _interfaceChecks = new List<string>();

        private IEnumerator VerifyInterface(string[] args)
        {
            int index = Array.IndexOf(args, "--capture-directory");
            string directory = index >= 0 && index + 1 < args.Length ? args[index + 1] :
                Path.Combine(Application.persistentDataPath, "InterfaceVerification");
            Directory.CreateDirectory(directory);
            bool previousAutoWave = _host.AutoWave;
            float previousVolume = AudioListener.volume, previousSensitivity = _camera.Sensitivity;
            int previousVsync = QualitySettings.vSyncCount;
            var preferenceKeys = new[] { "Volume", "CameraSensitivity", "VSync", "AutoWave" };
            var existingPreferences = preferenceKeys.ToDictionary(key => key, PlayerPrefs.HasKey);
            var work = new Stack<IEnumerator>();
            work.Push(InterfaceChecks(directory));
            string failure = null;
            while (work.Count > 0)
            {
                object next = null;
                try
                {
                    var task = work.Peek();
                    if (!task.MoveNext()) { work.Pop(); continue; }
                    next = task.Current;
                    if (next is IEnumerator nested) { work.Push(nested); continue; }
                }
                catch (Exception e) { failure = e.ToString(); break; }
                yield return next;
            }
            File.WriteAllText(Path.Combine(directory, "interface-verification.txt"),
                "Passed: " + (failure == null) + "\nChecks: " + _interfaceChecks.Count + "\n" +
                string.Join("\n", _interfaceChecks) + (failure == null ? "" : "\nFAIL: " + failure));
            _host.AutoWave = previousAutoWave;
            AudioListener.volume = previousVolume;
            _camera.Sensitivity = previousSensitivity;
            QualitySettings.vSyncCount = previousVsync;
            PlayerPrefs.SetFloat("Volume", previousVolume);
            PlayerPrefs.SetFloat("CameraSensitivity", previousSensitivity);
            PlayerPrefs.SetInt("VSync", previousVsync);
            PlayerPrefs.SetInt("AutoWave", previousAutoWave ? 1 : 0);
            foreach (var key in preferenceKeys) if (!existingPreferences[key]) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            if (failure != null) Debug.LogError(failure);
            Application.Quit(failure == null ? 0 : 1);
        }

        private IEnumerator InterfaceChecks(string directory)
        {
            Check(_host.VerificationMode, "Verification uses an isolated persistence directory");
            _host.AutoWave = false;
            var capture = ReviewCapture.Initialize(_viewCamera, this);
            yield return new WaitForSecondsRealtime(1);
            yield return Click("PLAY");
            Check(_page == Page.Setup, "Play opens unified setup");
            foreach (var difficulty in _host.Content.Difficulties.Values)
            {
                yield return Click(difficulty.DisplayName.ToUpperInvariant());
                Check(_difficulty == difficulty.Id, "Difficulty selection: " + difficulty.Id);
            }
            foreach (var mode in _host.Content.Challenges.Values)
            {
                yield return Click(mode.MenuLabel.ToUpperInvariant());
                Check(_mode == mode.Id, "Mode selection: " + mode.Id);
            }
            yield return Click("EASY");
            yield return Click(_host.Content.Challenges["standard"].MenuLabel.ToUpperInvariant());
            yield return Click("START RUN");
            Check(_page == Page.Hud && _host.Session.DifficultyId == "easy" && _host.Session.ChallengeId == "standard", "Setup creates requested run");
            yield return Click("1×");
            Check(_host.Session.Speed == 2, "Speed command: 2×");
            yield return Click("2×");
            Check(_host.Session.Speed == 1, "Speed command: 1×");
            yield return Click("PAUSE");
            Check(_host.Paused && _page == Page.Pause, "Pause suspends simulation");
            yield return Click("CONTROLS");
            Check(_page == Page.Controls, "Pause controls navigation");
            yield return Click("BACK");
            Check(_page == Page.Pause, "Controls returns to pause");
            yield return Click("SETTINGS");
            var sliders = _root.Query<Slider>().ToList();
            float volume = AudioListener.volume, sensitivity = _camera.Sensitivity;
            sliders[0].value = .43f;
            sliders[1].value = 1.37f;
            Check(Mathf.Abs(AudioListener.volume - .43f) < .001f && Mathf.Abs(_camera.Sensitivity - 1.37f) < .001f, "Settings sliders update audio and camera");
            sliders[0].value = volume;
            sliders[1].value = sensitivity;
            var toggles = _root.Query<Toggle>().ToList();
            bool vsync = toggles[0].value;
            toggles[0].value = !vsync;
            Check((QualitySettings.vSyncCount > 0) != vsync, "VSync setting updates runtime");
            toggles[0].value = vsync;
            toggles[2].value = true;
            Check(_host.AutoWave, "Auto-wave setting updates runtime");
            toggles[2].value = false;
            yield return Click("BACK");
            yield return Click("RESTART");
            string initialRun = _host.Session.RunId;
            yield return Click("CANCEL");
            Check(_host.Session.RunId == initialRun && _page == Page.Pause, "Restart cancellation preserves run");
            yield return Click("RESTART");
            yield return Click("CONFIRM");
            Check(_host.Session.RunId != initialRun && _page == Page.Hud, "Restart confirmation creates a fresh run");

            var needle = _host.Content.Towers["needle_turret"];
            yield return Click(needle.DisplayName.ToUpperInvariant() + "\n" + needle.PurchaseCost);
            Check(_host.PlacementId == needle.Id, "Tower toolbar enters placement");
            int credits = _host.Session.Economy.Credits;
            _host.Place(new SimVector(-200, -200), false);
            Check(_host.Session.Towers.Count == 0 && _host.Session.Economy.Credits == credits, "Invalid world placement cannot spend credits");
            yield return Click("CANCEL");
            Check(!_host.IsPlacing, "Placement cancel clears ghost state");
            yield return Click(needle.DisplayName.ToUpperInvariant() + "\n" + needle.PurchaseCost);
            PlaceThroughHost();
            yield return null;
            var tower = _host.Session.Towers.Single();
            Check(_host.SelectedTower == tower && _host.Session.Economy.Credits == credits - needle.PurchaseCost, "Valid placement spends credits and opens tower intel");
            var targets = _root.Query<DropdownField>().First();
            string target = targets.choices.Last();
            targets.value = target;
            yield return null;
            Check(tower.TargetMode.ToString() == target, "Target selector issues validated command");
            var doctrine = tower.Definition.Tier2Doctrines.First();
            yield return ClickPrefix(doctrine.DisplayName + "  ·");
            Check(tower.Doctrine.Id == doctrine.Id, "Doctrine button upgrades the selected tower");
            string run = _host.Session.RunId;
            int savedCredits = _host.Session.Economy.Credits;
            yield return Click("PAUSE");
            yield return Click("SAVE");
            yield return Click("SAVE");
            Check(_host.Saves.GetSlots().Any(s => s.Slot == 1 && s.IsOccupied), "Save button writes slot 1");
            yield return Click("BACK");
            yield return Click("RESUME");
            yield return ClickPrefix("SELL  +");
            Check(_host.Session.Towers.Count == 0, "Sell removes tower through validated command");
            yield return Click("PAUSE");
            yield return Click("LOAD");
            yield return Click("LOAD");
            tower = _host.Session.Towers.Single();
            Check(_host.Session.RunId == run && _host.Session.Economy.Credits == savedCredits && tower.Doctrine.Id == doctrine.Id && tower.TargetMode.ToString() == target,
                "Load restores run ID, credits, tower doctrine and targeting");
            _host.Select(tower.Id);
            yield return null;
            yield return Click("AUTO");
            Check(_host.Session.AutoOverdriveTowerId == tower.Id, "Auto protocol selection");
            yield return Click("AUTO");
            Check(_host.Session.AutoOverdriveTowerId == 0, "Auto protocol deselection");
            yield return Click(tower.Protocol.DisplayName.ToUpperInvariant());
            Check(tower.IsOverdriven && _host.Session.OverdriveCooldownRemaining > 0, "Manual protocol activates and starts cooldown");
            yield return new WaitForSecondsRealtime(.2f);
            Check(!_root.Query<Button>().ToList().First(b => b.parent.ClassListContains("protocol-controls")).enabledSelf,
                "Protocol control disables while active or cooling down");
            yield return Snap("00-intel-commands");
            yield return Click("PAUSE");
            yield return Click("LOAD");
            yield return Click("DELETE");
            yield return Click("CANCEL");
            Check(_host.Saves.GetSlots().Any(s => s.Slot == 1 && s.IsOccupied), "Delete cancellation preserves save");
            yield return Click("DELETE");
            yield return Click("CONFIRM");
            Check(!_host.Saves.GetSlots().Any(s => s.Slot == 1 && s.IsOccupied), "Delete confirmation removes save");
            yield return Click("BACK");
            yield return Click("MAIN MENU");
            yield return Click("CONFIRM");
            Check(_host.Session == null && _page == Page.Home, "Leave run returns to main menu");

            yield return Click("PLAY");
            yield return Click("START RUN");
            yield return ClickPrefix("FORGE");
            PlaceThroughHost();
            yield return null;
            Check(_host.Session.Generator != null && _host.GeneratorSelected, "Forge placement opens forge intel");
            Check(!FindButton("UPGRADE  ·", true).enabledInHierarchy, "Unaffordable forge upgrade is disabled");
            // A funded checkpoint isolates the upgrade controls from campaign income timing.
            _host.Session.Economy.AddCredits(10000);
            yield return new WaitForSecondsRealtime(.2f);
            while (_host.Session.Generator.CanUpgrade)
            {
                int level = _host.Session.Generator.LevelIndex;
                yield return ClickPrefix("UPGRADE  ·");
                Check(_host.Session.Generator.LevelIndex == level + 1, "Forge upgrade level " + (level + 2));
            }
            yield return Snap("01-forge-upgrades");
            yield return ClickPrefix("SELL  +");
            Check(_host.Session.Generator == null, "Forge sell removes generator");
            yield return Click("PULSE PLATE");
            PlaceThroughHost();
            yield return null;
            Check(_host.Session.EmergencyDefenses.Count == 1 && _host.Session.EmergencyInventory == 0, "Pulse Plate uses stored charge");
            Check(!FindButton("PULSE PLATE").enabledInHierarchy, "Empty plate inventory disables intermission purchase");
            yield return Click("START WAVE");
            Check(_host.Session.Waves.IsActive && _host.Session.CurrentWave == 1, "Start Wave issues validated command");
            yield return new WaitForSecondsRealtime(.2f);
            Check(FindButton("PULSE PLATE").enabledInHierarchy, "During-wave plate purchase becomes available");
            credits = _host.Session.Economy.Credits;
            yield return Click("PULSE PLATE");
            PlaceThroughHost();
            yield return null;
            Check(_host.Session.EmergencyDirectPurchasesThisWave == 1 && _host.Session.Economy.Credits < credits, "Direct plate purchase spends credits during wave");
            yield return Click("PAUSE");
            yield return Click("SAVE");
            Check(_root.Query<Button>().ToList().Where(b => b.text == "SAVE").All(b => !b.enabledInHierarchy), "Save actions are disabled during active wave");
            yield return Click("BACK");
            yield return Click("MAIN MENU");
            yield return Click("CONFIRM");

            yield return Click("PLAY");
            yield return Click(_host.Content.Challenges.Values.First(c => c.IsSandbox).MenuLabel.ToUpperInvariant());
            yield return Click("START RUN");
            Check(_host.Session.IsSandbox, "Sandbox setup uses sandbox simulation");
            foreach (var definition in _host.Content.Towers.Values)
            for (int doctrineIndex = 0; doctrineIndex < Math.Max(1, definition.Tier2Doctrines.Count); doctrineIndex++)
            for (int specializationIndex = 0; specializationIndex < Math.Max(1, definition.Specializations.Count); specializationIndex++)
            {
                yield return Click(definition.DisplayName.ToUpperInvariant() + "\n" + definition.PurchaseCost);
                PlaceThroughHost();
                yield return null;
                tower = _host.Session.Towers.Last();
                Check(tower.Definition.Id == definition.Id, "Roster placement: " + definition.Id);
                if (tower.RequiresDoctrine)
                {
                    var choice = definition.Tier2Doctrines[doctrineIndex];
                    FindButton(choice.DisplayName + "  ·", true).Focus();
                    yield return null;
                    Check(_previewStatsLevel != null && _statsTitle.text == "UPGRADE PREVIEW", "Upgrade focus previews: " + choice.Id);
                    CheckStatLayout();
                    yield return ClickPrefix(choice.DisplayName + "  ·");
                    Check(tower.Doctrine.Id == choice.Id, "Roster doctrine: " + definition.Id + " / " + choice.Id);
                }
                if (tower.RequiresSpecialization)
                {
                    var choice = definition.Specializations[specializationIndex];
                    yield return ClickPrefix(choice.DisplayName + "  ·");
                    Check(tower.Specialization.Id == choice.Id, "Roster specialization: " + definition.Id + " / " + choice.Id);
                }
                while (tower.CanUpgrade) yield return ClickPrefix("UPGRADE  ·");
                if (definition.Apex != null)
                {
                    yield return ClickPrefix("APEX  ·");
                    Check(tower.IsApex, "Roster Apex: " + definition.Id);
                    Check(!_root.Query<Button>().ToList().Any(b => b.text == "AUTO"), "Apex omits manual auto toggle: " + definition.Id);
                }
                if (definition.Id == "prism_beam") yield return Snap("02-prism-apex");
                yield return ClickPrefix("SELL  +");
                Check(_host.Session.Towers.Count == 0, "Roster sell: " + definition.Id);
            }
            yield return Click("SPAWN 5");
            Check(_host.Session.Enemies.Count == 5, "Sandbox target spawn control");
            yield return Click("CLEAR TARGETS");
            Check(_host.Session.Enemies.Count == 0, "Sandbox target clear control");
            yield return Click("RESET PROTOCOLS");
            Check(_host.Session.OverdriveCooldownRemaining == 0, "Sandbox protocol reset control");
            var wave = _root.Query<IntegerField>().First();
            wave.value = 15;
            yield return Click("PLAY WAVE");
            Check(_host.Session.SandboxWaveActive && _host.Session.AnnouncementTitle.StartsWith("TEST WAVE 15", StringComparison.Ordinal), "Sandbox authored-wave control");
            yield return Snap("03-sandbox-wave");

            _host.NewRun("easy", "standard");
            _host.Session.Economy.AddCredits(20000);
            yield return null;
            yield return ClickPrefix("FORGE");
            PlaceThroughHost();
            yield return null;
            foreach (string id in new[] { "needle_turret", "watchtower", "prism_beam", "arc_relay", "siege_mortar" })
            {
                var definition = _host.Content.Towers[id];
                yield return Click(definition.DisplayName.ToUpperInvariant() + "\n" + definition.PurchaseCost);
                PlaceThroughHost();
                yield return null;
                tower = _host.SelectedTower;
                yield return ClickPrefix(definition.Tier2Doctrines.First().DisplayName + "  ·");
                yield return ClickPrefix(definition.Specializations.First().DisplayName + "  ·");
            }
            yield return Click("FORGE");
            yield return Click("START WAVE");
            yield return Click("1×");
            var liveChargeLabel = _forgeCharge;
            string initialCharge = liveChargeLabel.text;
            yield return new WaitForSecondsRealtime(1.2f);
            Check(_forgeCharge == liveChargeLabel && liveChargeLabel.text != initialCharge &&
                Math.Abs(float.Parse(liveChargeLabel.text.TrimEnd('s')) - _host.Session.Generator.ProductionRemaining) < 1,
                "Forge production label refreshes without rerendering inspector");
            tower = _host.Session.Towers.First(t => t.Definition.Id == "watchtower");
            _host.Select(tower.Id);
            yield return null;
            var contributions = _root.Query<Foldout>().First();
            contributions.value = true;
            var liveDamageLabel = _contributionDamage;
            string initialDamage = liveDamageLabel.text;
            double damageDeadline = Time.realtimeSinceStartupAsDouble + 15;
            while (liveDamageLabel.text == initialDamage && Time.realtimeSinceStartupAsDouble < damageDeadline)
                yield return new WaitForSecondsRealtime(.2f);
            Check(_contributionDamage == liveDamageLabel && liveDamageLabel.text != initialDamage &&
                liveDamageLabel.text == tower.LifetimeDamage.ToString("N0"), "Contribution values refresh while the same inspector remains open");
            double waveDeadline = Time.realtimeSinceStartupAsDouble + 60;
            while (_host.Session.Waves.IsActive && !_host.Session.IsDefeat && Time.realtimeSinceStartupAsDouble < waveDeadline)
                yield return new WaitForSecondsRealtime(.2f);
            Check(!_host.Session.Waves.IsActive && !_host.Session.IsDefeat && _host.Session.CurrentWave == 1, "Live wave finishes with valid checkpoint");
            yield return null;
            Check(_host.Saves.GetSlots().Any(s => s.Slot == 0 && s.IsOccupied && s.CurrentWave == 1), "Wave-end autosave writes after simulation cleanup");
            string completedRun = _host.Session.RunId;
            yield return Click("PAUSE");
            yield return Click("LOAD");
            yield return Click("LOAD");
            Check(_host.Session.RunId == completedRun && _host.Session.CurrentWave == 1 && _host.Session.Towers.Count == 5 &&
                _host.Session.Generator != null && _host.Session.CanSaveCheckpoint, "Autosave restores the completed wave and constructed defenses");
            yield return Snap("07-autosave-restored");

            // Validated late-run fixtures exercise result navigation without replaying a full campaign.
            var resultFixture = new MaximalBastion.GameSession(_host.Content, "foundry_loop", "easy", "standard").CaptureSaveGame();
            resultFixture.Waves.CurrentWaveNumber = 30;
            resultFixture.Waves.IsFinalWaveCleared = true;
            _host.Attach(MaximalBastion.GameSession.RestoreSaveGame(_host.Content, resultFixture));
            yield return new WaitForSecondsRealtime(.3f);
            Check(_host.Session.IsVictory && _page == Page.Results, "Completed-wave fixture reaches actual Victory state");
            yield return Snap("04-victory");
            yield return Click("CONTINUE ENDLESS");
            Check(_host.Session.IsEndlessMode && !_host.Session.IsVictory && _page == Page.Hud, "Continue Endless updates simulation and resumes HUD");
            var defeat = new MaximalBastion.GameSession(_host.Content, "foundry_loop", "easy", "standard");
            defeat.Economy.LoseLives(defeat.Economy.Lives);
            _host.Attach(defeat);
            yield return new WaitForSecondsRealtime(.3f);
            Check(defeat.IsDefeat && _page == Page.Results, "Zero-integrity fixture reaches actual Defeat state");
            yield return Snap("05-defeat");
            yield return Click("HISTORY");
            Check(_host.History.GetEntries().Any(e => !e.Victory) && _host.History.GetEntries().Any(e => e.Victory), "Results persist both completed outcomes");
            yield return Click("DETAILS");
            Check(_root.Query<Label>().ToList().Any(l => l.text == "Top tower"), "History details opens recorded run");
            yield return Click("BACK");
            yield return Click("BACK");
            Check(_page == Page.Results, "History returns to originating results");
            yield return Click("RESTART");
            Check(!_host.Session.IsDefeat && !_host.Session.IsVictory && _page == Page.Hud, "Result restart creates playable run");
            yield return Click("PAUSE");
            yield return Click("MAIN MENU");
            yield return Click("CONFIRM");
            yield return Click("LOAD");
            Check(_page == Page.Saves, "Home load navigation");
            yield return Click("BACK");
            yield return Click("HISTORY");
            Check(_page == Page.History, "Home history navigation");
            yield return Click("BACK");
            yield return Snap("06-complete");

            IEnumerator Snap(string name)
            {
                yield return new WaitForSecondsRealtime(.2f);
                yield return new WaitForEndOfFrame();
                capture.Capture(Path.Combine(directory, name + ".png"));
            }
        }

        private void Check(bool valid, string label)
        {
            if (!valid) throw new InvalidOperationException("Interface verification: " + label);
            _interfaceChecks.Add("PASS: " + label);
        }

        private void CheckStatLayout()
        {
            foreach (var cell in _stats.Children())
            {
                var labels = cell.Query<Label>().ToList();
                Check(labels.Count == 2 && labels[0].worldBound.yMax <= labels[1].worldBound.yMin + .5f &&
                    labels[1].worldBound.yMax <= cell.worldBound.yMax + .5f, "Stat labels and values stay inside their cells");
                var value = labels[1];
                float textWidth = value.MeasureTextSize(value.text, 0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined).x;
                Check(textWidth <= cell.resolvedStyle.width - cell.resolvedStyle.paddingRight + 1,
                    "Complete stat preview fits: " + value.text);
            }
        }

        private Button FindButton(string text, bool prefix = false)
        {
            var button = _root.Query<Button>().ToList().FirstOrDefault(b => prefix ? b.text.StartsWith(text, StringComparison.Ordinal) : b.text == text);
            if (button == null) throw new InvalidOperationException("Missing interface control: " + text + " on " + _page);
            return button;
        }

        private IEnumerator ClickPrefix(string text) => Click(text, true);
        private IEnumerator Click(string text, bool prefix = false)
        {
            yield return null;
            var button = FindButton(text, prefix);
            if (!button.enabledInHierarchy) throw new InvalidOperationException("Disabled interface control: " + text);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return null;
            yield return null;
        }

        private void PlaceThroughHost()
        {
            for (int y = 90; y < 640; y += 20)
            for (int x = 40; x < 1180; x += 20)
            {
                var point = new SimVector(x, y);
                if (!_host.ResolvePlacement(point, out _)) continue;
                _host.Place(point, false);
                if (!_host.IsPlacing) return;
            }
            throw new InvalidOperationException("No valid placement for " + _host.PlacementId + " / " + _host.TacticalPlacement);
        }
    }
}
