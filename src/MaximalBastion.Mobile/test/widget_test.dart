import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:maximal_bastion_mobile/engine/engine_bridge.dart';
import 'package:maximal_bastion_mobile/ui/bastion_app.dart';

EngineBridge playing() => EngineBridge()
  ..ready = true
  ..catalog = {
    'towers': List.generate(
      10,
      (i) => {
        'id': 'tower$i',
        'displayName': 'Tower $i',
        'role': 'Area control',
        'purchaseCost': 100,
        'visual': {'shape': 'triangle', 'primary': '#2192aa'},
      },
    ),
  }
  ..state = {
    'screen': 'playing',
    'runId': 'test',
    'mutable': true,
    'currentWave': 12,
    'totalWaves': 30,
    'lives': 12,
    'credits': 12345,
    'speed': 1,
    'canStartWave': true,
    'waveButtonLabel': 'START WAVE 13',
    'availableTowers': List.generate(10, (i) => 'tower$i'),
    'tactical': {
      'tacticalSystemsEnabled': true,
      'pulsePlatesEnabled': true,
      'emergencyInventory': 3,
    },
  };

void main() {
  for (final size in [
    const Size(320, 720),
    const Size(390, 844),
    const Size(844, 390),
  ]) {
    for (final scale in [1.0, 1.5]) {
      testWidgets('Combat and selection fit $size at text scale $scale', (
        tester,
      ) async {
        tester.view.devicePixelRatio = 1;
        tester.view.physicalSize = size;
        tester.platformDispatcher.textScaleFactorTestValue = scale;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        final bridge = playing();
        await tester.pumpWidget(
          BastionApp(
            bridge: bridge,
            surface: const ColoredBox(color: navy),
          ),
        );
        expect(tester.takeException(), isNull);
        expect(find.byTooltip('Fit whole map'), findsOneWidget);
        bridge.state['isSandbox'] = true;
        bridge.state['tactical'] = {
          'tacticalSystemsEnabled': false,
          'pulsePlatesEnabled': true,
          'emergencyInventory': 0,
        };
        bridge.receive(
          jsonEncode({'type': 'state', 'protocol': 1, 'state': bridge.state}),
        );
        await tester.pump();
        expect(find.text('◎ ∞'), findsOneWidget);
        expect(find.text('Forge'), findsNothing);
        expect(tester.takeException(), isNull);
        bridge.state['placement'] = {
          'active': true,
          'kind': 'Tower',
          'towerId': 'tower0',
          'failure': 'None',
          'hasPlacementPreview': true,
          'x': 240,
          'y': 175,
        };
        bridge.receive(
          jsonEncode({'type': 'state', 'protocol': 1, 'state': bridge.state}),
        );
        await tester.pump();
        expect(tester.takeException(), isNull);
        expect(find.text('Tower 0'), findsOneWidget);
        await tester.pumpWidget(const SizedBox());
        bridge.dispose();
      });
    }
  }
  testWidgets('Menus remain scrollable with large text on a small phone', (
    tester,
  ) async {
    tester.view.devicePixelRatio = 1;
    tester.view.physicalSize = const Size(320, 568);
    tester.platformDispatcher.textScaleFactorTestValue = 1.5;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    final bridge = playing()..state = {'screen': 'menu'};
    await tester.pumpWidget(
      BastionApp(bridge: bridge, surface: const SizedBox()),
    );
    expect(tester.takeException(), isNull);
    await tester.ensureVisible(find.text('Settings'));
    await tester.tap(find.text('Settings'));
    await tester.pumpAndSettle();
    expect(find.text('Music'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    bridge.dispose();
  });
}
