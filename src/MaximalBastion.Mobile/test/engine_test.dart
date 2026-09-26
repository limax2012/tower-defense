import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:maximal_bastion_mobile/engine/engine_bridge.dart';
import 'package:maximal_bastion_mobile/engine/runtime_server.dart';

class RecordingTransport implements EngineTransport {
  final sent = <JsonMap>[];
  final gate = Completer<void>();
  @override
  Future<void> send(String request) async {
    sent.add(object(jsonDecode(request)));
    if (sent.length == 1) await gate.future;
  }

  @override
  Future<void> reload() async {}
  @override
  Future<void> suspend(bool suspended) async {}
}

void main() {
  test('Commands are ordered, correlated, and carry the active run', () async {
    final transport = RecordingTransport();
    final bridge = EngineBridge()
      ..transport = transport
      ..state = {'runId': 'defense'};
    final first = bridge.request('beginPlacement', {'towerId': 'needle'});
    final second = bridge.request('pointer', {'x': 240, 'y': 175});
    await Future<void>.delayed(Duration.zero);
    expect(transport.sent.length, 1);
    transport.gate.complete();
    await Future<void>.delayed(Duration.zero);
    expect(transport.sent.map((item) => item['action']), [
      'beginPlacement',
      'pointer',
    ]);
    expect(
      transport.sent.every(
        (item) => item['protocol'] == 1 && item['runId'] == 'defense',
      ),
      isTrue,
    );
    for (final request in transport.sent) {
      bridge.receive(
        jsonEncode({
          'type': 'reply',
          'protocol': 1,
          'id': request['id'],
          'ok': true,
          'result': request['action'],
          'state': {'runId': 'defense'},
        }),
      );
    }
    expect(await first, 'beginPlacement');
    expect(await second, 'pointer');
    bridge.receive('{"type":"ready","protocol":99}');
    expect(bridge.fatal, isTrue);
    bridge.dispose();
  });

  test(
    'Native persistence survives restart, replacement and deletion',
    () async {
      final directory = await Directory.systemTemp.createTemp('bastion-store-');
      addTearDown(() => directory.delete(recursive: true));
      final store = EngineFileStore(directory);
      await store.load();
      final first = base64Encode(utf8.encode('checkpoint one'));
      final second = base64Encode(utf8.encode('checkpoint two'));
      final writes = [
        store.update('Solo/slot1.json', first),
        store.update('Solo/slot1.json', second),
      ];
      await Future.wait(writes);
      final reloaded = EngineFileStore(directory);
      await reloaded.load();
      expect(reloaded.files['Solo/slot1.json'], second);
      await reloaded.update('Solo/slot1.json', null);
      expect(await File('${directory.path}/Solo/slot1.json').exists(), isFalse);
      await expectLater(
        store.update('../escape', first),
        throwsFormatException,
      );
      await expectLater(
        store.update('slot.json', 'not base64!'),
        throwsFormatException,
      );
    },
  );

  test(
    'A failed native write can be retried without losing the checkpoint',
    () async {
      final directory = await Directory.systemTemp.createTemp('bastion-store-');
      addTearDown(() => directory.delete(recursive: true));
      final store = EngineFileStore(directory);
      await store.load();
      final blocker = File('${directory.path}/Solo');
      await blocker.writeAsString('blocks a directory');
      final data = base64Encode(utf8.encode('checkpoint'));
      await expectLater(
        store.update('Solo/slot1.json', data),
        throwsA(isA<FileSystemException>()),
      );
      expect(store.files.containsKey('Solo/slot1.json'), isFalse);
      await blocker.delete();
      await store.update('Solo/slot1.json', data);
      expect(
        await File('${directory.path}/Solo/slot1.json').readAsString(),
        'checkpoint',
      );
    },
  );
}
