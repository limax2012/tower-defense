using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion3D
{
    public sealed partial class GameInterface : MonoBehaviour
    {
        private enum Page { Home, Setup, Hud, Pause, Saves, History, Settings, Controls, Results }
        private SimulationHost _host;
        private BattlefieldCamera _camera;
        private UIDocument _document;
        private PanelSettings _panel;
        private VisualElement _root;
        private VisualElement _stats;
        private Label _statsTitle;
        private TowerLevelDefinition _previewStatsLevel;
        private string _statsKey = "";
        private Label _health;
        private Label _credits;
        private Label _wave;
        private Label _notice;
        private Label _hint;
        private Label _forgeCharge;
        private Label _contributionDamage;
        private Label _contributionKills;
        private Label _contributionAssist;
        private BattlefieldView _battlefield;
        private Camera _viewCamera;
        private VisualElement _enemyCard;
        private VisualElement _enemyHealthFill;
        private Label _enemyName;
        private Label _enemyValues;
        private Label _enemyStatus;
        private Label _tooltip;
        private string _hoverTip = "";
        private float _hoverTipSince;
        private Button _waveButton;
        private Button _speedButton;
        private Page _page;
        private Page _returnPage;
        private string _difficulty = "normal";
        private string _mode = "standard";
        private string _seenResult = "";
        private bool _saveMode;
        private bool _dirty;
        private float _nextRefresh;
        private readonly List<Action> _enabledRules = new List<Action>();

        public void Initialize(SimulationHost host, BattlefieldCamera camera)
        {
            _host = host;
            _camera = camera;
            _viewCamera = camera.GetComponent<Camera>();
            _battlefield = FindFirstObjectByType<BattlefieldView>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.name = "Bastion Interface";
            _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panel.referenceResolution = new Vector2Int(1280, 720);
            _panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panel.match = .5f;
            _panel.sortingOrder = 100;
            _panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("BastionTheme");
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panel;
            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            _root.AddToClassList("bastion-ui");
            var sheet = Resources.Load<StyleSheet>("Interface");
            if (sheet != null) _root.styleSheets.Add(sheet);
            var font = Resources.Load<Font>("Fonts/BarlowSemiCondensed-Medium");
            if (font != null) _root.style.unityFontDefinition = FontDefinition.FromFont(font);
            _host.Changed += MarkDirty;
            _host.SessionChanged += SessionChanged;
            AudioListener.volume = PlayerPrefs.GetFloat("Volume", .7f);
            _camera.Sensitivity = PlayerPrefs.GetFloat("CameraSensitivity", 1);
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync", 1);
            Show(_host.Session == null ? Page.Home : Page.Hud);
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--ui-verify")) StartCoroutine(VerifyInterface(args));
            else if (args.Contains("--ui-review")) StartCoroutine(CaptureReview(args));
        }

        public bool PointerOverInterface()
        {
            if (_root == null || _root.panel == null) return false;
            if (_page != Page.Hud) return true;
            var mouse = Input.mousePosition;
            var point = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            var picked = _root.panel.Pick(point);
            while (picked != null)
            {
                if (picked.ClassListContains("ui-surface")) return true;
                picked = picked.parent;
            }
            return false;
        }

        public void SetCaptureTarget(RenderTexture target)
        {
            _panel.targetTexture = target;
            _panel.clearColor = false;
            _panel.clearDepthStencil = true;
        }

        private void MarkDirty() { _dirty = true; }
        private void SessionChanged() { _seenResult = ""; Show(_host.Session == null ? Page.Home : Page.Hud); }

        private void Update()
        {
            if (_host == null) return;
            if (_host.HasFocus && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_page == Page.Hud && _host.IsPlacing) _host.CancelPlacement();
                else if (_page == Page.Hud) Show(Page.Pause);
                else if (_page == Page.Pause) Show(Page.Hud);
                else if (_page != Page.Home && _page != Page.Results) Back();
            }
            if (_page == Page.Hud && _host.HasFocus && Input.GetKeyDown(KeyCode.Space) && !_host.Session.IsSandbox)
                _host.Command(GameCommandType.StartWave);
            var session = _host.Session;
            if (session != null && (session.IsVictory || session.IsDefeat))
            {
                string result = session.RunId + ":" + session.CurrentWave;
                if (_seenResult != result) { _seenResult = result; Show(Page.Results); }
            }
            if (_dirty) { _dirty = false; if (_page == Page.Hud) Render(); }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + .15f;
                RefreshHud();
                foreach (var rule in _enabledRules) rule();
            }
        }

        private void LateUpdate()
        {
            UpdateTooltip();
            if (_enemyCard == null || _page != Page.Hud || _host.Session == null || _battlefield == null) return;
            var enemy = _host.Session.Enemies.FirstOrDefault(e => e.Id == _battlefield.HoverEnemy && !e.IsDead && !e.HasEscaped);
            if (enemy == null) enemy = _host.Session.Enemies.FirstOrDefault(e => e.IsBoss && !e.IsDead && !e.HasEscaped);
            if (enemy == null) { _enemyCard.style.display = DisplayStyle.None; return; }
            var point = _viewCamera.WorldToViewportPoint(_battlefield.EnemyPosition(enemy) + Vector3.up * .6f);
            if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1)
            { _enemyCard.style.display = DisplayStyle.None; return; }
            _enemyCard.style.display = DisplayStyle.Flex;
            _enemyName.text = enemy.DisplayName;
            _enemyValues.text = Mathf.CeilToInt(enemy.Health) + " / " + Mathf.CeilToInt(enemy.MaxHealth) +
                (enemy.Shield > 0 ? "   +" + Mathf.CeilToInt(enemy.Shield) + " SHIELD" : "");
            _enemyHealthFill.style.width = Length.Percent(Mathf.Clamp01(enemy.Health / enemy.MaxHealth) * 100);
            var status = new List<string>();
            if (enemy.StatusEffects.IsBurning) status.Add("BURN");
            if (enemy.StatusEffects.IsStunned) status.Add("STUN");
            else if (enemy.StatusEffects.SlowFactor > 0) status.Add("SLOW");
            if (enemy.StatusEffects.ArmorReduction > 0) status.Add("BREAK");
            if (enemy.StatusEffects.DamageMultiplier > 1) status.Add("EXPOSE");
            _enemyStatus.text = string.Join(" · ", status);
            _enemyStatus.style.display = status.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            float rightMargin = _host.SelectedTower != null || _host.GeneratorSelected || _host.Session.IsSandbox ? 330 : 18;
            _enemyCard.style.left = Mathf.Clamp(point.x * width - 110, 18, Mathf.Max(18, width - rightMargin - 220));
            _enemyCard.style.top = Mathf.Clamp((1 - point.y) * height - 78, 86, Mathf.Max(86, height - 240));
        }

        private void UpdateTooltip()
        {
            if (_tooltip == null || _root.panel == null) return;
            if (!_host.HasFocus) { _tooltip.style.display = DisplayStyle.None; return; }
            var mouse = Input.mousePosition;
            var point = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            var item = _root.panel.Pick(point);
            while (item != null && string.IsNullOrEmpty(item.tooltip)) item = item.parent;
            string tip = item?.tooltip ?? "";
            if (tip != _hoverTip) { _hoverTip = tip; _hoverTipSince = Time.unscaledTime; }
            if (tip.Length == 0 || Time.unscaledTime - _hoverTipSince < .4f)
            { _tooltip.style.display = DisplayStyle.None; return; }
            _tooltip.text = tip;
            _tooltip.style.display = DisplayStyle.Flex;
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            _tooltip.style.left = Mathf.Clamp(point.x + 14, 12, Mathf.Max(12, width - 302));
            float tooltipHeight = float.IsNaN(_tooltip.resolvedStyle.height) ? 80 : _tooltip.resolvedStyle.height;
            _tooltip.style.top = Mathf.Clamp(point.y - tooltipHeight - 14, 12, Mathf.Max(12, height - tooltipHeight - 12));
        }

        private void Show(Page page)
        {
            _page = page;
            _host.Paused = page != Page.Hud && _host.Session != null;
            _dirty = false;
            Render();
        }

        private void OpenSecondary(Page page)
        {
            _returnPage = _page;
            Show(page);
        }

        private void Back() { Show(_host.Session == null ? Page.Home : (_returnPage == Page.Results ? Page.Results : _returnPage == Page.Hud ? Page.Hud : Page.Pause)); }

        private void Render()
        {
            _root.Clear();
            _enabledRules.Clear();
            _health = _credits = _wave = _notice = _hint = null;
            _forgeCharge = _contributionDamage = _contributionKills = _contributionAssist = null;
            _waveButton = _speedButton = null;
            _enemyCard = null;
            _stats = null;
            _statsTitle = null;
            _previewStatsLevel = null;
            _statsKey = "";
            switch (_page)
            {
                case Page.Home: Home(); break;
                case Page.Setup: Setup(); break;
                case Page.Hud: Hud(); break;
                case Page.Pause: Pause(); break;
                case Page.Saves: Saves(); break;
                case Page.History: History(); break;
                case Page.Settings: Settings(); break;
                case Page.Controls: Controls(); break;
                case Page.Results: Results(); break;
            }
            _tooltip = Text(_root, "", "runtime-tooltip");
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;
            _hoverTip = "";
        }

        private VisualElement Panel(string title, bool narrow = false)
        {
            var shade = Element(_root, "modal-shade");
            var page = Element(shade, "ui-surface", "page");
            if (narrow) page.AddToClassList("narrow");
            if (!string.IsNullOrEmpty(title)) Text(page, title, "page-title");
            return page;
        }

        private void Home()
        {
            var page = Panel("", true);
            var brand = Element(page, "brand-lockup");
            var emblem = new Image { image = Resources.Load<Texture2D>("Brand/Prism"), scaleMode = ScaleMode.ScaleToFit };
            emblem.AddToClassList("brand-emblem");
            brand.Add(emblem);
            var title = Element(brand, "brand-words");
            Text(title, "RELAY", "eyebrow");
            Text(title, "BASTION", "brand");
            Button(page, "PLAY", () => Show(Page.Setup), "primary");
            Button(page, "LOAD", () => { _saveMode = false; OpenSecondary(Page.Saves); }, "blue");
            Button(page, "HISTORY", () => OpenSecondary(Page.History), "green");
            Button(page, "SETTINGS", () => OpenSecondary(Page.Settings), "violet");
            Button(page, "CONTROLS", () => OpenSecondary(Page.Controls));
            Button(page, "QUIT", Quit, "danger");
        }

        private void Setup()
        {
            var page = Panel("New Game");
            var map = Element(page, "map-card");
            Text(map, "Cinderworks", "map-name");
            Text(map, "Industrial platforms · 30 waves · Endless", "muted");
            Text(page, "DIFFICULTY", "section-title");
            var difficulties = Element(page, "choices");
            foreach (string id in new[] { "easy", "normal", "hard", "bastion" })
            {
                if (!_host.Content.Difficulties.TryGetValue(id, out var item)) continue;
                string captured = id;
                var b = Button(difficulties, item.DisplayName.ToUpperInvariant(), () => { _difficulty = captured; Render(); });
                if (id == _difficulty) b.AddToClassList("selected");
            }
            if (_host.Content.Difficulties.TryGetValue(_difficulty, out var difficulty))
                Text(page, $"Enemy health {difficulty.EnemyHealthMultiplier:P0} · Speed {difficulty.EnemySpeedMultiplier:P0} · {difficulty.StartingLives} integrity", "muted");
            Text(page, "MODE", "section-title");
            var modes = Element(page, "choices");
            foreach (var item in _host.Content.Challenges.Values)
            {
                string captured = item.Id;
                var b = Button(modes, item.MenuLabel.ToUpperInvariant(), () => { _mode = captured; Render(); });
                if (item.Id == _mode) b.AddToClassList("selected");
            }
            if (_host.Content.Challenges.TryGetValue(_mode, out var mode)) Text(page, mode.Description, "muted");
            var actions = Element(page, "actions");
            Button(actions, "START RUN", () => { _host.NewRun(_difficulty, _mode); _camera.Overview(); }, "primary");
            Button(actions, "BACK", () => Show(Page.Home));
        }

        private void Hud()
        {
            var session = _host.Session;
            if (session == null) { Show(Page.Home); return; }
            var top = Element(_root, "ui-surface", "top-bar");
            _health = Counter(top, "INTEGRITY");
            _credits = Counter(top, "CREDITS");
            _wave = Counter(top, "WAVE");
            Element(top, "grow");
            _waveButton = Button(top, "START WAVE", () => _host.Command(GameCommandType.StartWave), "blue");
            Enable(_waveButton, () => session.CanStartWave && !session.IsSandbox);
            _speedButton = Button(top, "1×", () => _host.Command(GameCommandType.SetSpeed, speed: session.Speed > 1 ? 1 : 2));
            Button(top, "PAUSE", () => Show(Page.Pause));
            var roster = Element(_root, "ui-surface", "build-bar");
            foreach (var tower in _host.Content.Towers.Values)
            {
                var definition = tower;
                var b = Button(roster, definition.DisplayName.ToUpperInvariant() + "\n" + definition.PurchaseCost,
                    () => _host.BeginPlacement(definition.Id), "tower-button");
                b.tooltip = definition.Role;
                if (ColorUtility.TryParseHtmlString(definition.Visual.Accent, out var color)) b.style.borderTopColor = color;
                if (_host.PlacementId == definition.Id) b.AddToClassList("selected");
                Enable(b, () => session.IsTowerAvailable(definition.Id) && session.Economy.CanAfford(definition.PurchaseCost));
            }
            var tactics = Element(_root, "ui-surface", "tactics");
            if (session.PulsePlatesEnabled)
            {
                var plate = Button(tactics, "PULSE PLATE", () => _host.BeginTactical(TacticalPlacementKind.PulsePlate), "small");
                plate.tooltip = "Place on the road. Uses a stored charge, or buys one during a wave.";
                Enable(plate, () => session.EmergencyInventory > 0 || session.CanDirectPurchaseEmergencyDefense || session.IsSandbox);
            }
            if (session.TacticalSystemsEnabled)
            {
                var forge = Button(tactics, session.Generator == null ? "FORGE  " + _host.Content.Tactics.Generator.PurchaseCost : "FORGE",
                    () => { if (session.Generator == null) _host.BeginTactical(TacticalPlacementKind.ChargeForge); else _host.Select(0, true); }, "small");
                forge.tooltip = "Produces Pulse Plate charges.";
                Enable(forge, () => session.Generator != null || session.Economy.CanAfford(_host.Content.Tactics.Generator.PurchaseCost));
            }
            Button(tactics, "CAMERA", () => _camera.Overview(), "small").tooltip = "Reset to overview [Home]. Right-drag orbit; middle-drag pan; wheel zoom; F free flight.";
            if (_host.IsPlacing) Button(tactics, "CANCEL", () => _host.CancelPlacement(), "small");
            _hint = Text(_root, "", "hint");
            _hint.pickingMode = PickingMode.Ignore;
            _notice = Text(_root, "", "notice");
            _notice.pickingMode = PickingMode.Ignore;
            _enemyCard = Element(_root, "enemy-card");
            _enemyCard.pickingMode = PickingMode.Ignore;
            _enemyCard.style.display = DisplayStyle.None;
            _enemyName = Text(_enemyCard, "", "enemy-name");
            var track = Element(_enemyCard, "enemy-health-track");
            _enemyHealthFill = Element(track, "enemy-health-fill");
            _enemyValues = Text(_enemyCard, "", "enemy-values");
            _enemyStatus = Text(_enemyCard, "", "enemy-status");
            foreach (var child in _enemyCard.Query<VisualElement>().ToList()) child.pickingMode = PickingMode.Ignore;
            if (_host.SelectedTower != null) TowerIntel(_host.SelectedTower);
            else if (_host.GeneratorSelected && session.Generator != null) ForgeIntel();
            else if (session.IsSandbox) Sandbox();
            RefreshHud();
        }

        private void RefreshHud()
        {
            var session = _host.Session;
            if (_page != Page.Hud || session == null || _health == null) return;
            _health.text = session.IsSandbox ? "∞" : session.Economy.Lives.ToString();
            _credits.text = session.IsSandbox ? "∞" : session.Economy.Credits.ToString("N0");
            _wave.text = session.IsSandbox ? "SANDBOX" : $"{session.CurrentWave} / {(session.IsEndlessMode ? "∞" : session.TotalWaves.ToString())}";
            _waveButton.text = session.Waves.IsActive ? "IN PROGRESS" : "START WAVE";
            _speedButton.text = session.Speed > 1 ? "2×" : "1×";
            _notice.text = _host.Notice;
            _notice.style.display = string.IsNullOrEmpty(_host.Notice) ? DisplayStyle.None : DisplayStyle.Flex;
            _hint.text = _host.IsPlacing ? "Click to deploy · Shift repeats · Esc cancels" :
                (session.EmergencyInventory > 0 ? $"{session.EmergencyInventory} Pulse Plate charge{(session.EmergencyInventory == 1 ? "" : "s")}" : "Right-drag orbit · Wheel zoom · F free flight");
            if (_stats != null && _host.SelectedTower != null) ShowStats(_host.SelectedTower, _previewStatsLevel);
            if (_forgeCharge != null && session.Generator != null)
                _forgeCharge.text = session.Generator.ProductionRemaining.ToString("0") + "s";
            var selected = _host.SelectedTower;
            if (_contributionDamage != null && selected != null)
            {
                _contributionDamage.text = selected.LifetimeDamage.ToString("N0");
                _contributionKills.text = selected.LifetimeKills.ToString("N0");
                _contributionAssist.text = (selected.LifetimeSupportDamageEquivalent + selected.LifetimeExposeDamageEquivalent + selected.LifetimeArmorBreakDamageEquivalent).ToString("N0");
            }
        }

        private VisualElement Intel(string title, string subtitle)
        {
            var panel = Element(_root, "ui-surface", "intel");
            var head = Element(panel, "row");
            Text(head, title, "intel-title", "grow");
            Button(head, "×", () => _host.Select(0), "small");
            Text(panel, subtitle, "muted");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            panel.Add(scroll);
            return scroll;
        }

        private void TowerIntel(TowerInstance tower)
        {
            var session = _host.Session;
            var panel = Intel(tower.Definition.DisplayName, tower.IsApex ? "APEX" : TowerInfo.ProgressionLabel(tower));
            if (session.ProtocolsEnabled)
            {
                var controls = Element(panel, "protocol-controls");
                var fire = Button(controls, tower.Protocol.DisplayName.ToUpperInvariant(), () => _host.Command(GameCommandType.OverdriveTower, tower.Id), "violet", "grow");
                fire.tooltip = tower.Protocol.Summary;
                Enable(fire, () => !tower.IsOverdriven && !tower.IsSandboxDisabled &&
                    tower.ApexProtocolCooldownRemaining <= 0 && session.OverdriveCooldownRemaining <= 0);
                _enabledRules.Add(() =>
                {
                    float cooldown = Mathf.Max(tower.ApexProtocolCooldownRemaining, session.OverdriveCooldownRemaining);
                    fire.text = tower.IsOverdriven ? "ACTIVE" : cooldown > 0 ? "READY IN " + Mathf.CeilToInt(cooldown) + "s" : tower.Protocol.DisplayName.ToUpperInvariant();
                });
                if (!tower.IsApex)
                {
                    var automatic = Button(controls, "AUTO", () => _host.Command(GameCommandType.ToggleAutoProtocol, tower.Id), "small");
                    automatic.tooltip = "Automatically activate this tower's protocol when ready.";
                    Enable(automatic, () => !tower.IsSandboxDisabled);
                    _enabledRules.Add(() => automatic.EnableInClassList("selected", session.AutoOverdriveTowerId == tower.Id));
                }
            }
            _statsTitle = Text(panel, "CURRENT STATS", "section-title");
            _stats = Element(panel, "stats-grid");
            ShowStats(tower, null);
            Text(panel, "UPGRADES", "section-title");
            if (tower.RequiresDoctrine)
            {
                foreach (var choice in tower.Definition.Tier2Doctrines)
                {
                    var item = choice;
                    var preview = tower.Definition.Levels[1].WithDoctrine(item);
                    Upgrade(panel, tower, item.DisplayName, item.Summary, item.UpgradeCost, preview,
                        () => _host.Command(GameCommandType.ChooseDoctrine, tower.Id, item.Id));
                }
            }
            else if (tower.RequiresSpecialization)
            {
                foreach (var choice in tower.Definition.Specializations)
                {
                    var item = choice;
                    Upgrade(panel, tower, item.DisplayName, item.Summary, item.UpgradeCost, item.Level.WithDoctrine(tower.Doctrine),
                        () => _host.Command(GameCommandType.SpecializeTower, tower.Id, item.Id));
                }
            }
            else if (tower.CanUpgrade)
                Upgrade(panel, tower, "UPGRADE", "", tower.UpgradeCost, tower.Definition.Levels[tower.LevelIndex + 1].WithDoctrine(tower.Doctrine),
                    () => _host.Command(GameCommandType.UpgradeTower, tower.Id));
            else if (!tower.IsApex && tower.Definition.Apex != null)
            {
                var b = Upgrade(panel, tower, "APEX", "Unlocks before wave 21.", tower.ApexUpgradeCost, tower.ApexPreviewLevel,
                    () => _host.Command(GameCommandType.UpgradeTower, tower.Id));
                Enable(b, () => session.CanApexUpgrade(tower) && session.Economy.CanAfford(tower.ApexUpgradeCost));
            }
            else Text(panel, "Fully upgraded", "muted");
            if (!tower.IsSupport)
            {
                Text(panel, "TARGET", "section-title");
                var modes = session.AvailableTargetModes.Select(x => x.ToString()).ToList();
                var targets = new DropdownField(modes, Math.Max(0, modes.IndexOf(tower.TargetMode.ToString())));
                targets.RegisterValueChangedCallback(evt => _host.Command(GameCommandType.SetTargetMode, tower.Id, targetMode: (TargetMode)Enum.Parse(typeof(TargetMode), evt.newValue)));
                panel.Add(targets);
            }
            var details = new Foldout { text = "Contribution", value = false };
            panel.Add(details);
            _contributionDamage = Stat(details, "Damage", tower.LifetimeDamage.ToString("N0"));
            _contributionKills = Stat(details, "Kills", tower.LifetimeKills.ToString("N0"));
            _contributionAssist = Stat(details, "Assist", (tower.LifetimeSupportDamageEquivalent + tower.LifetimeExposeDamageEquivalent + tower.LifetimeArmorBreakDamageEquivalent).ToString("N0"));
            if (session.SellingEnabled) Button(panel, "SELL  +" + tower.SellValue, () => { _host.Command(GameCommandType.SellTower, tower.Id); _host.Select(0); }, "danger", "small");
        }

        private Button Upgrade(VisualElement parent, TowerInstance tower, string name, string summary, int cost, TowerLevelDefinition preview, Action action)
        {
            var button = Button(parent, name + "  ·  " + cost, action, "blue");
            button.tooltip = summary;
            button.RegisterCallback<PointerEnterEvent>(_ => ShowStats(tower, preview));
            button.RegisterCallback<PointerLeaveEvent>(_ => ShowStats(tower, null));
            button.RegisterCallback<FocusInEvent>(_ => ShowStats(tower, preview));
            button.RegisterCallback<FocusOutEvent>(_ => ShowStats(tower, null));
            Enable(button, () => _host.Session.Economy.CanAfford(cost));
            return button;
        }

        private void ShowStats(TowerInstance tower, TowerLevelDefinition preview)
        {
            if (_stats == null) return;
            _previewStatsLevel = preview;
            _statsTitle.text = preview == null ? "CURRENT STATS" : "UPGRADE PREVIEW";
            var s = _host.Session;
            var stats = TowerInfo.ComparisonStats(tower.Definition, tower.Level, preview, s.GetSupportBuff(tower),
                s.Map.GetPowerBuff(tower.Position), tower.IsOverdriven ? tower.Protocol : null,
                s.GetSignalDamageMultiplier(tower), s.GetSignalRateMultiplier(tower));
            string key = string.Join("|", stats.Select(stat => stat.Label + ":" + TowerInfo.ComparisonStatValueText(stat) + ":" + stat.Direction));
            if (key == _statsKey) return;
            _statsKey = key;
            _stats.Clear();
            foreach (var stat in stats)
            {
                var cell = Element(_stats, "stat-cell");
                Text(cell, stat.Label, "stat-cell-label");
                var label = Text(cell, TowerInfo.ComparisonStatValueText(stat), "stat-cell-value");
                if (stat.Direction != TowerStatDirection.Unchanged) label.AddToClassList(stat.Direction == TowerStatDirection.Increase ? "positive" : "negative");
            }
        }

        private void ForgeIntel()
        {
            var session = _host.Session;
            var forge = session.Generator;
            var panel = Intel("Charge Forge", "LEVEL " + (forge.LevelIndex + 1));
            Stat(panel, "Production", forge.Level.ProductionSeconds.ToString("0.#") + "s");
            Stat(panel, "Capacity", forge.Level.Capacity.ToString());
            Stat(panel, "Plate bonus", forge.Level.DefenseDamageBonus.ToString("P0"));
            _forgeCharge = Stat(panel, "Next charge", forge.ProductionRemaining.ToString("0") + "s");
            if (forge.CanUpgrade)
            {
                var b = Button(panel, "UPGRADE  ·  " + forge.UpgradeCost, () => _host.Command(GameCommandType.UpgradeGenerator), "blue");
                Enable(b, () => session.Economy.CanAfford(forge.UpgradeCost));
            }
            if (session.SellingEnabled) Button(panel, "SELL  +" + forge.SellValue, () => { _host.Command(GameCommandType.SellGenerator); _host.Select(0); }, "danger");
        }

        private void Sandbox()
        {
            var panel = Element(_root, "ui-surface", "intel");
            Text(panel, "Sandbox", "intel-title");
            var wave = new IntegerField("Wave") { value = 1 };
            panel.Add(wave);
            Button(panel, "PLAY WAVE", () => _host.Session.StartSandboxWave(Mathf.Clamp(wave.value, 1, 30)), "blue");
            var definitions = _host.Content.Enemies.Values.ToList();
            var enemies = new DropdownField(definitions.Select(x => x.DisplayName).ToList(), 0);
            panel.Add(enemies);
            Button(panel, "SPAWN 5", () => _host.Session.SpawnSandboxTargets(definitions[Math.Max(0, enemies.index)].Id, 5, 1, "Standard", false));
            Button(panel, "CLEAR TARGETS", () => _host.Session.ClearSandboxTargets(), "small");
            Button(panel, "RESET PROTOCOLS", () => _host.Session.ResetSandboxProtocol(), "small");
        }

        private void Pause()
        {
            var page = Panel("Paused", true);
            Button(page, "RESUME", () => Show(Page.Hud), "primary");
            Button(page, "RESTART", () => Confirm("Restart this run?", "Current progress will be lost.", () => _host.Restart()), "violet");
            Button(page, "SAVE", () => { _saveMode = true; OpenSecondary(Page.Saves); }, "blue");
            Button(page, "LOAD", () => { _saveMode = false; OpenSecondary(Page.Saves); }, "green");
            Button(page, "SETTINGS", () => OpenSecondary(Page.Settings));
            Button(page, "CONTROLS", () => OpenSecondary(Page.Controls));
            Button(page, "MAIN MENU", () => Confirm("Leave the run?", _host.Session.CanSaveCheckpoint ? "An autosave will preserve this checkpoint." : "Progress since the last checkpoint will be lost.", () => _host.LeaveRun()), "danger");
        }

        private void Saves()
        {
            var page = Panel(_saveMode ? "Save Game" : "Load Game");
            if (_saveMode && !_host.Session.CanSaveCheckpoint) Text(page, "Save between waves.", "body-copy");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("list"); page.Add(scroll);
            try
            {
                var slots = _host.Saves.GetSlots();
                foreach (var slot in slots)
                {
                    if (slot.Slot == 0 && !slot.IsOccupied) continue;
                    if (!_saveMode && !slot.IsOccupied) continue;
                    var item = slot;
                    var row = Element(scroll, "list-row", "row");
                    var text = Element(row, "grow");
                    Text(text, (item.Slot == 0 ? "AUTOSAVE" : "SAVE " + item.Slot) + (item.IsOccupied ? "  ·  " + MapName(item.MapId) : "  ·  Empty"));
                    if (item.IsOccupied) Text(text, item.Error != null ? "Unreadable save" : $"Wave {item.CurrentWave} · {item.SavedAtUtc.ToLocalTime():g}", "muted");
                    if (_saveMode && item.Slot != 0)
                    {
                        var b = Button(row, "SAVE", () => { _host.Save(item.Slot); Render(); }, "small", "blue");
                        b.SetEnabled(_host.Session.CanSaveCheckpoint);
                    }
                    else if (!_saveMode && item.IsOccupied)
                    {
                        var b = Button(row, "LOAD", () => { if (_host.Load(item.Slot)) Show(Page.Hud); }, "small", "blue");
                        b.SetEnabled(item.Error == null && item.MapId == "foundry_loop" && !item.IsCoOp);
                        if (item.MapId != "foundry_loop") b.tooltip = "This 3D scene supports Cinderworks.";
                    }
                    if (item.IsOccupied) Button(row, "DELETE", () => Confirm("Delete this save?", "This also removes its recovery copy.", () => { _host.Saves.Delete(item.Slot); Render(); }), "small", "danger");
                }
                if (!_saveMode && slots.All(x => !x.IsOccupied)) Text(scroll, "No saved runs.", "muted");
            }
            catch (Exception e) { Text(scroll, "Could not read saves: " + e.Message, "body-copy"); }
            Button(page, "BACK", Back);
        }

        private void History()
        {
            var page = Panel("Run History");
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("list"); page.Add(scroll);
            try
            {
                var entries = _host.History.GetEntries();
                foreach (var item in entries.OrderByDescending(x => x.CompletedAtUtc).Take(100))
                {
                    var entry = item;
                    var row = Element(scroll, "list-row", "row");
                    var info = Element(row, "grow");
                    Text(info, entry.MapName + "  ·  " + (entry.Victory ? "VICTORY" : "DEFEAT"));
                    Text(info, $"{entry.DifficultyName} · Wave {entry.CurrentWave} · {entry.CompletedAtUtc.ToLocalTime():g}", "muted");
                    Button(row, "DETAILS", () => HistoryDetails(entry), "small");
                }
                if (entries.Count == 0) Text(scroll, "Completed runs appear here.", "muted");
            }
            catch (Exception e) { Text(scroll, "Could not read history: " + e.Message, "body-copy"); }
            Button(page, "BACK", Back);
        }

        private void HistoryDetails(RunHistoryEntry entry)
        {
            _root.Clear(); _enabledRules.Clear();
            var page = Panel(entry.Victory ? "Victory" : "Defeat");
            Text(page, entry.MapName + " · " + entry.DifficultyName + " · " + entry.ChallengeName, "body-copy");
            Stat(page, "Wave", entry.CurrentWave.ToString());
            Stat(page, "Time", TimeSpan.FromSeconds(entry.DefenseSeconds).ToString(@"mm\:ss"));
            Stat(page, "Kills", entry.Kills.ToString("N0"));
            Stat(page, "Top tower", entry.TopTowerName);
            var detail = new Foldout { text = "Contribution", value = false }; page.Add(detail);
            foreach (var tower in entry.Towers.OrderByDescending(x => x.Damage).Take(10))
                Stat(detail, tower.DisplayName, tower.Damage.ToString("N0"));
            Button(page, "BACK", Render);
        }

        private void Settings()
        {
            var page = Panel("Settings");
            Setting(page, "Volume", Slider(0, 1, AudioListener.volume, value => { AudioListener.volume = value; PlayerPrefs.SetFloat("Volume", value); }));
            Setting(page, "Camera sensitivity", Slider(.3f, 2.5f, _camera.Sensitivity, value => { _camera.Sensitivity = value; PlayerPrefs.SetFloat("CameraSensitivity", value); }));
            var vsync = new Toggle { value = QualitySettings.vSyncCount > 0 };
            vsync.RegisterValueChangedCallback(e => { QualitySettings.vSyncCount = e.newValue ? 1 : 0; PlayerPrefs.SetInt("VSync", e.newValue ? 1 : 0); });
            Setting(page, "VSync", vsync);
            var fullscreen = new Toggle { value = Screen.fullScreen };
            fullscreen.RegisterValueChangedCallback(e => Screen.fullScreen = e.newValue);
            Setting(page, "Fullscreen", fullscreen);
            var automatic = new Toggle { value = _host.AutoWave };
            automatic.RegisterValueChangedCallback(e => { _host.AutoWave = e.newValue; PlayerPrefs.SetInt("AutoWave", e.newValue ? 1 : 0); });
            Setting(page, "Auto-start waves", automatic);
            Button(page, "BACK", () => { PlayerPrefs.Save(); Back(); });
        }

        private void Controls()
        {
            var page = Panel("Controls");
            Stat(page, "Build / select", "Left click");
            Stat(page, "Repeat placement", "Shift + click");
            Stat(page, "Cancel / pause", "Esc");
            Stat(page, "Next wave", "Space");
            Stat(page, "Orbit", "Right mouse drag");
            Stat(page, "Pan", "Middle mouse drag / WASD");
            Stat(page, "Zoom", "Mouse wheel");
            Stat(page, "Elevation", "Q / E");
            Stat(page, "Free flight", "F");
            Stat(page, "Move faster", "Shift");
            Stat(page, "Overview", "Home");
            Button(page, "BACK", Back);
        }

        private void Results()
        {
            var session = _host.Session;
            var page = Panel(session.IsVictory ? "Victory" : "Defeat", true);
            Text(page, session.Map.Definition.DisplayName + " · " + session.Difficulty.DisplayName, "body-copy");
            Stat(page, "Wave", session.CurrentWave.ToString());
            Stat(page, "Time", TimeSpan.FromSeconds(session.Statistics.SimulatedSeconds).ToString(@"mm\:ss"));
            Stat(page, "Kills", session.Statistics.Enemies.Sum(x => x.Kills).ToString("N0"));
            var leader = session.Statistics.TowerLeaders.FirstOrDefault();
            if (leader != null) Stat(page, "Top tower", leader.DisplayName);
            Element(page, "separator");
            if (session.IsVictory) Button(page, "CONTINUE ENDLESS", () => { if (_host.Command(GameCommandType.ContinueEndless)) Show(Page.Hud); }, "primary");
            Button(page, "RESTART", () => _host.Restart(), "violet");
            Button(page, "HISTORY", () => OpenSecondary(Page.History), "green");
            Button(page, "MAIN MENU", () => _host.LeaveRun());
        }

        private void Confirm(string title, string explanation, Action confirmed)
        {
            _root.Clear(); _enabledRules.Clear();
            _tooltip = null;
            var page = Panel(title, true);
            Text(page, explanation, "body-copy");
            Button(page, "CONFIRM", () => { confirmed(); if (_root.childCount == 0) Render(); }, "danger");
            Button(page, "CANCEL", Render);
        }

        private string MapName(string id) => _host.Content.Maps.TryGetValue(id, out var map) ? map.DisplayName : id;
        private static VisualElement Element(VisualElement parent, params string[] classes)
        {
            var item = new VisualElement(); foreach (var name in classes) item.AddToClassList(name); parent.Add(item); return item;
        }
        private static Label Text(VisualElement parent, string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var name in classes) label.AddToClassList(name);
            if (classes.Contains("page-title") || classes.Contains("brand"))
            {
                var font = Resources.Load<Font>("Fonts/Oxanium-SemiBold");
                if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font);
            }
            parent.Add(label); return label;
        }
        private static Button Button(VisualElement parent, string text, Action action, params string[] classes)
        {
            var button = new Button(action) { text = text }; foreach (var name in classes) button.AddToClassList(name); parent.Add(button); return button;
        }
        private void Enable(Button button, Func<bool> enabled)
        {
            Action refresh = () => button.SetEnabled(enabled()); _enabledRules.Add(refresh); refresh();
        }
        private static Label Counter(VisualElement parent, string name)
        {
            var box = Element(parent, "counter"); Text(box, name, "counter-label"); return Text(box, "", "counter-value");
        }
        private static Label Stat(VisualElement parent, string name, string value)
        {
            var row = Element(parent, "stat-row"); Text(row, name, "stat-name"); return Text(row, value, "stat-value");
        }
        private static void Setting(VisualElement parent, string name, VisualElement control)
        {
            var row = Element(parent, "setting-row"); Text(row, name); row.Add(control);
        }
        private static Slider Slider(float minimum, float maximum, float value, Action<float> changed)
        {
            var slider = new Slider(minimum, maximum) { value = value }; slider.RegisterValueChangedCallback(e => changed(e.newValue)); return slider;
        }
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator CaptureReview(string[] args)
        {
            int index = Array.IndexOf(args, "--capture-directory");
            string directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "InterfaceReview");
            Directory.CreateDirectory(directory);
            var capture = ReviewCapture.Initialize(_camera.GetComponent<Camera>(), this);
            var captured = new List<string>();
            yield return new WaitForSecondsRealtime(2);
            yield return CapturePage(Page.Home, "00-home");
            yield return CapturePage(Page.Setup, "01-setup");
            _host.NewRun("easy", "standard");
            yield return CapturePage(Page.Hud, "02-hud");
            yield return CapturePage(Page.Pause, "03-pause");
            _returnPage = Page.Pause;
            yield return CapturePage(Page.Settings, "04-settings");
            yield return CapturePage(Page.Controls, "05-controls");
            _host.Save(1, false);
            _saveMode = true;
            yield return CapturePage(Page.Saves, "06-save");
            _saveMode = false;
            yield return CapturePage(Page.Saves, "07-load");
            yield return CapturePage(Page.History, "08-history");
            yield return CapturePage(Page.Results, "09-results-layout");
            _host.NewRun("easy", _host.Content.Challenges.Values.First(x => x.IsSandbox).Id);
            bool placed = false;
            for (int y = 120; y < 600 && !placed; y += 40)
            for (int x = 100; x < 1100 && !placed; x += 40)
            {
                var point = new MaximalBastion.Gameplay.Vector2(x, y);
                if (_host.Session.ValidatePlacement("prism_beam", point) != PlacementFailure.None) continue;
                placed = _host.Command(GameCommandType.PlaceTower, choice: "prism_beam", position: point);
            }
            if (placed)
            {
                var tower = _host.Session.Towers.Last();
                _host.Select(tower.Id);
                yield return CapturePage(Page.Hud, "10-tower-intel");
                var doctrine = tower.Definition.Tier2Doctrines.FirstOrDefault();
                if (doctrine != null) _host.Command(GameCommandType.ChooseDoctrine, tower.Id, doctrine.Id);
                yield return CapturePage(Page.Hud, "11-specialization");
                var specialization = tower.Definition.Specializations.FirstOrDefault();
                if (specialization != null) _host.Command(GameCommandType.SpecializeTower, tower.Id, specialization.Id);
                yield return CapturePage(Page.Hud, "12-apex");
            }
            _host.Select(0);
            yield return CapturePage(Page.Hud, "13-sandbox");
            if (placed)
            {
                var tower = _host.Session.Towers.Last();
                _host.Select(tower.Id);
                Show(Page.Hud);
                ShowStats(tower, tower.ApexPreviewLevel);
                yield return CaptureCurrent("14-upgrade-preview");
            }
            Confirm("Restart this run?", "Current progress will be lost.", () => _host.Restart());
            yield return CaptureCurrent("15-confirmation");
            bool imagesVerified = captured.Count == 16 && captured.All(p => File.Exists(p) && new FileInfo(p).Length > 1024);
            File.WriteAllText(Path.Combine(directory, "interface-review.txt"), "Verified images: " + captured.Count + "/16\nTower placed: " + placed + "\nPassed: " + (imagesVerified && placed));
            Application.Quit(imagesVerified && placed ? 0 : 1);

            IEnumerator CapturePage(Page page, string name)
            {
                Show(page);
                yield return CaptureCurrent(name);
            }

            IEnumerator CaptureCurrent(string name)
            {
                yield return new WaitForSecondsRealtime(.7f);
                yield return new WaitForEndOfFrame();
                string path = Path.Combine(directory, name + ".png");
                capture.Capture(path);
                captured.Add(path);
                yield return null;
            }
        }
        private void OnDestroy()
        {
            if (_host != null) { _host.Changed -= MarkDirty; _host.SessionChanged -= SessionChanged; }
            if (_panel != null) Destroy(_panel);
        }
    }
}
