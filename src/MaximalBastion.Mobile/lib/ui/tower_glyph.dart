import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../engine/engine_bridge.dart';

Color authoredColor(dynamic value, [Color fallback = const Color(0xff2192aa)]) {
  final text = value?.toString().replaceFirst('#', '') ?? '';
  final parsed = int.tryParse(text, radix: 16);
  return parsed == null ? fallback : Color(0xff000000 | parsed);
}

class TowerGlyph extends StatelessWidget {
  const TowerGlyph({super.key, required this.tower, this.size = 32});
  final JsonMap tower;
  final double size;
  @override
  Widget build(BuildContext context) => CustomPaint(
    size: Size.square(size),
    painter: _TowerPainter(object(tower['visual'])),
  );
}

class _TowerPainter extends CustomPainter {
  _TowerPainter(this.visual);
  final JsonMap visual;
  @override
  void paint(Canvas canvas, Size size) {
    final center = size.center(Offset.zero);
    final radius = size.shortestSide * .36;
    final color = authoredColor(visual['primary']);
    canvas.drawCircle(
      center,
      radius + 4,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2,
    );
    final shape = visual['shape'];
    if (shape == 'circle') {
      canvas.drawCircle(center, radius - 2, Paint()..color = color);
    } else {
      final sides = shape == 'triangle'
          ? 3
          : shape == 'hexagon'
          ? 6
          : 4;
      final angle = shape == 'diamond' ? -math.pi / 4 : -math.pi / 2;
      final path = Path();
      for (var i = 0; i < sides; i++) {
        final theta = angle + i * 2 * math.pi / sides;
        final p = center + Offset(math.cos(theta), math.sin(theta)) * radius;
        if (i == 0) {
          path.moveTo(p.dx, p.dy);
        } else {
          path.lineTo(p.dx, p.dy);
        }
      }
      canvas.drawPath(path..close(), Paint()..color = color);
    }
    canvas.drawLine(
      center,
      center - Offset(0, radius * .7),
      Paint()
        ..color = authoredColor(visual['accent'], const Color(0xff152b46))
        ..strokeWidth = 3,
    );
  }

  @override
  bool shouldRepaint(_TowerPainter oldDelegate) =>
      oldDelegate.visual.toString() != visual.toString();
}

class StatRows extends StatelessWidget {
  const StatRows({super.key, required this.stats});
  final List<JsonMap> stats;
  @override
  Widget build(BuildContext context) => Column(
    children: stats.map((stat) {
      final previous = stat['previousValue'];
      final direction = stat['direction'];
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          children: [
            Expanded(
              child: Text(
                stat['label']?.toString() ?? '',
                style: const TextStyle(color: Color(0xff5b6b80), fontSize: 13),
              ),
            ),
            Flexible(
              child: Wrap(
                alignment: WrapAlignment.end,
                crossAxisAlignment: WrapCrossAlignment.center,
                spacing: 4,
                children: [
                  if (previous != null) ...[
                    Text(
                      previous.toString(),
                      style: const TextStyle(color: Color(0xff5b6b80)),
                    ),
                    const Icon(Icons.arrow_forward, size: 14),
                  ],
                  Text(
                    '${stat['value']}',
                    textAlign: TextAlign.end,
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: direction == 'Increase'
                          ? const Color(0xff127045)
                          : direction == 'Decrease'
                          ? const Color(0xffbc2f5a)
                          : null,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }).toList(),
  );
}
