import 'dart:js_interop';

import 'package:flutter/material.dart';
import 'package:web/web.dart' as web;

import 'engine_bridge.dart';

class GameSurface extends StatefulWidget {
  const GameSurface({super.key, required this.bridge});
  final EngineBridge bridge;
  @override
  State<GameSurface> createState() => _GameSurfaceState();
}

class _GameSurfaceState extends State<GameSurface> implements EngineTransport {
  web.HTMLIFrameElement? _frame;
  late final JSFunction _listener;
  @override
  void initState() {
    super.initState();
    _listener = ((web.MessageEvent event) {
      if (event.origin != web.window.location.origin ||
          event.source != _frame?.contentWindow) {
        return;
      }
      final data = object(event.data.dartify());
      if (data['source'] == 'maximal-bastion' && data['data'] is String) {
        widget.bridge.receive(data['data'] as String);
      }
    }).toJS;
    web.window.addEventListener('message', _listener);
    widget.bridge.transport = this;
  }

  void _post(JsonMap data) => _frame?.contentWindow?.postMessage(
    {'source': 'maximal-bastion-flutter', ...data}.jsify(),
    web.window.location.origin.toJS,
  );
  @override
  Future<void> send(String request) async => _post({'request': request});
  @override
  Future<void> suspend(bool suspended) async => _post({'suspended': suspended});
  @override
  Future<void> reload() async {
    if (_frame != null) _frame!.src = _frame!.src;
  }

  @override
  Widget build(BuildContext context) => HtmlElementView.fromTagName(
    tagName: 'iframe',
    onElementCreated: (element) {
      final frame = element as web.HTMLIFrameElement;
      _frame = frame;
      frame.title = 'Maximal Bastion battlefield';
      frame.style.border = '0';
      frame.style.width = '100%';
      frame.style.height = '100%';
      frame.style.pointerEvents = 'none';
      frame.allow = 'autoplay';
      frame.src = Uri.base
          .resolve('assets/assets/runtime/index.html?surface=mobile')
          .toString();
    },
  );
  @override
  void dispose() {
    web.window.removeEventListener('message', _listener);
    super.dispose();
  }
}
