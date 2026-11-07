import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:maximal_bastion_mobile/engine/engine_bridge.dart';
import 'package:maximal_bastion_mobile/ui/bastion_app.dart';

void main() {
  for (final size in [const Size(320, 720), const Size(844, 390)]) {
    testWidgets('Wave breach costs remain readable at $size with large text', (
      tester,
    ) async {
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = size;
      tester.platformDispatcher.textScaleFactorTestValue = 1.5;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
      final bridge = EngineBridge()
        ..ready = true
        ..catalog = {
          'towers': [],
          'enemies': [
            {'id': 'crawler', 'displayName': 'Crawler'},
            {'id': 'aegis', 'displayName': 'Aegis'},
            {'id': 'regenerator', 'displayName': 'Regenerator'},
          ],
        }
        ..state = {
          'screen': 'playing',
          'runId': 'breach-preview',
          'mutable': true,
          'paused': true,
          'currentWave': 0,
          'totalWaves': 30,
          'lives': 1,
          'credits': 400,
          'speed': 1,
          'canStartWave': true,
          'availableTowers': [],
          'tactical': {},
          'wave': {
            'intel': {'wave': 1, 'archetype': 'Mixed ranks', 'threats': []},
            'groups': [
              {
                'enemyId': 'crawler',
                'rank': 'Standard',
                'count': 3,
                'spawnInterval': 1,
                'breach': {'livesLost': 1, 'summary': 'BREACH: LOSE 1 LIFE'},
              },
              {
                'enemyId': 'aegis',
                'rank': 'Elite',
                'count': 2,
                'spawnInterval': 1,
                'breach': {'livesLost': 4, 'summary': 'BREACH: LOSE 4 LIVES'},
              },
              {
                'enemyId': 'regenerator',
                'rank': 'Boss',
                'count': 1,
                'spawnInterval': 1,
                'breach': {'livesLost': 10, 'summary': 'BREACH: LOSE 10 LIVES'},
              },
            ],
          },
        };
      await tester.pumpWidget(
        BastionApp(bridge: bridge, surface: const SizedBox()),
      );
      await tester.tap(find.text('Wave 0/30'));
      await tester.pumpAndSettle();
      for (final label in [
        'BREACH: LOSE 1 LIFE',
        'BREACH: LOSE 4 LIVES',
        'BREACH: LOSE 10 LIVES',
      ]) {
        expect(find.text(label), findsOneWidget);
        await tester.ensureVisible(find.text(label));
        await tester.pump();
        expect(tester.takeException(), isNull);
      }
      await tester.pumpWidget(const SizedBox());
      bridge.dispose();
    });
  }
}
