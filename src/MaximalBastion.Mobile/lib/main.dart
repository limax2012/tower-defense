import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'ui/bastion_app.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
  runApp(const BastionApp());
}
