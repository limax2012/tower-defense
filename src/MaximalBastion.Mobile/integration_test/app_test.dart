import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:maximal_bastion_mobile/engine/engine_bridge.dart';
import 'package:maximal_bastion_mobile/ui/bastion_app.dart';

Future<void> until(
  WidgetTester tester,
  bool Function() condition, {
  String reason = 'engine response',
}) async {
  final deadline = DateTime.now().add(const Duration(seconds: 120));
  while (!condition() && DateTime.now().isBefore(deadline)) {
    await tester.pump(const Duration(milliseconds: 250));
  }
  expect(condition(), isTrue, reason: reason);
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  testWidgets('Offline engine, touch placement, combat and checkpoint reload', (
    tester,
  ) async {
    final bridge = EngineBridge();
    await tester.pumpWidget(BastionApp(bridge: bridge));
    await until(
      tester,
      () => bridge.ready || bridge.fatal,
      reason: 'bundled engine startup',
    );
    expect(bridge.fatal, isFalse, reason: bridge.error);
    expect(objects(bridge.catalog['towers']).length, 10);
    expect(objects(bridge.catalog['maps']).length, 4);
    await tester.tap(find.text('New defense'));
    await tester.pump();
    await tester.ensureVisible(find.text('Deploy defense'));
    await tester.tap(find.text('Deploy defense'));
    await until(
      tester,
      () => bridge.screen == 'playing',
      reason: bridge.error ?? 'new defense',
    );
    final credits = number(bridge.state['credits']);
    final needle = objects(bridge.catalog['towers'])
        .firstWhere((tower) => tower['id'] == 'needle_turret');
    await tester.tap(
      find.byWidgetPredicate(
        (widget) =>
            widget is Tooltip && widget.message!.startsWith('Needle Turret ·'),
      ),
    );
    await until(tester, () => bridge.placement['active'] == true);
    final field = tester.getRect(
      find.byKey(const ValueKey('battlefield-input')),
    );
    final position =
        field.topLeft +
        Offset(field.width * 240 / 960, field.height * 175 / 720);
    await tester.tapAt(position);
    await until(tester, () => bridge.placement['hasPlacementPreview'] == true);
    expect(bridge.placement['failure'], 'None');
    await tester.ensureVisible(find.text('Place here'));
    await tester.tap(find.text('Place here'));
    await until(tester, () => number(bridge.state['credits']) < credits);
    expect(
      number(bridge.state['credits']),
      credits - number(needle['purchaseCost']),
    );
    await tester.tapAt(position);
    await until(tester, () => bridge.selected.isNotEmpty);
    expect(bridge.selected['definitionId'], 'needle_turret');
    expect(objects(bridge.selected['upgrades']).length, 2);
    final slot = await tester.runAsync(
      () => bridge.request('save', {'slot': -1}),
    );
    await tester.runAsync(() => bridge.request('cancel'));
    await tester.runAsync(
      () => bridge.request('command', {'type': 'StartWave'}),
    );
    await until(tester, () => number(bridge.state['currentWave']) == 1);
    await tester.pump(const Duration(seconds: 4));
    expect(bridge.error, isNull);
    await tester.runAsync(() => bridge.transport!.suspend(true));
    await until(tester, () => bridge.state['suspended'] == true);
    expect(bridge.state['paused'], isTrue);
    await tester.runAsync(() => bridge.transport!.suspend(false));
    bridge.ready = false;
    await tester.runAsync(() => bridge.transport!.reload());
    await until(
      tester,
      () => bridge.ready || bridge.fatal,
      reason: 'engine restart',
    );
    expect(bridge.fatal, isFalse, reason: bridge.error);
    await tester.runAsync(() => bridge.request('load', {'slot': slot}));
    await until(tester, () => bridge.screen == 'playing');
    expect(bridge.state['paused'], isTrue);
    expect(
      number(bridge.state['credits']),
      credits - number(needle['purchaseCost']),
    );
    expect(number(bridge.state['currentWave']), 0);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    bridge.dispose();
  });
}
