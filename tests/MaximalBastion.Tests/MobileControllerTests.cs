using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Mobile;
using MaximalBastion.Multiplayer;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class MobileControllerTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var directory = Path.Combine(Path.GetTempPath(), "MaximalBastion.MobileTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var mobile = new MobileSessionController(content, directory);
            long id = 0;
            var catalog = Send("catalog").GetProperty("result");
            Assert(catalog.GetProperty("towers").GetArrayLength() == content.Towers.Count, "The roster is read from shared content.");
            Assert(catalog.GetProperty("maps").GetArrayLength() == content.Maps.Count, "Every authored map is exposed.");
            Send("newRun", new { mapId = "foundry_loop", difficultyId = "normal", challengeId = "standard" });
            var baseline = new GameSession(content, "foundry_loop", "normal", "standard");
            Apply(new GameCommand { Type = GameCommandType.PlaceTower, PlayerId = 1,
                TowerDefinitionId = "needle_turret", X = 240, Y = 175 });
            var beforeSelection = Checksum();
            Send("pointer", new { x = 240, y = 175, commit = true, radius = 40 });
            Assert(Checksum() == beforeSelection, "Touch selection must not change simulation state.");
            var selected = State().GetProperty("selected");
            Assert(selected.GetProperty("upgrades").GetArrayLength() == 2, "Both doctrines are available before purchase.");
            var preview = selected.GetProperty("upgrades")[0].GetProperty("stats");
            var damage = preview.EnumerateArray().First(stat => stat.GetProperty("label").GetString() == "DAMAGE");
            Assert(damage.GetProperty("previousValue").GetString() == "8", "Preview uses the current shared stats.");

            Apply(new GameCommand { Type = GameCommandType.ChooseDoctrine, PlayerId = 1, EntityId = 1, DoctrineId = "needle_cycler" });
            Apply(new GameCommand { Type = GameCommandType.SetTargetMode, PlayerId = 1, EntityId = 1, TargetMode = TargetMode.Strongest });
            Apply(new GameCommand { Type = GameCommandType.ToggleAutoProtocol, PlayerId = 1, EntityId = 1 });
            var beforePreview = Checksum();
            Send("beginPlacement", new { kind = "tower", towerId = "frost_spire" });
            Send("pointer", new { x = 500, y = 200 });
            Assert(Checksum() == beforePreview, "Uncommitted placement is presentation only.");
            Send("pointer", new { x = 500, y = 200, commit = true });
            Assert(baseline.TryPlaceTower("frost_spire", new Vector2(500, 200)), "Baseline placement succeeds.");
            Assert(Checksum() == SessionChecksum.Compute(baseline, 0), "Touch placement follows the same C# rules.");

            var replayId = ++id;
            var request = Encode(replayId, "command", new { type = "StartWave" });
            var first = mobile.HandleRequest(request);
            Assert(JsonDocument.Parse(first).RootElement.GetProperty("ok").GetBoolean(), "Wave starts through the bridge.");
            Assert(mobile.HandleRequest(request) == first, "Duplicate requests return their original receipt.");
            Assert(baseline.StartNextWave(), "Baseline wave starts.");
            for (var tick = 0; tick < 600; tick++)
            {
                mobile.Update(1f / 60);
                baseline.Update(1f / 60);
            }
            Assert(Checksum() == SessionChecksum.Compute(baseline, 0), "Mobile and direct simulation produce identical combat checksums.");
            Apply(new GameCommand { Type = GameCommandType.SetSpeed, PlayerId = 1, Speed = 2 });
            for (var tick = 0; tick < 120; tick++) { mobile.Update(1f / 60); baseline.Update(1f / 60); }
            Assert(Checksum() == SessionChecksum.Compute(baseline, 0), "Speed changes remain shared gameplay.");

            Send("pause", new { paused = true });
            var paused = Checksum();
            var rejected = Send("command", new { type = "SellTower", entityId = 1 }, accepted: false);
            Assert(!rejected.GetProperty("ok").GetBoolean() && Checksum() == paused, "Paused UI cannot mutate gameplay.");
            mobile.SetSuspended(true);
            mobile.Update(100);
            Assert(Checksum() == paused, "Device suspension freezes simulation without catch-up.");
            mobile.SetSuspended(false);
            Assert(mobile.Paused, "A device interruption requires an explicit resume.");
            Assert(!JsonDocument.Parse(mobile.HandleRequest("[]")).RootElement.GetProperty("ok").GetBoolean(), "Malformed envelopes are rejected.");
            var stale = JsonSerializer.Serialize(new { protocol = 1, id = ++id, action = "command", runId = "old-run", data = new { type = "StartWave" } });
            Assert(!JsonDocument.Parse(mobile.HandleRequest(stale)).RootElement.GetProperty("ok").GetBoolean(), "Commands from a previous run are rejected.");

            Send("newRun", new { mapId = "foundry_loop", difficultyId = "normal", challengeId = "standard" });
            Send("save", new { slot = 1 });
            Send("duplicateSave", new { slot = 1 });
            Send("load", new { slot = 1 });
            Assert(mobile.Session!.CurrentWave == 0 && mobile.Paused, "Shared checkpoints restore into a paused mobile run.");
            Send("deleteSave", new { slot = 2 });
            Send("career");

            Send("newRun", new { mapId = "foundry_loop", difficultyId = "normal", challengeId = "standard" });
            mobile.Update(1f / 60);
            Send("command", new GameCommand { Type = GameCommandType.PlaceTower,
                TowerDefinitionId = "needle_turret", X = 240, Y = 175 });
            mobile.SetSuspended(true);
            mobile.SetSuspended(false);
            Send("load", new { slot = 0 });
            Assert(mobile.Session!.Towers.Count == 1, "Backgrounding refreshes the current between-wave checkpoint.");

            Send("newRun", new { mapId = "foundry_loop", difficultyId = "normal", challengeId = "sandbox_lab" });
            var sandboxTactical = State().GetProperty("tactical");
            Assert(sandboxTactical.GetProperty("pulsePlatesEnabled").GetBoolean() &&
                   !sandboxTactical.GetProperty("tacticalSystemsEnabled").GetBoolean(),
                "Sandbox exposes touch placement for Pulse Plates independently of the Forge.");
            Send("beginPlacement", new { kind = "plate" });
            Send("pointer", new { x = 68, y = 104, commit = true });
            Assert(mobile.Session!.EmergencyDefenses.Count == 1 && mobile.Session.EmergencyInventory == 0 &&
                   mobile.Session.EmergencyDirectPurchasesThisWave == 0,
                "Sandbox touch placement deploys a free Plate without campaign inventory.");

            string Checksum() => SessionChecksum.Compute(mobile.Session!, 0);
            JsonElement State() => JsonDocument.Parse(mobile.GetStateJson()).RootElement.GetProperty("state");
            string Encode(long requestId, string action, object? data) => JsonSerializer.Serialize(new
                { protocol = 1, id = requestId, action, runId = mobile.Session?.RunId ?? "", data });
            JsonElement Send(string action, object? data = null, bool accepted = true)
            {
                var response = JsonDocument.Parse(mobile.HandleRequest(Encode(++id, action, data))).RootElement;
                Assert(response.GetProperty("ok").GetBoolean() == accepted, $"Unexpected bridge response: {response}");
                return response;
            }
            void Apply(GameCommand command)
            {
                Send("command", command);
                Assert(GameCommandProcessor.Apply(baseline, command).Accepted, "Baseline command succeeds.");
                Assert(Checksum() == SessionChecksum.Compute(baseline, 0), "Command uses shared gameplay state.");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
