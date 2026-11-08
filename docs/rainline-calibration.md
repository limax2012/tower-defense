# Rainline Heights / Bastion / Signal Gauntlet

The history-guided planner clears Rainline Heights (`crosswind_basin`) from the ordinary empty starting board, then carries the same 35 towers through the first generated Endless wave. The campaign ends with 2,173 kills, zero leaks, one life and 248 credits. The scenario uses seed 1337, oracle information, expert execution and authoritative 60 Hz stepping. Mid-wave selling is prohibited. Both verified traces contain zero sales of any kind.

## Reproduce

Use the isolated build instructions in [the solver guide](strategy-solver.md#build-and-run) to set `$dotnet` and `$planner`.

```powershell
$out = '.build\planner\rainline-check'
& $dotnet $planner --planner-layout-campaign --plan tests\MaximalBastion.Tests\Fixtures\rainline-bastion-signal-plan.json --no-combat-sales --simulations 32 --seconds 60 --beam 1 --output "$out\campaign"
& $dotnet $planner --planner-endless-continuation --checkpoint "$out\campaign\wave-30.json" --plan tests\MaximalBastion.Tests\Fixtures\rainline-bastion-signal-endless31.json --no-combat-sales --simulations 1 --seconds 60 --output "$out\endless31"
```

The fixtures contain geometry, upgrade goals and decision policies. They require no player save or history file. Campaign verification replays every accepted command from normal starting resources. The Endless continuation preserves that prefix and uses the real `ContinueEndless` command before starting wave 31.

## Layout prior and allocation

The August 27, 2026 Bastion / Signal Gauntlet victory in local history supplies 50 tower goals and an automatic Beacon preference. The importer orders surviving historical tower IDs and retains their positions and upgrade branches. That is a proposed investment order. History does not record the purchase times needed to reconstruct the campaign.

The fresh solver can afford the first 35 goals before wave 30. These towers have a total investment of 22,325 credits. The verified board has no Apex upgrades and no Forge. The remaining goals stay in the fixture as part of the complete layout prior, but the final-wave policy disables construction and upgrades. Historical lives, credits and income are not imported.

Waves 1–20 use First targeting with decisions every 60 ticks. Waves 21–29 use Split targeting every 15 ticks and keep Frost on Fastest. Both ranges spend on the ordered layout and buy no Plates.

The selected final-wave policy holds the board, switches to First with Frost on Fastest, and allows tactical Plates, with decisions every 30 ticks. It clears all 136 enemies, buying seven Plates for 735 credits. The campaign fixture reproduces this result in 30 wave evaluations. One measured reproduction took 5.20 seconds, including independent replay verification.

## Standing-board Endless control

Keeping the campaign's existing orders and taking no further player actions after `ContinueEndless` and `StartWave` kills 131 of 137 enemies before defeat.

The selected Support policy keeps Frost on Fastest and makes decisions every 15 ticks. It clears all 137 enemies in 5,167 ticks without any purchase, sale, upgrade or manual protocol activation. Its 46 accepted targeting changes leave all 35 tower IDs, positions, branches, levels and invested credits intact. The result ends with one life and 968 credits, for 2,310 kills across the campaign and wave 31. A separate one-policy run with paid Plates disabled reproduced the result in 4.44 seconds, including prefix verification.

The campaign proof checks 634 accepted actions over 89,098 ticks. Its final state signature is `795F37625AB994DF3576EFECF0EE5E8CD257D1CA71099DDFDE7E8F1DEFF6962F`. The extended proof checks 682 actions over 94,265 ticks and ends at `348C0C631D7EA6FEC4078CCA3343FDEABBF05F30672910F1B9238B03B5AA1C97`. Wave 31 is a verified finite Endless horizon. It does not establish survival through later generated waves.
