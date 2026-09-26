import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../engine/engine_bridge.dart';
import '../engine/game_surface.dart';
import 'tower_glyph.dart';

const navy = Color(0xff152b46);
const paper = Color(0xfff4f5f8);
const teal = Color(0xff2192aa);
const green = Color(0xff127045);
const actionSurface = Color(0xff102033);
const actionBorder = Color(0xff526b83);
const actionMuted = Color(0xff8a9daf);

class BastionApp extends StatelessWidget {
  const BastionApp({super.key, this.bridge, this.surface});
  final EngineBridge? bridge;
  final Widget? surface;
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Maximal Bastion',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      useMaterial3: true,
      scaffoldBackgroundColor: navy,
      colorScheme: ColorScheme.fromSeed(seedColor: teal, surface: paper),
      textTheme: const TextTheme(
        bodyMedium: TextStyle(fontSize: 15),
        bodyLarge: TextStyle(fontSize: 16),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(48, 56),
          backgroundColor: actionSurface,
          foregroundColor: paper,
          disabledBackgroundColor: actionSurface,
          disabledForegroundColor: actionMuted,
          side: const BorderSide(color: actionBorder),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
          textStyle: const TextStyle(
            fontFamily: 'Roboto',
            fontSize: 17,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(minimumSize: const Size(48, 48)),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(minimumSize: const Size(48, 48)),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: navy),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        border: OutlineInputBorder(),
      ),
    ),
    home: BastionHome(bridge: bridge, surface: surface),
  );
}

class BastionHome extends StatefulWidget {
  const BastionHome({super.key, this.bridge, this.surface});
  final EngineBridge? bridge;
  final Widget? surface;
  @override
  State<BastionHome> createState() => _BastionHomeState();
}

class _BastionHomeState extends State<BastionHome> with WidgetsBindingObserver {
  late final EngineBridge engine;
  late final Widget _surface;
  final _fieldKey = GlobalKey();
  final _surfaceKey = GlobalKey();
  final _camera = TransformationController();
  final List<String> _pages = [];
  bool _resumeAfterPage = false;
  String _map = 'foundry_loop', _difficulty = 'normal', _challenge = 'standard';
  JsonMap _reference = {}, _upgrade = {};
  List<JsonMap> _saves = [], _history = [];
  JsonMap _career = {};
  String _enemyId = '', _rank = 'Standard', _signal = 'None';
  int _enemyCount = 5, _sandboxWave = 1;
  double _health = 1;
  bool _immortal = false;
  bool _pointerBusy = false;
  Offset? _queuedPointer;
  DateTime _lastPointer = DateTime.fromMillisecondsSinceEpoch(0);
  String? _lastRunId;

  @override
  void initState() {
    super.initState();
    engine = widget.bridge ?? EngineBridge();
    _surface = widget.surface ?? GameSurface(key: _surfaceKey, bridge: engine);
    engine.addListener(_refresh);
    WidgetsBinding.instance.addObserver(this);
  }

  void _refresh() {
    if (!mounted) return;
    if (engine.state['notice'] != null && engine.error == null) {
      engine.error = engine.state['notice'].toString();
    }
    if (engine.runId != _lastRunId) {
      _lastRunId = engine.runId;
      _camera.value = Matrix4.identity();
    }
    setState(() {});
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    unawaited(
      engine.transport?.suspend(state != AppLifecycleState.resumed).catchError((
        Object error,
      ) {
        engine.fail(
          'The game could not pause for the device interruption: $error',
        );
      }),
    );
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    engine.removeListener(_refresh);
    if (widget.bridge == null) engine.dispose();
    _camera.dispose();
    super.dispose();
  }

  Future<void> _open(String page, {bool pause = false}) async {
    if (pause &&
        engine.screen == 'playing' &&
        engine.state['paused'] != true &&
        _pages.isEmpty) {
      try {
        await engine.request('pause', {'paused': true});
        _resumeAfterPage = true;
      } catch (_) {
        return;
      }
    }
    if (!mounted) return;
    setState(() {
      _pages.add(page);
    });
    try {
      if (page == 'saves') _saves = objects(await engine.request('saves'));
      if (page == 'history') {
        _history = objects(await engine.request('history'));
      }
      if (page == 'career') _career = object(await engine.request('career'));
      if (mounted) setState(() {});
    } catch (error) {
      engine.fail(error.toString());
    }
  }

  void _back() {
    if (_pages.isNotEmpty) {
      setState(() {
        _pages.removeLast();
      });
    }
    if (_pages.isEmpty && _resumeAfterPage) {
      _resumeAfterPage = false;
      engine.issue('pause', {'paused': false});
    }
  }

  Future<bool> _confirm(String title, String message, String action) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(message),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: Text(action),
            ),
          ],
        ),
      ) ??
      false;

  Future<void> _newRun() async {
    if (engine.screen == 'playing' &&
        !await _confirm(
          'Start a new defense?',
          'The current defense will be replaced. Its latest checkpoint remains in Saves.',
          'New defense',
        )) {
      return;
    }
    try {
      await engine.request('newRun', {
        'mapId': _map,
        'difficultyId': _difficulty,
        'challengeId': _challenge,
      });
      if (mounted) {
        setState(() {
          _pages.clear();
          _resumeAfterPage = false;
        });
      }
    } catch (error) {
      engine.fail(error.toString());
    }
  }

  Offset? _worldPoint(Offset global) {
    final box = _fieldKey.currentContext?.findRenderObject() as RenderBox?;
    if (box == null || !box.hasSize) return null;
    final local = box.globalToLocal(global);
    if (!(Offset.zero & box.size).contains(local)) return null;
    return Offset(
      local.dx / box.size.width * 960,
      local.dy / box.size.height * 720,
    );
  }

  Future<void> _preview(Offset global, {bool force = false}) async {
    final point = _worldPoint(global - const Offset(0, 44));
    if (point == null) return;
    _queuedPointer = point;
    if (_pointerBusy ||
        (!force &&
            DateTime.now().difference(_lastPointer).inMilliseconds < 45)) {
      return;
    }
    await _sendPreview();
  }

  Future<void> _sendPreview() async {
    final point = _queuedPointer;
    if (point == null || _pointerBusy) return;
    _queuedPointer = null;
    _pointerBusy = true;
    _lastPointer = DateTime.now();
    try {
      await engine.request('pointer', {'x': point.dx, 'y': point.dy});
    } catch (error) {
      engine.fail(error.toString());
    } finally {
      _pointerBusy = false;
      if (_queuedPointer != null && mounted) unawaited(_sendPreview());
    }
  }

  Future<void> _tapField(Offset global) async {
    final point = _worldPoint(global);
    if (point == null) return;
    final placing = engine.placement['active'] == true;
    final box = _fieldKey.currentContext!.findRenderObject() as RenderBox;
    final args = <String, dynamic>{
      'x': point.dx,
      'y': point.dy,
      'commit': !placing,
      'radius': 26 * 960 / box.size.width / _camera.value.getMaxScaleOnAxis(),
    };
    try {
      final response = object(await engine.request('pointer', args));
      final candidates = objects(response['candidates']);
      if (candidates.isEmpty || !mounted) return;
      final id = await showDialog<int>(
        context: context,
        builder: (context) => SimpleDialog(
          title: const Text('Choose a tower'),
          children: candidates
              .map(
                (tower) => SimpleDialogOption(
                  onPressed: () => Navigator.pop(context, tower['id']),
                  child: Padding(
                    padding: const EdgeInsets.all(10),
                    child: Text(
                      '${tower['displayName']} · ${tower['progression']}',
                    ),
                  ),
                ),
              )
              .toList(),
        ),
      );
      if (id != null) engine.issue('pointer', {...args, 'towerId': id});
    } catch (error) {
      engine.fail(error.toString());
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: false,
    onPopInvokedWithResult: (didPop, _) {
      if (didPop) return;
      if (_pages.isNotEmpty) {
        _back();
      } else if (engine.screen == 'playing') {
        unawaited(_open('pause', pause: true));
      }
    },
    child: Scaffold(
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            final landscape =
                constraints.maxWidth > constraints.maxHeight &&
                constraints.maxWidth >= 600;
            final inMenu = engine.screen == 'menu';
            return Stack(
              children: [
                ExcludeSemantics(
                  excluding:
                      inMenu ||
                      !engine.ready ||
                      engine.fatal ||
                      _pages.isNotEmpty ||
                      ['victory', 'defeat'].contains(engine.screen),
                  child: Column(
                    children: [
                      _hud(),
                      Expanded(
                        child: landscape
                            ? Row(
                                children: [
                                  Expanded(child: _battlefield()),
                                  SizedBox(
                                    width: _hasSelection ? 244 : 132,
                                    child: _dock(true),
                                  ),
                                ],
                              )
                            : Column(
                                children: [
                                  Expanded(child: _battlefield()),
                                  SizedBox(
                                    height: _hasSelection ? 224 : 182,
                                    child: _dock(false),
                                  ),
                                ],
                              ),
                      ),
                    ],
                  ),
                ),
                if (inMenu)
                  Positioned.fill(
                    child: ExcludeSemantics(
                      excluding: _pages.isNotEmpty,
                      child: _menu(),
                    ),
                  ),
                if (!engine.ready || engine.fatal)
                  Positioned.fill(child: _loading()),
                if (engine.ready &&
                    ['victory', 'defeat'].contains(engine.screen) &&
                    _pages.isEmpty)
                  Positioned.fill(
                    child: _pageFrame(
                      engine.screen == 'victory'
                          ? 'Bastion secured'
                          : 'Defense breached',
                      _result(),
                      close: false,
                    ),
                  ),
                if (engine.ready && _pages.isNotEmpty)
                  Positioned.fill(
                    child: _pageFrame(
                      _pageTitle(_pages.last),
                      _page(_pages.last),
                    ),
                  ),
                if (engine.ready && !engine.fatal && engine.error != null)
                  Align(
                    alignment: Alignment.bottomCenter,
                    child: Material(
                      color: const Color(0xffffe5e8),
                      child: Padding(
                        padding: const EdgeInsets.only(left: 16),
                        child: Row(
                          children: [
                            Expanded(
                              child: Text(
                                engine.error!,
                                maxLines: 3,
                                style: const TextStyle(
                                  color: Color(0xff7e243c),
                                ),
                              ),
                            ),
                            IconButton(
                              tooltip: 'Dismiss message',
                              onPressed: engine.clearError,
                              icon: const Icon(Icons.close),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
      ),
    ),
  );

  bool get _hasSelection =>
      engine.selected.isNotEmpty ||
      engine.placement['active'] == true ||
      object(engine.state['generator'])['selected'] == true;

  Widget _loading() => ColoredBox(
    color: navy,
    child: Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.hexagon_outlined,
              color: Color(0xffe8b637),
              size: 64,
            ),
            const SizedBox(height: 20),
            const Text(
              'MAXIMAL BASTION',
              style: TextStyle(color: paper, fontSize: 26, letterSpacing: 2),
            ),
            const SizedBox(height: 20),
            if (!engine.fatal) const CircularProgressIndicator(color: teal),
            const SizedBox(height: 16),
            Text(
              engine.fatal
                  ? engine.error ?? 'Unable to start.'
                  : 'Preparing your defense…',
              textAlign: TextAlign.center,
              style: const TextStyle(color: paper),
            ),
            if (engine.fatal)
              Padding(
                padding: const EdgeInsets.only(top: 20),
                child: FilledButton(
                  onPressed: () {
                    engine.fatal = false;
                    engine.error = null;
                    setState(() {});
                    unawaited(engine.transport?.reload());
                  },
                  child: const Text('Retry'),
                ),
              ),
          ],
        ),
      ),
    ),
  );

  Widget _menu() => ColoredBox(
    color: navy,
    child: Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 460),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Icon(
                Icons.hexagon_outlined,
                color: Color(0xffe8b637),
                size: 58,
              ),
              const SizedBox(height: 14),
              const Text(
                'MINIMAL\nBASTION',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: paper,
                  fontSize: 34,
                  height: 1.05,
                  letterSpacing: 4,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Build. Adapt. Hold the line.',
                textAlign: TextAlign.center,
                style: TextStyle(color: Color(0xffaebdca)),
              ),
              const SizedBox(height: 28),
              _button('New defense', () => _open('setup'), primary: true),
              _menuButton(
                'Continue / saves',
                Icons.save_outlined,
                () => _open('saves'),
              ),
              _menuButton('History', Icons.history, () => _open('history')),
              _menuButton(
                'Career',
                Icons.emoji_events_outlined,
                () => _open('career'),
              ),
              _menuButton(
                'Settings',
                Icons.settings_outlined,
                () => _open('settings'),
              ),
            ],
          ),
        ),
      ),
    ),
  );

  Widget _menuButton(String label, IconData icon, VoidCallback action) =>
      _button(label, action, icon: icon);

  Widget _hud() {
    final s = engine.state;
    return Container(
      color: navy,
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      child: Wrap(
        alignment: WrapAlignment.spaceBetween,
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 6,
        children: [
          Semantics(
            label: 'Lives',
            child: Text(
              '♥ ${s['isSandbox'] == true ? '∞' : s['lives'] ?? '–'}',
              style: const TextStyle(
                color: Color(0xffff899c),
                fontWeight: FontWeight.w600,
              ),
            ),
          ),

          Semantics(
            label: 'Credits',
            child: Text(
              '● ${s['isSandbox'] == true ? '∞' : s['credits'] ?? '–'}',
              style: const TextStyle(
                color: Color(0xfff6d679),
                fontWeight: FontWeight.w600,
              ),
            ),
          ),

          TextButton(
            style: TextButton.styleFrom(
              foregroundColor: paper,
              minimumSize: const Size(48, 48),
            ),
            onPressed: () => _open('wave', pause: true),
            child: Text(
              s['isSandbox'] == true
                  ? 'Lab'
                  : '${s['isEndlessMode'] == true ? 'Endless' : 'Wave'} ${s['currentWave'] ?? 0}${s['isEndlessMode'] == true ? '' : '/${s['totalWaves'] ?? 30}'}',
            ),
          ),
          TextButton(
            style: TextButton.styleFrom(
              foregroundColor: paper,
              minimumSize: const Size(48, 48),
            ),
            onPressed: engine.canMutate
                ? () => engine.command('SetSpeed', {
                    'speed': number(s['speed']) >= 1.5 ? 1 : 2,
                  })
                : null,
            child: Text('${number(s['speed'], 1).toInt()}×'),
          ),
          IconButton(
            color: paper,
            tooltip: s['paused'] == true ? 'Resume' : 'Pause',
            onPressed: () {
              if (s['paused'] == true && engine.screen == 'playing') {
                engine.issue('pause', {'paused': false});
              } else {
                unawaited(_open('pause', pause: true));
              }
            },
            icon: Icon(s['paused'] == true ? Icons.play_arrow : Icons.pause),
          ),
        ],
      ),
    );
  }

  Widget _battlefield() => Column(
    children: [
      Expanded(
        child: LayoutBuilder(
          builder: (context, bounds) {
            final width = math.min(bounds.maxWidth, bounds.maxHeight * 4 / 3);
            return ClipRect(
              child: InteractiveViewer(
                transformationController: _camera,
                minScale: 1,
                maxScale: 3,
                panEnabled: engine.placement['active'] != true,
                child: SizedBox(
                  width: bounds.maxWidth,
                  height: bounds.maxHeight,
                  child: Center(
                    child: SizedBox(
                      key: _fieldKey,
                      width: width,
                      height: width * 3 / 4,
                      child: Stack(
                        fit: StackFit.expand,
                        children: [
                          IgnorePointer(child: _surface),
                          GestureDetector(
                            key: const ValueKey('battlefield-input'),
                            excludeFromSemantics: true,
                            behavior: HitTestBehavior.opaque,
                            onTapUp: (details) =>
                                _tapField(details.globalPosition),
                            onPanUpdate: engine.placement['active'] == true
                                ? (details) => _preview(details.globalPosition)
                                : null,
                            onPanEnd: engine.placement['active'] == true
                                ? (_) => _sendPreview()
                                : null,
                            child: const ColoredBox(color: Colors.transparent),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            );
          },
        ),
      ),
      Container(
        color: const Color(0xff182b32),
        padding: const EdgeInsets.symmetric(horizontal: 8),
        child: Row(
          children: [
            if (engine.tactical['pulsePlatesEnabled'] == true)
              TextButton(
                style: TextButton.styleFrom(
                  foregroundColor: const Color(0xfff6d679),
                  minimumSize: const Size(48, 48),
                ),
                onPressed: engine.canMutate
                    ? () => engine.issue('beginPlacement', {'kind': 'plate'})
                    : null,
                child: Text(
                  engine.state['isSandbox'] == true
                      ? '◎ ∞'
                      : '◎ ${engine.tactical['emergencyInventory'] ?? 0}',
                ),
              ),
            if (engine.tactical['tacticalSystemsEnabled'] == true)
              TextButton(
                style: TextButton.styleFrom(
                  foregroundColor: paper,
                  minimumSize: const Size(48, 48),
                ),
                onPressed: engine.canMutate
                    ? () => engine.issue('beginPlacement', {'kind': 'forge'})
                    : null,
                child: const Text('Forge'),
              ),
            if (engine.state['isSandbox'] == true)
              Flexible(
                child: TextButton(
                  style: TextButton.styleFrom(foregroundColor: paper),
                  onPressed: () => _open('sandbox'),
                  child: const Text(
                    'Lab controls',
                    textAlign: TextAlign.center,
                  ),
                ),
              ),
            const Spacer(),
            IconButton(
              color: paper,
              tooltip: 'Fit whole map',
              onPressed: () => _camera.value = Matrix4.identity(),
              icon: const Icon(Icons.fit_screen),
            ),
            if (engine.state['paused'] == true)
              const Padding(
                padding: EdgeInsets.only(right: 8),
                child: Text(
                  'PAUSED',
                  style: TextStyle(color: Color(0xfff6d679), fontSize: 12),
                ),
              ),
          ],
        ),
      ),
    ],
  );

  Widget _dock(bool landscape) => ColoredBox(
    color: paper,
    child: Padding(
      padding: const EdgeInsets.all(8),
      child: engine.placement['active'] == true
          ? _placementPanel()
          : engine.selected.isNotEmpty
          ? _towerPanel()
          : object(engine.state['generator'])['selected'] == true
          ? _forgePanel()
          : Column(
              children: [
                Expanded(
                  child: LayoutBuilder(
                    builder: (context, bounds) => GridView.count(
                      crossAxisCount:
                          (bounds.maxWidth /
                                  (56 *
                                      MediaQuery.textScalerOf(context)
                                          .scale(1)))
                              .floor()
                              .clamp(1, 5),
                      mainAxisSpacing: 6,
                      crossAxisSpacing: 6,
                      mainAxisExtent:
                          42 + MediaQuery.textScalerOf(context).scale(14),
                      children: objects(engine.catalog['towers'])
                          .map(_towerButton)
                          .toList(),
                    ),
                  ),
                ),
                const SizedBox(height: 4),
                _button(
                  engine.state['isSandbox'] == true
                      ? 'Test wave'
                      : _friendly(
                          engine.state['waveButtonLabel']?.toString() ??
                              'Start wave',
                        ),
                  engine.canMutate && engine.state['canStartWave'] == true
                      ? () {
                          if (engine.state['isSandbox'] == true) {
                            unawaited(_open('sandbox'));
                          } else {
                            engine.command('StartWave');
                          }
                        }
                      : null,
                  primary: true,
                ),
              ],
            ),
    ),
  );

  Widget _towerButton(JsonMap tower) {
    final available = (engine.state['availableTowers'] as List? ?? []).contains(
      tower['id'],
    );
    final affordable =
        engine.state['isSandbox'] == true ||
        number(engine.state['credits']) >= number(tower['purchaseCost']);
    final enabled = available && engine.canMutate;
    final button = Tooltip(
      message: '${tower['displayName']} · ${tower['role']}',
      child: OutlinedButton(
        style: OutlinedButton.styleFrom(
          padding: const EdgeInsets.all(2),
          minimumSize: const Size(48, 48),
          side: BorderSide(
            color: affordable
                ? const Color(0xffb4c2d2)
                : const Color(0xffbc4662),
          ),
        ),
        onPressed: enabled
            ? () => engine.issue('beginPlacement', {
                'kind': 'tower',
                'towerId': tower['id'],
              })
            : null,
        child: Semantics(
          label:
              '${tower['displayName']}, ${tower['purchaseCost']} credits${affordable ? '' : ', insufficient credits'}',
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              TowerGlyph(tower: tower, size: 28),
              Text(
                '${tower['purchaseCost']}',
                style: TextStyle(
                  fontSize: 12,
                  color: affordable ? navy : const Color(0xff9f284a),
                ),
              ),
            ],
          ),
        ),
      ),
    );
    if (!enabled || !affordable) return button;
    return Draggable<JsonMap>(
      data: tower,
      maxSimultaneousDrags: 1,
      feedback: Material(
        type: MaterialType.transparency,
        child: Transform.translate(
          offset: const Offset(-24, -68),
          child: TowerGlyph(tower: tower, size: 48),
        ),
      ),
      onDragStarted: () => engine.issue('beginPlacement', {
        'kind': 'tower',
        'towerId': tower['id'],
      }),
      onDragUpdate: (details) => _preview(details.globalPosition),
      onDragEnd: (_) => _sendPreview(),
      child: button,
    );
  }

  Widget _panel(String title, List<Widget> children) => SingleChildScrollView(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                title,
                style: const TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            IconButton(
              tooltip: 'Close selection',
              onPressed: () => engine.issue('cancel'),
              icon: const Icon(Icons.close),
            ),
          ],
        ),
        ...children,
      ],
    ),
  );

  Widget _placementPanel() {
    final p = engine.placement;
    final tower = objects(engine.catalog['towers'])
        .where((tower) => tower['id'] == p['towerId'])
        .firstOrNull;
    final label =
        tower?['displayName']?.toString() ??
        (p['kind'] == 'PulsePlate' ? 'Pulse Plate' : 'Charge Forge');
    final valid = p['hasPlacementPreview'] == true && p['failure'] == 'None';
    return _panel(label, [
      if (tower != null)
        Text('${tower['role']} · ${tower['purchaseCost']} credits'),
      if (p['kind'] == 'PulsePlate')
        Text(_friendly(engine.tactical['plateLabel'].toString())),
      if (p['kind'] == 'ChargeForge')
        Text(
          '${object(object(engine.catalog['tactics'])['generator'])['purchaseCost']} credits · produces Plates during waves',
        ),
      const SizedBox(height: 8),
      Text(
        valid
            ? 'Position ready · range shown on map'
            : p['failure'] == 'None'
            ? 'Tap or drag on the map to position.'
            : _humanize(p['failure'].toString()),
        style: TextStyle(
          color: valid ? green : const Color(0xff5b6b80),
          fontSize: 14,
        ),
      ),
      if (objects(p['nodes']).isNotEmpty)
        Text(
          objects(p['nodes']).map((node) => node['displayName']).join(' · '),
        ),
      const SizedBox(height: 8),
      _button(
        'Place here',
        valid && engine.canMutate
            ? () => engine.issue('pointer', {
                'x': p['x'],
                'y': p['y'],
                'commit': true,
              })
            : null,
        primary: true,
      ),
      if (tower != null)
        TextButton(
          onPressed: () {
            _reference = tower;
            unawaited(_open('towerReference', pause: true));
          },
          child: const Text('Tower reference'),
        ),
    ]);
  }

  Widget _towerPanel() {
    final t = engine.selected, protocol = object(t['protocol']);
    final active = number(protocol['overdriveRemaining']);
    final cooldown = number(protocol['cooldown']);
    final upgrades = objects(t['upgrades']);
    return _panel(t['displayName'].toString(), [
      Text(
        '${t['progression']}${t['isApex'] == true ? ' · APEX' : ''}',
        style: const TextStyle(color: Color(0xff5b6b80), fontSize: 13),
      ),
      if (t['isDisrupted'] == true || t['isSuppressed'] == true)
        Text(
          t['isDisrupted'] == true ? 'Disrupted' : 'Signal weakened',
          style: const TextStyle(color: Color(0xffbc2f8a)),
        ),
      const SizedBox(height: 8),
      if (upgrades.isNotEmpty)
        _button(
          upgrades.length > 1
              ? 'Choose upgrade · ${upgrades.map((u) => number(u['cost'])).reduce(math.min).toInt()}+'
              : '${upgrades.first['name']} · ${upgrades.first['cost']}',
          engine.canMutate ? () => _open('upgrades') : null,
          primary: true,
        ),
      if (t['isSupport'] != true)
        _button('Target · ${t['targetMode']}', () => _open('target')),
      if (engine.tactical['protocolsEnabled'] == true ||
          protocol['self'] == true) ...[
        _button(
          '${protocol['displayName']} · ${active > 0
              ? '${active.ceil()}s active'
              : cooldown > 0
              ? '${cooldown.ceil()}s'
              : 'Ready'}',
          engine.canMutate && protocol['ready'] == true
              ? () => engine.command('OverdriveTower', {'entityId': t['id']})
              : null,
        ),
        if (protocol['self'] == true)
          const Text(
            'Apex · self-activating Protocol',
            style: TextStyle(fontSize: 13, color: Color(0xff5b6b80)),
          )
        else
          _button(
            'Auto · ${protocol['armed'] == true
                ? 'Armed here'
                : number(engine.tactical['autoOverdriveTowerId']) > 0
                ? 'Move here'
                : 'Arm here'}',
            engine.canMutate
                ? () => engine.command('ToggleAutoProtocol', {
                    'entityId': t['id'],
                  })
                : null,
          ),
      ],
      TextButton(
        onPressed: () => _open('details'),
        child: const Text('Details & sell'),
      ),
    ]);
  }

  Widget _forgePanel() {
    final forge = object(engine.state['generator']);
    return _panel('Charge Forge · ${forge['level']}', [
      Text(
        '${engine.tactical['emergencyInventory']}/${forge['capacity']} stored',
      ),
      Text(
        engine.state['waveActive'] == true
            ? 'Next Plate in ${number(forge['productionRemaining']).ceil()}s'
            : 'Production resumes during waves',
      ),
      if (forge['canUpgrade'] == true)
        _button(
          'Upgrade · ${forge['upgradeCost']}',
          engine.canMutate ? () => engine.command('UpgradeGenerator') : null,
          primary: true,
        ),
      if (engine.tactical['sellingEnabled'] == true)
        _button(
          'Sell · ${forge['sellValue']}',
          engine.canMutate
              ? () async {
                  if (await _confirm(
                    'Sell Charge Forge?',
                    'Receive ${forge['sellValue']} credits.',
                    'Sell',
                  )) {
                    engine.command('SellGenerator');
                  }
                }
              : null,
        ),
    ]);
  }

  Widget _pageFrame(String title, Widget body, {bool close = true}) =>
      ColoredBox(
        color: navy.withValues(alpha: .94),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 680),
            child: Material(
              color: paper,
              child: Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.only(
                      left: 16,
                      right: 6,
                      top: 4,
                      bottom: 4,
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            title,
                            style: const TextStyle(
                              fontSize: 21,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                        if (close)
                          IconButton(
                            tooltip: 'Back',
                            onPressed: _back,
                            icon: const Icon(Icons.close),
                          ),
                      ],
                    ),
                  ),
                  const Divider(height: 1),
                  Expanded(
                    child: SingleChildScrollView(
                      padding: const EdgeInsets.all(16),
                      child: body,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );

  String _pageTitle(String page) => switch (page) {
    'setup' => 'Prepare your defense',
    'pause' => 'Defense paused',
    'saves' => 'Saved defenses',
    'history' => 'Run history',
    'career' => 'Career',
    'settings' => 'Settings',
    'details' => engine.selected['displayName']?.toString() ?? 'Tower details',
    'upgrades' => 'Choose an upgrade',
    'upgradePreview' => _upgrade['name'].toString(),
    'target' => 'Target priority',
    'wave' => 'Wave intel',
    'towerReference' => _reference['displayName'].toString(),
    'sandbox' => 'Sandbox Lab',
    'runDetail' => 'Defense record',
    _ => 'Maximal Bastion',
  };

  Widget _page(String page) => switch (page) {
    'setup' => _setup(),
    'pause' => _pauseMenu(),
    'saves' => _saveList(),
    'history' => _historyList(),
    'career' => _careerPage(),
    'settings' => _settings(),
    'details' => _details(),
    'upgrades' => _upgrades(),
    'upgradePreview' => _upgradePreview(),
    'target' => _targets(),
    'wave' => _wave(),
    'towerReference' => _towerReference(),
    'sandbox' => _sandbox(),
    'runDetail' => _runDetail(),
    _ => const SizedBox.shrink(),
  };

  Widget _setup() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      _picker(
        'Arena',
        _map,
        objects(engine.catalog['maps']),
        (value) => setState(() => _map = value),
      ),
      _picker(
        'Difficulty',
        _difficulty,
        objects(engine.catalog['difficulties']),
        (value) => setState(() => _difficulty = value),
      ),
      _picker(
        'Mode',
        _challenge,
        objects(engine.catalog['challenges']),
        (value) => setState(() => _challenge = value),
      ),
      _button('Deploy defense', _newRun, primary: true),
    ],
  );

  Widget _picker(
    String label,
    String value,
    List<JsonMap> choices,
    ValueChanged<String> onChanged,
  ) {
    final selected =
        choices.where((item) => item['id'] == value).firstOrNull ?? {};
    return Padding(
      padding: const EdgeInsets.only(bottom: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          DropdownButtonFormField<String>(
            initialValue: choices.any((item) => item['id'] == value)
                ? value
                : null,
            decoration: InputDecoration(labelText: label),
            isExpanded: true,
            items: choices
                .map(
                  (choice) => DropdownMenuItem(
                    value: choice['id'].toString(),
                    child: Text(choice['displayName'].toString()),
                  ),
                )
                .toList(),
            onChanged: (value) {
              if (value != null) onChanged(value);
            },
          ),
          if (selected['description'] != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(selected['description'].toString()),
            ),
          if (selected['modifierSummary'] != null)
            Text(
              selected['modifierSummary'].toString(),
              style: const TextStyle(fontSize: 13, color: Color(0xff5b6b80)),
            ),
          if (selected['rules'] is List)
            ...List<String>.from(selected['rules']).map(
              (line) =>
                  Text(_friendly(line), style: const TextStyle(fontSize: 13)),
            ),
        ],
      ),
    );
  }

  Widget _pauseMenu() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(
        '${engine.state['displayName']} · ${_humanize(engine.state['difficultyId'].toString())} · ${_humanize(engine.state['challengeId'].toString())}',
      ),
      const SizedBox(height: 16),
      if (engine.screen == 'playing')
        _button('Resume defense', () {
          _resumeAfterPage = false;
          setState(() => _pages.clear());
          engine.issue('pause', {'paused': false});
        }, primary: true),
      _button('Saves', () => _open('saves')),
      _button('Settings', () => _open('settings')),
      _button('Main menu', () async {
        if (engine.screen == 'playing' &&
            !await _confirm(
              'Return to menu?',
              'Your last completed-wave checkpoint remains available in Saves.',
              'Main menu',
            )) {
          return;
        }
        engine.issue('menu');
        if (mounted) {
          setState(() {
            _pages.clear();
            _resumeAfterPage = false;
          });
        }
      }),
    ],
  );

  Widget _details() {
    final t = engine.selected;
    if (t.isEmpty) return const Text('Select a tower to inspect it.');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('${t['progression']} · ${t['role']}'),
        if (t['buffs'] != null && t['buffs'] != '')
          Text(t['buffs'].toString(), style: const TextStyle(color: teal)),
        StatRows(stats: objects(t['stats'])),
        const Divider(),
        Text(object(t['protocol'])['summary'].toString()),
        Text(
          object(t['protocol'])['auto'].toString(),
          style: const TextStyle(color: Color(0xff5b6b80)),
        ),
        const Divider(),
        Text('Lifetime damage: ${number(t['lifetimeDamage']).round()}'),
        Text('Kills: ${t['lifetimeKills']}'),
        Text(
          'Control: ${number(t['lifetimeControlSeconds']).toStringAsFixed(1)}s',
        ),
        Text(
          'Support contribution: ${number(t['lifetimeSupportDamageEquivalent']).round()}',
        ),
        const SizedBox(height: 12),
        if (engine.state['isSandbox'] == true)
          _button(
            'Test Protocol',
            engine.canMutate
                ? () => engine.issue('sandbox', {
                    'operation': 'protocol',
                    'towerId': t['id'],
                  })
                : null,
          ),
        if (engine.state['isSandbox'] == true)
          _button(
            t['isSandboxDisabled'] == true ? 'Enable tower' : 'Disable tower',
            engine.canMutate
                ? () => engine.issue('sandbox', {
                    'operation': 'toggle',
                    'towerId': t['id'],
                  })
                : null,
          ),
        if (engine.tactical['sellingEnabled'] == true &&
            engine.screen != 'inspect')
          _button(
            'Sell · ${t['sellValue']}',
            engine.canMutate
                ? () async {
                    if (await _confirm(
                      'Sell ${t['displayName']}?',
                      'Receive ${t['sellValue']} credits.',
                      'Sell',
                    )) {
                      engine.command('SellTower', {'entityId': t['id']});
                      _back();
                    }
                  }
                : null,
          ),
      ],
    );
  }

  Widget _upgrades() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'Choose a permanent role. Preview the exact changes before buying.',
      ),
      const SizedBox(height: 12),
      ...objects(engine.selected['upgrades']).map(
        (upgrade) => _card(
          upgrade['name'].toString(),
          '${upgrade['summary']}\n${upgrade['unlocked'] == true ? '${upgrade['cost']} credits' : 'Unlocks before wave 21'}',
          () {
            _upgrade = {...upgrade, 'towerId': engine.selected['id']};
            unawaited(_open('upgradePreview'));
          },
        ),
      ),
    ],
  );

  Widget _upgradePreview() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(_upgrade['summary'].toString()),
      const SizedBox(height: 12),
      StatRows(stats: objects(_upgrade['stats'])),
      const SizedBox(height: 16),
      _button(
        'Confirm · ${_upgrade['cost']}',
        engine.canMutate &&
                _upgrade['unlocked'] == true &&
                (engine.state['isSandbox'] == true ||
                    number(engine.state['credits']) >= number(_upgrade['cost']))
            ? () async {
                try {
                  await engine.request('command', {
                    'type': _upgrade['command'],
                    'entityId': _upgrade['towerId'],
                    'doctrineId': _upgrade['id'],
                    'specializationId': _upgrade['id'],
                  });
                  if (mounted) {
                    setState(() {
                      _pages.removeWhere(
                        (page) =>
                            page == 'upgrades' || page == 'upgradePreview',
                      );
                    });
                  }
                } catch (error) {
                  engine.fail(error.toString());
                }
              }
            : null,
        primary: true,
      ),
    ],
  );

  Widget _targets() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text('Choose which enemy this tower prioritizes within its range.'),
      const SizedBox(height: 12),
      ...(engine.selected['targetModes'] as List? ?? []).map(
        (mode) => _button(
          mode.toString(),
          engine.canMutate
              ? () {
                  engine.command('SetTargetMode', {
                    'entityId': engine.selected['id'],
                    'targetMode': mode,
                  });
                  _back();
                }
              : null,
        ),
      ),
    ],
  );

  Widget _wave() {
    final wave = object(engine.state['wave']), intel = object(wave['intel']);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Wave ${intel['wave'] ?? engine.state['currentWave']} · ${intel['archetype'] ?? ''}',
          style: const TextStyle(fontSize: 20),
        ),
        Text(intel['briefing']?.toString() ?? ''),
        const SizedBox(height: 12),
        Wrap(
          spacing: 6,
          children: (intel['threats'] as List? ?? [])
              .map((threat) => Chip(label: Text(_friendly(threat.toString()))))
              .toList(),
        ),
        Text(
          'Enemy health ×${number(wave['healthScale'], 1).toStringAsFixed(2)} · speed ×${number(wave['speedScale'], 1).toStringAsFixed(2)}',
        ),
        Text('${engine.state['enemiesRemaining'] ?? 0} enemies remaining'),
        const Divider(),
        ...objects(wave['groups']).map(_waveGroup),
      ],
    );
  }

  Widget _waveGroup(JsonMap group) {
    final enemy = objects(engine.catalog['enemies'])
        .where((enemy) => enemy['id'] == group['enemyId'])
        .firstOrNull;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(
        '${group['count']} × ${enemy?['displayName'] ?? group['enemyId']}',
      ),
      subtitle: Text(
        '${group['rank'] ?? 'Standard'} · spawn interval ${group['spawnInterval'] ?? group['interval'] ?? '–'}s',
      ),
    );
  }

  Widget _saveList() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      if (engine.state['canSaveCheckpoint'] == true &&
          engine.screen != 'inspect')
        _button('Save to a new slot', () async {
          try {
            await engine.request('save', {'slot': -1});
            _saves = objects(await engine.request('saves'));
            if (mounted) setState(() {});
          } catch (error) {
            engine.fail(error.toString());
          }
        }, primary: true),
      if (engine.screen == 'playing' &&
          engine.state['canSaveCheckpoint'] != true)
        const Text('Checkpoint saves become available between waves.'),
      ..._saves
          .where((slot) => slot['isOccupied'] == true || slot['error'] != null)
          .map(
            (slot) => Card(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      slot['slot'] == 0 ? 'Autosave' : 'Save ${slot['slot']}',
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    Text(
                      slot['error']?.toString() ??
                          '${_humanize(slot['mapId'].toString())} · Wave ${slot['currentWave']} · ${slot['lives']} lives',
                    ),
                    Wrap(
                      spacing: 8,
                      children: [
                        TextButton(
                          onPressed: slot['error'] != null
                              ? null
                              : () async {
                                  if (engine.screen == 'playing' &&
                                      !await _confirm(
                                        'Load this defense?',
                                        'Replace the current defense with this checkpoint.',
                                        'Load',
                                      )) {
                                    return;
                                  }
                                  try {
                                    await engine.request('load', {
                                      'slot': slot['slot'],
                                    });
                                    if (mounted) {
                                      setState(() {
                                        _pages.clear();
                                        _resumeAfterPage = false;
                                      });
                                    }
                                  } catch (error) {
                                    engine.fail(error.toString());
                                  }
                                },
                          child: const Text('Load'),
                        ),
                        TextButton(
                          onPressed: () async {
                            await _saveAction('duplicateSave', slot);
                          },
                          child: const Text('Duplicate'),
                        ),
                        TextButton(
                          onPressed: () async {
                            if (await _confirm(
                              'Delete this save?',
                              'This removes the checkpoint and its recovery copy.',
                              'Delete',
                            )) {
                              await _saveAction('deleteSave', slot);
                            }
                          },
                          child: const Text('Delete'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
      if (_saves.every(
        (slot) => slot['isOccupied'] != true && slot['error'] == null,
      ))
        const Text('No saved defenses yet.'),
    ],
  );

  Future<void> _saveAction(String action, JsonMap slot) async {
    try {
      await engine.request(action, {'slot': slot['slot']});
      _saves = objects(await engine.request('saves'));
      if (mounted) setState(() {});
    } catch (error) {
      engine.fail(error.toString());
    }
  }

  Widget _historyList() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      if (_history.isEmpty) const Text('Completed defenses appear here.'),
      ..._history.map(
        (run) => _card(
          '${run['mapName']} · ${run['victory'] == true ? 'Victory' : 'Wave ${run['currentWave']}'}',
          '${run['difficultyName']} · ${run['challengeName']} · ${run['topTowerName']}',
          () {
            _reference = run;
            unawaited(_open('runDetail'));
          },
        ),
      ),
    ],
  );

  Widget _runDetail() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      if (_reference['finalLayout'] != null)
        _button('Inspect final layout', () async {
          try {
            await engine.request('inspect', {'runId': _reference['runId']});
            if (mounted) {
              setState(() {
                _pages.clear();
                _resumeAfterPage = false;
              });
            }
          } catch (error) {
            engine.fail(error.toString());
          }
        }, primary: true),
      _facts(
        Map.fromEntries(
          _reference.entries.where((entry) => entry.key != 'finalLayout'),
        ),
      ),
    ],
  );

  Widget _careerPage() => _facts(_career);

  Widget _settings() {
    final settings = engine.settings;
    void update(JsonMap values) =>
        engine.issue('settings', {...settings, ...values});
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('Music'),
        Slider(
          value: number(settings['musicVolume'], .2).clamp(0, 1),
          divisions: 20,
          label: '${(number(settings['musicVolume'], .2) * 100).round()}%',
          onChanged: (value) => update({'musicVolume': value}),
        ),
        const Text('Sound effects'),
        Slider(
          value: number(settings['sfxVolume'], .65).clamp(0, 1),
          divisions: 20,
          onChanged: (value) => update({'sfxVolume': value}),
        ),
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Reduced effects'),
          value: settings['reducedEffects'] == true,
          onChanged: (value) => update({'reducedEffects': value}),
        ),
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Automatic next wave'),
          subtitle: const Text(
            'Wave 1 starts manually. Automatic starts retain the early-call bonus.',
          ),
          value: settings['autoStartWaves'] == true,
          onChanged: (value) => update({'autoStartWaves': value}),
        ),
        if (settings['autoStartWaves'] == true)
          DropdownButtonFormField<int>(
            initialValue: number(settings['autoStartDelaySeconds']).toInt(),
            decoration: const InputDecoration(labelText: 'Delay between waves'),
            items: [0, 3, 5, 10]
                .map(
                  (seconds) => DropdownMenuItem(
                    value: seconds,
                    child: Text(
                      seconds == 0 ? 'Immediately' : '$seconds seconds',
                    ),
                  ),
                )
                .toList(),
            onChanged: (value) => update({'autoStartDelaySeconds': value}),
          ),
      ],
    );
  }

  Widget _towerReference() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        children: [
          TowerGlyph(tower: _reference, size: 48),
          const SizedBox(width: 14),
          Expanded(
            child: Text(
              '${_reference['role']} · ${_reference['purchaseCost']} credits',
            ),
          ),
        ],
      ),
      StatRows(stats: objects(_reference['stats'])),
      const Divider(),
      Text(
        object(_reference['protocol'])['displayName'].toString(),
        style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
      ),
      Text(object(_reference['protocol'])['summary'].toString()),
      Text(object(_reference['protocol'])['auto'].toString()),
      const SizedBox(height: 20),
      const Text(
        'Tier 2 doctrines',
        style: TextStyle(fontSize: 19, fontWeight: FontWeight.w600),
      ),
      ...objects(_reference['doctrines']).map(
        (choice) => ExpansionTile(
          tilePadding: EdgeInsets.zero,
          title: Text('${choice['displayName']} · ${choice['cost']}'),
          subtitle: Text(choice['summary'].toString()),
          children: [StatRows(stats: objects(choice['stats']))],
        ),
      ),
      const SizedBox(height: 20),
      const Text(
        'Final roles',
        style: TextStyle(fontSize: 19, fontWeight: FontWeight.w600),
      ),
      const Text(
        'Reference values below are before the chosen doctrine and active field bonuses.',
        style: TextStyle(fontSize: 13),
      ),
      ...objects(_reference['specializations']).map(
        (choice) => ExpansionTile(
          tilePadding: EdgeInsets.zero,
          title: Text('${choice['displayName']} · ${choice['cost']}'),
          subtitle: Text(choice['summary'].toString()),
          children: [StatRows(stats: objects(choice['stats']))],
        ),
      ),
      const SizedBox(height: 20),
      const Text(
        'Apex · unlocks before wave 21',
        style: TextStyle(fontSize: 19, fontWeight: FontWeight.w600),
      ),
      const Text(
        'Permanent promotion with independent automatic Protocol activation.',
      ),
      _facts(object(_reference['apex'])),
    ],
  );

  Widget _sandbox() {
    final enemies = objects(engine.catalog['enemies']);
    if (_enemyId.isEmpty && enemies.isNotEmpty) {
      _enemyId = enemies.first['id'].toString();
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _picker(
          'Enemy',
          _enemyId,
          enemies,
          (value) => setState(() => _enemyId = value),
        ),
        DropdownButtonFormField<String>(
          initialValue: _rank,
          decoration: const InputDecoration(labelText: 'Rank'),
          items: ['Standard', 'Elite', 'Boss']
              .map((rank) => DropdownMenuItem(value: rank, child: Text(rank)))
              .toList(),
          onChanged: (value) => setState(() => _rank = value!),
        ),
        const SizedBox(height: 16),
        DropdownButtonFormField<String>(
          initialValue: _signal,
          decoration: const InputDecoration(labelText: 'Signal role'),
          items:
              [
                    'None',
                    ...objects(engine.catalog['signals'])
                        .map((signal) => signal['id'].toString()),
                  ]
                  .map(
                    (role) => DropdownMenuItem(value: role, child: Text(role)),
                  )
                  .toList(),
          onChanged: (value) => setState(() => _signal = value!),
        ),
        const SizedBox(height: 16),
        Text('Group size: $_enemyCount'),
        Slider(
          value: _enemyCount.toDouble(),
          min: 1,
          max: 24,
          divisions: 23,
          onChanged: (value) => setState(() => _enemyCount = value.round()),
        ),
        Text('Health ×${_health.toStringAsFixed(1)}'),
        Slider(
          value: _health,
          min: 1,
          max: 10,
          divisions: 18,
          onChanged: (value) => setState(() => _health = value),
        ),
        SwitchListTile(
          title: const Text('Immortal targets'),
          value: _immortal,
          onChanged: (value) => setState(() => _immortal = value),
        ),
        _button(
          'Spawn targets',
          engine.canMutate
              ? () => engine.issue('sandbox', {
                  'operation': 'spawn',
                  'enemyId': _enemyId,
                  'rank': _rank,
                  'signalRole': _signal,
                  'count': _enemyCount,
                  'health': _health,
                  'immortal': _immortal,
                })
              : null,
          primary: true,
        ),
        const Divider(),
        Text('Campaign wave: $_sandboxWave'),
        Slider(
          value: _sandboxWave.toDouble(),
          min: 1,
          max: 30,
          divisions: 29,
          onChanged: (value) => setState(() => _sandboxWave = value.round()),
        ),
        SwitchListTile(
          title: const Text('Wave signals'),
          value:
              object(engine.state['sandbox'])['sandboxWaveSignalsEnabled'] ==
              true,
          onChanged: engine.canMutate
              ? (_) => engine.issue('sandbox', {'operation': 'signals'})
              : null,
        ),
        _button(
          'Send test wave',
          engine.canMutate
              ? () => engine.issue('sandbox', {
                  'operation': 'wave',
                  'wave': _sandboxWave,
                })
              : null,
        ),
        _button(
          'Reset targets',
          engine.canMutate
              ? () => engine.issue('sandbox', {'operation': 'reset'})
              : null,
        ),
        _button(
          'Clear towers',
          engine.canMutate
              ? () async {
                  if (await _confirm(
                    'Clear all towers?',
                    'Remove every test tower from this Sandbox.',
                    'Clear',
                  )) {
                    engine.issue('sandbox', {'operation': 'clear'});
                  }
                }
              : null,
        ),
      ],
    );
  }

  Widget _result() {
    final result = object(engine.state['result']);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '${engine.state['displayName']} · Wave ${engine.state['currentWave']}',
          style: const TextStyle(fontSize: 22),
        ),
        Text(
          '${result['kills'] ?? 0} kills · ${result['topTowerName'] ?? ''} led the defense',
        ),
        const SizedBox(height: 20),
        if (engine.screen == 'victory')
          _button(
            'Continue into Endless',
            () => engine.command('ContinueEndless'),
            primary: true,
          ),
        _button('View defense report', () {
          _reference = result;
          unawaited(_open('runDetail'));
        }),
        _button('New defense', () => _open('setup')),
        _button('Main menu', () => engine.issue('menu')),
      ],
    );
  }

  Widget _facts(JsonMap data) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: data.entries
        .where(
          (entry) =>
              ![
                'id',
                'visual',
                'accentColor',
                'primaryColor',
                'nodeColor',
                'color',
                'baseColor',
                'accent',
                'schemaVersion',
              ].contains(entry.key) &&
              entry.value != null,
        )
        .map((entry) {
          final value = entry.value;
          if (value is Map) {
            return ExpansionTile(
              tilePadding: EdgeInsets.zero,
              title: Text(_humanize(entry.key)),
              children: [_facts(object(value))],
            );
          }
          if (value is List) {
            return ExpansionTile(
              tilePadding: EdgeInsets.zero,
              title: Text('${_humanize(entry.key)} · ${value.length}'),
              children: value
                  .map(
                    (item) => item is Map
                        ? Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: _facts(object(item)),
                          )
                        : ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(item.toString()),
                          ),
                  )
                  .toList(),
            );
          }
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 5),
            child: Text(
              '${_humanize(entry.key)}: ${value is bool
                  ? value
                        ? 'Yes'
                        : 'No'
                  : value}',
            ),
          );
        })
        .toList(),
  );

  Widget _card(
    String title,
    String subtitle,
    VoidCallback? onTap, {
    Widget? leading,
  }) => Card(
    margin: const EdgeInsets.only(bottom: 10),
    child: ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
      leading: leading,
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: Text(subtitle),
      onTap: onTap,
      trailing: onTap == null ? null : const Icon(Icons.chevron_right),
    ),
  );

  Widget _button(
    String label,
    VoidCallback? onPressed, {
    bool primary = false,
    IconData? icon,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: SizedBox(
      width: double.infinity,
      child: OutlinedButton.icon(
        style: OutlinedButton.styleFrom(
          minimumSize: Size(48, primary ? 56 : 48),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          backgroundColor: actionSurface,
          foregroundColor: paper,
          disabledBackgroundColor: actionSurface,
          disabledForegroundColor: actionMuted,
          side: const BorderSide(color: actionBorder),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
          textStyle: Theme.of(context).textTheme.labelLarge!.copyWith(
            fontSize: primary ? 17 : 15,
            fontWeight: primary ? FontWeight.w600 : FontWeight.w500,
            letterSpacing: .3,
          ),
        ),
        onPressed: onPressed,
        icon: icon == null ? null : Icon(icon, size: 20),
        label: Text(label, textAlign: TextAlign.center),
      ),
    ),
  );

  String _humanize(String text) => text
      .replaceAll('_', ' ')
      .replaceAllMapped(
        RegExp(r'([a-z])([A-Z])'),
        (match) => '${match[1]} ${match[2]}',
      );
  String _friendly(String text) => text.toLowerCase().replaceAllMapped(
    RegExp(r'(^|[|] )([a-z])'),
    (match) => '${match[1]}${match[2]!.toUpperCase()}',
  );
}
