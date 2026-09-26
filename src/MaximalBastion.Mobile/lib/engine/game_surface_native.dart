import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:path_provider/path_provider.dart';
import 'package:webview_flutter/webview_flutter.dart';
import 'package:webview_flutter_android/webview_flutter_android.dart';
import 'package:webview_flutter_wkwebview/webview_flutter_wkwebview.dart';

import 'engine_bridge.dart';
import 'runtime_server.dart';

class GameSurface extends StatefulWidget {
  const GameSurface({super.key, required this.bridge});
  final EngineBridge bridge;
  @override
  State<GameSurface> createState() => _GameSurfaceState();
}

class _GameSurfaceState extends State<GameSurface> implements EngineTransport {
  WebViewController? _controller;
  RuntimeServer? _server;
  @override
  void initState() {
    super.initState();
    unawaited(_initialize());
  }

  Future<void> _initialize() async {
    try {
      final documents = await getApplicationSupportDirectory();
      final store = EngineFileStore(
        Directory('${documents.path}/MaximalBastion'),
      );
      await store.load();
      final server = RuntimeServer(store);
      _server = server;
      final url = await server.start();
      final params = WebViewPlatform.instance is WebKitWebViewPlatform
          ? WebKitWebViewControllerCreationParams(
              allowsInlineMediaPlayback: true,
              mediaTypesRequiringUserAction: const <PlaybackMediaTypes>{},
            )
          : const PlatformWebViewControllerCreationParams();
      final controller = WebViewController.fromPlatformCreationParams(params);
      _controller = controller;
      await controller.setJavaScriptMode(JavaScriptMode.unrestricted);
      await controller.setBackgroundColor(const Color(0xff182b32));
      await controller.enableZoom(false);
      await controller.addJavaScriptChannel(
        'MaximalBastionMobile',
        onMessageReceived: (message) {
          final data = object(jsonDecode(message.message));
          if (data['type'] == 'storage') {
            unawaited(
              store
                  .update(data['path'] as String, data['contents'] as String?)
                  .catchError((Object error) {
                    widget.bridge.fail(
                      'Progress could not be saved on this device: $error',
                    );
                  }),
            );
          } else {
            widget.bridge.receive(message.message);
          }
        },
      );
      await controller.setNavigationDelegate(
        NavigationDelegate(
          onNavigationRequest: (request) =>
              server.permits(Uri.parse(request.url))
              ? NavigationDecision.navigate
              : NavigationDecision.prevent,
          onWebResourceError: (error) {
            if (error.isForMainFrame == true) {
              widget.bridge.fail(
                'The bundled engine could not load: ${error.description}',
                fatal: true,
              );
            }
          },
        ),
      );
      if (controller.platform is AndroidWebViewController) {
        await (controller.platform as AndroidWebViewController)
            .setMediaPlaybackRequiresUserGesture(false);
      }
      widget.bridge.transport = this;
      if (!mounted) {
        await server.close();
        return;
      }
      setState(() {});
      await controller.loadRequest(url);
    } catch (error) {
      widget.bridge.fail(
        'The offline game could not start: $error',
        fatal: true,
      );
    }
  }

  @override
  Future<void> send(String request) async => _controller?.runJavaScript(
    'window.maximalBastion.mobile.send(${jsonEncode(request)});',
  );
  @override
  Future<void> suspend(bool suspended) async {
    await _controller?.runJavaScript(
      'window.maximalBastion?.mobile.suspend($suspended);',
    );
    if (suspended) await _server?.store.flush();
  }

  @override
  Future<void> reload() async {
    await _server?.store.flush();
    await _controller?.reload();
  }

  @override
  Widget build(BuildContext context) => _controller == null
      ? const ColoredBox(color: Color(0xff182b32))
      : WebViewWidget(controller: _controller!);
  @override
  void dispose() {
    unawaited(_server?.close());
    super.dispose();
  }
}
