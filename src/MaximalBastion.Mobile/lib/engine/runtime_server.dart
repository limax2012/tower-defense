import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter/services.dart';

/// Engine files retain the shared save schema; native storage survives changes to the loopback origin.
class EngineFileStore {
  EngineFileStore(this.directory);
  final Directory directory;
  final Map<String, String> files = {};
  Future<void> _writes = Future.value();
  static const maximumFileBytes = 8 * 1024 * 1024;
  static const maximumFiles = 2048;
  static bool validKey(String key) =>
      key.isNotEmpty &&
      key.length <= 240 &&
      !key.startsWith('/') &&
      !key.contains('\\') &&
      key
          .split('/')
          .every(
            (part) =>
                part != '.' &&
                part != '..' &&
                RegExp(r'^[A-Za-z0-9_.-]+$').hasMatch(part),
          );

  Future<void> load() async {
    await directory.create(recursive: true);
    await for (final entry in directory.list(
      recursive: true,
      followLinks: false,
    )) {
      if (entry is! File || entry.path.endsWith('.mobile-tmp')) continue;
      final key = entry.path
          .substring(directory.path.length + 1)
          .replaceAll('\\', '/');
      if (!validKey(key) ||
          files.length >= maximumFiles ||
          await entry.length() > maximumFileBytes) {
        continue;
      }
      files[key] = base64Encode(await entry.readAsBytes());
    }
  }

  Future<void> update(String key, String? contents) {
    if (!validKey(key)) {
      return Future.error(
        const FormatException('Invalid engine storage path.'),
      );
    }
    if (contents != null &&
        (contents.length > (maximumFileBytes * 4 / 3).ceil() + 4 ||
            (!files.containsKey(key) && files.length >= maximumFiles))) {
      return Future.error(
        const FormatException('The engine storage limit was reached.'),
      );
    }
    final Uint8List? bytes;
    try {
      bytes = contents == null ? null : base64Decode(contents);
    } catch (error) {
      return Future.error(error);
    }
    if (bytes != null && bytes.length > maximumFileBytes) {
      return Future.error(
        const FormatException('The engine storage limit was reached.'),
      );
    }
    final operation = _writes.then((_) async {
      if (files[key] == contents) return;
      if (contents != null &&
          !files.containsKey(key) &&
          files.length >= maximumFiles) {
        throw const FormatException('The engine storage limit was reached.');
      }
      final destination = File(
        '${directory.path}/${key.replaceAll('/', Platform.pathSeparator)}',
      );
      if (bytes == null) {
        if (await destination.exists()) await destination.delete();
      } else {
        await destination.parent.create(recursive: true);
        final temporary = File('${destination.path}.mobile-tmp');
        await temporary.writeAsBytes(bytes!, flush: true);
        await temporary.rename(destination.path);
      }
      if (contents == null) {
        files.remove(key);
      } else {
        files[key] = contents;
      }
    });
    _writes = operation.catchError((Object _) {});
    return operation;
  }

  Future<void> flush() => _writes;
}

/// Serves only bundled assets on loopback. Gameplay never depends on an external server.
class RuntimeServer {
  RuntimeServer(this.store);
  final EngineFileStore store;
  HttpServer? _server;
  late Uri origin;
  late String _prefix;
  late Set<String> _assets;
  Future<Uri> start() async {
    final manifest = jsonDecode(
      await rootBundle.loadString('assets/runtime/runtime-manifest.json'),
    ) as Map;
    _assets = (manifest['files'] as List).cast<String>().toSet();
    final random = Random.secure();
    _prefix =
        '/${List.generate(24, (_) => random.nextInt(16).toRadixString(16)).join()}/';
    final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    _server = server;
    origin = Uri.parse('http://127.0.0.1:${server.port}');
    server.listen((request) {
      unawaited(_serve(request));
    });
    return origin.replace(
      path: '${_prefix}index.html',
      query: 'surface=mobile',
    );
  }

  bool permits(Uri uri) =>
      uri.scheme == origin.scheme &&
      uri.host == origin.host &&
      uri.port == origin.port &&
      uri.path.startsWith(_prefix);
  Future<void> _serve(HttpRequest request) async {
    try {
      if (!request.uri.path.startsWith(_prefix) ||
          !['GET', 'HEAD'].contains(request.method)) {
        request.response.statusCode = HttpStatus.notFound;
        return;
      }
      final path = request.uri.path.substring(_prefix.length);
      if (path == 'native-bootstrap.js') {
        request.response.headers.contentType = ContentType(
          'text',
          'javascript',
          charset: 'utf-8',
        );
        request.response.headers.set(
          HttpHeaders.cacheControlHeader,
          'no-store',
        );
        if (request.method == 'GET') {
          request.response.write(
            'window.maximalBastionNativeFiles=${jsonEncode(store.files)};',
          );
        }
        return;
      }
      if (!_assets.contains(path)) {
        request.response.statusCode = HttpStatus.notFound;
        return;
      }
      final asset = await rootBundle.load('assets/runtime/$path');
      final bytes = asset.buffer.asUint8List(
        asset.offsetInBytes,
        asset.lengthInBytes,
      );
      request.response.headers.contentType = switch (path
          .split('.')
          .last
          .toLowerCase()) {
        'html' => ContentType.html,
        'js' || 'mjs' => ContentType('text', 'javascript'),
        'wasm' => ContentType('application', 'wasm'),
        'json' => ContentType.json,
        'css' => ContentType('text', 'css'),
        'svg' => ContentType('image', 'svg+xml'),
        'png' => ContentType('image', 'png'),
        'ogg' => ContentType('audio', 'ogg'),
        'mp3' => ContentType('audio', 'mpeg'),
        _ => ContentType.binary,
      };
      request.response.contentLength = bytes.length;
      if (request.method == 'GET') request.response.add(bytes);
    } catch (_) {
      request.response.statusCode = HttpStatus.internalServerError;
    } finally {
      await request.response.close();
    }
  }

  Future<void> close() async {
    await _server?.close(force: true);
    await store.flush();
  }
}
