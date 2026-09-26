import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';

typedef JsonMap = Map<String, dynamic>;
JsonMap object(dynamic value) =>
    value is Map ? Map<String, dynamic>.from(value) : {};
List<JsonMap> objects(dynamic value) =>
    value is List ? value.map(object).toList() : [];
double number(dynamic value, [double fallback = 0]) =>
    value is num ? value.toDouble() : fallback;

abstract interface class EngineTransport {
  Future<void> send(String request);
  Future<void> suspend(bool suspended);
  Future<void> reload();
}

/// Presentation state only. The bundled C# engine owns prices, rules and stat previews.
class EngineBridge extends ChangeNotifier {
  static const protocolVersion = 1;
  EngineTransport? transport;
  JsonMap catalog = {};
  JsonMap state = {};
  String? error;
  bool fatal = false;
  bool ready = false;
  bool _disposed = false;
  int _nextId = 1;
  Future<void> _outbound = Future.value();
  final Map<int, Completer<dynamic>> _pending = {};
  final Map<int, Timer> _timeouts = {};
  String get screen => state['screen'] as String? ?? 'menu';
  String get runId => state['runId'] as String? ?? '';
  bool get canMutate => state['mutable'] == true;
  JsonMap get selected => object(state['selected']);
  JsonMap get placement => object(state['placement']);
  JsonMap get tactical => object(state['tactical']);
  JsonMap get settings => object(state['settings'] ?? catalog['settings']);

  void receive(String encoded) {
    if (_disposed) return;
    try {
      final message = object(jsonDecode(encoded));
      switch (message['type']) {
        case 'ready':
          if (message['protocol'] != protocolVersion) {
            fail(
              'This interface and its bundled engine need to be built together.',
              fatal: true,
            );
            return;
          }
          unawaited(_loadCatalog());
        case 'state':
          if (message['protocol'] != protocolVersion) return;
          state = object(message['state']);
          notifyListeners();
        case 'reply':
          final id = message['id'];
          final pending = _pending.remove(id);
          _timeouts.remove(id)?.cancel();
          if (message['state'] is Map) state = object(message['state']);
          if (message['ok'] == true) {
            pending?.complete(message['result']);
          } else {
            error =
                message['error']?.toString() ?? 'The game rejected the action.';
            pending?.completeError(StateError(error!));
          }
          notifyListeners();
        case 'fatal':
          fail(
            message['error']?.toString() ?? 'The game engine stopped.',
            fatal: true,
          );
      }
    } catch (exception) {
      fail('The game returned an unreadable response: $exception', fatal: true);
    }
  }

  Future<void> _loadCatalog() async {
    try {
      catalog = object(await request('catalog'));
      ready = true;
      fatal = false;
      error = null;
      if (!_disposed) notifyListeners();
    } catch (exception) {
      fail('Game data could not be loaded: $exception', fatal: true);
    }
  }

  Future<dynamic> request(String action, [JsonMap? data]) {
    final target = transport;
    if (target == null) {
      return Future.error(StateError('The engine is still starting.'));
    }
    if (_pending.length >= 64) {
      return Future.error(StateError('The engine is busy.'));
    }
    final id = _nextId++;
    final completer = Completer<dynamic>();
    _pending[id] = completer;
    _timeouts[id] = Timer(const Duration(seconds: 15), () {
      _timeouts.remove(id);
      _pending
          .remove(id)
          ?.completeError(TimeoutException('The engine did not respond.'));
    });
    final encoded = jsonEncode({
      'protocol': protocolVersion,
      'id': id,
      'runId': runId,
      'action': action,
      'data': ?data,
    });
    _outbound = _outbound.then((_) => target.send(encoded)).catchError((
      Object exception,
    ) {
      _timeouts.remove(id)?.cancel();
      _pending.remove(id)?.completeError(exception);
    });
    return completer.future;
  }

  void issue(String action, [JsonMap? data]) {
    unawaited(
      request(action, data).catchError((Object exception) {
        fail(exception.toString());
        return null;
      }),
    );
  }

  void command(String type, [JsonMap data = const {}]) =>
      issue('command', {'type': type, ...data});
  void fail(String message, {bool fatal = false}) {
    if (_disposed) return;
    error = message;
    this.fatal = fatal;
    notifyListeners();
  }

  void clearError() {
    error = null;
    notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    for (final timer in _timeouts.values) {
      timer.cancel();
    }
    for (final pending in _pending.values) {
      pending.completeError(StateError('The game surface was closed.'));
    }
    _pending.clear();
    super.dispose();
  }
}
