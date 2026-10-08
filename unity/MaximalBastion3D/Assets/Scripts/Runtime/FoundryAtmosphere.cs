using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bastion3D
{
    /// <summary>Local factory presentation; it never writes synchronized game state.</summary>
    public sealed class FoundryAtmosphere : MonoBehaviour
    {
        private const int CloudLimit = 96, SparkLimit = 28;
        private sealed class Vent
        {
            public Transform socket;
            public float clock, phase;
            public bool damaged;
        }
        private sealed class Rotor
        {
            public Transform transform;
            public float speed;
        }
        private sealed class CoreSurface
        {
            public Renderer renderer;
            public int slot;
            public MaterialPropertyBlock block = new MaterialPropertyBlock();
        }
        private struct Particle
        {
            public bool active;
            public Vector3 position, velocity;
            public float age, lifetime, radius, phase;
            public Color color;
        }

        private readonly Particle[] _clouds = new Particle[CloudLimit];
        private readonly Particle[] _sparks = new Particle[SparkLimit];
        private readonly MeshRenderer[] _cloudRenderers = new MeshRenderer[CloudLimit];
        private readonly MeshRenderer[] _sparkRenderers = new MeshRenderer[SparkLimit];
        private readonly List<Vent> _vents = new List<Vent>();
        private readonly List<Transform> _heat = new List<Transform>();
        private readonly List<Rotor> _rotors = new List<Rotor>();
        private readonly List<CoreSurface> _coreSurfaces = new List<CoreSurface>();
        private MaterialPropertyBlock _cloudBlock;
        private MaterialPropertyBlock _sparkBlock;
        private readonly Light[] _heatLights = new Light[2];
        private Light _coreLight;
        private Transform _coreSocket;
        private SimulationHost _host;
        private Camera _camera;
        private Mesh _volumeMesh;
        private Material _volumeMaterial;
        private Material _skyMaterial;
        private Cubemap _reflectionSky;
        private bool _reflectionPending;
        private float _reflectionAt;
        private AudioSource _machinery;
        private AudioClip _machineClip;
        private uint _seed = 58391;
        private float _scanAt, _sparkClock, _health = 1, _time;
        private int _cloudCursor, _sparkCursor, _lastLives = -1;
        private bool _initialized;
        private bool _reportedSockets;
        private bool _reportedClouds;

        public void Initialize(SimulationHost host)
        {
            _initialized = false;
            _cloudBlock = new MaterialPropertyBlock();
            _sparkBlock = new MaterialPropertyBlock();
            _host = host;
            _camera = Camera.main;
            _host.SessionChanged += RebindSoon;
            _volumeMesh = VolumeMesh();
            var shader = Shader.Find("Bastion/FoundryVolume");
            if (!shader) { Debug.LogWarning("Foundry volume shader is unavailable."); enabled = false; return; }
            _volumeMaterial = new Material(shader) { name = "Foundry scattering", enableInstancing = true };
            var volumes = new GameObject("Foundry volumes").transform;
            volumes.SetParent(transform, false);
            for (int i = 0; i < CloudLimit; i++) _cloudRenderers[i] = CreateVolume("Steam " + i, volumes);
            for (int i = 0; i < SparkLimit; i++) _sparkRenderers[i] = CreateVolume("Heat spark " + i, volumes);
            BuildSky();
            for (int i = 0; i < _heatLights.Length; i++) _heatLights[i] = LocalLight("Furnace flicker " + i, 4.5f);
            _coreLight = LocalLight("Reactor integrity light", 5);
            _machineClip = MachineLoop();
            _machinery = new GameObject("Foundry machinery").AddComponent<AudioSource>();
            _machinery.transform.SetParent(transform);
            _machinery.transform.position = new Vector3(0, 4, -22);
            _machinery.clip = _machineClip; _machinery.loop = true;
            _machinery.spatialBlend = .75f; _machinery.minDistance = 14; _machinery.maxDistance = 100;
            _machinery.rolloffMode = AudioRolloffMode.Linear; _machinery.dopplerLevel = 0;
            _machinery.volume = 0; _machinery.playOnAwake = false; _machinery.Play();
            RebindSoon();
            _initialized = true;
        }

        private MeshRenderer CreateVolume(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _volumeMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _volumeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.enabled = false;
            return renderer;
        }

        private void BuildSky()
        {
            var shader = Shader.Find("Bastion/FoundrySky");
            if (!shader) return;
            const int resolution = 128;
            _reflectionSky = new Cubemap(resolution, TextureFormat.RGBAHalf, true)
            {
                name = "Foundry overcast radiance", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            for (int face = 0; face < 6; face++)
            {
                var pixels = new Color[resolution * resolution];
                for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
                {
                    float u = (x + .5f) / resolution * 2 - 1, v = (y + .5f) / resolution * 2 - 1;
                    Vector3 direction;
                    switch ((CubemapFace)face)
                    {
                        case CubemapFace.PositiveX: direction = new Vector3(1, -v, -u); break;
                        case CubemapFace.NegativeX: direction = new Vector3(-1, -v, u); break;
                        case CubemapFace.PositiveY: direction = new Vector3(u, 1, v); break;
                        case CubemapFace.NegativeY: direction = new Vector3(u, -1, -v); break;
                        case CubemapFace.PositiveZ: direction = new Vector3(u, -v, 1); break;
                        default: direction = new Vector3(-u, -v, -1); break;
                    }
                    pixels[y * resolution + x] = SkyRadiance(direction.normalized);
                }
                _reflectionSky.SetPixels(pixels, (CubemapFace)face);
            }
            _reflectionSky.Apply(true, false);
            _skyMaterial = new Material(shader) { name = "Foundry atmospheric sky" };
            _skyMaterial.SetTexture("_Tex", _reflectionSky);
            _skyMaterial.SetFloat("_Exposure", .65f);
            RenderSettings.skybox = _skyMaterial;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = _reflectionSky;
            RenderSettings.reflectionIntensity = .85f;
            RenderSettings.ambientMode = AmbientMode.Custom;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.31f, .36f, .43f));
            ambient.AddDirectionalLight(new Vector3(-.3f, 1, -.2f).normalized, new Color(.48f, .58f, .72f), .30f);
            ambient.AddDirectionalLight(new Vector3(.4f, .2f, .6f).normalized, new Color(.44f, .29f, .18f), .18f);
            RenderSettings.ambientProbe = ambient;
            RenderSettings.fogColor = new Color(.13f, .17f, .205f);
            if (_camera)
            {
                _camera.clearFlags = CameraClearFlags.Skybox;
                _camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            }
            _reflectionPending = true;
            _reflectionAt = Time.unscaledTime + 1;
        }

        private static Color SkyRadiance(Vector3 direction)
        {
            float elevation = direction.y;
            var horizon = new Color(.24f, .30f, .35f);
            var color = elevation >= 0
                ? Color.Lerp(horizon, new Color(.13f, .205f, .31f), Mathf.Pow(elevation, .55f))
                : Color.Lerp(horizon, new Color(.105f, .13f, .16f), Mathf.Pow(-elevation, .42f));
            var coolDirection = new Vector3(-.45f, .75f, -.48f).normalized;
            var warmDirection = new Vector3(.72f, .27f, .64f).normalized;
            float cool = Mathf.Pow(Mathf.Clamp01((Vector3.Dot(direction, coolDirection) + .2f) / 1.2f), 5);
            float warm = Mathf.Pow(Mathf.Clamp01((Vector3.Dot(direction, warmDirection) + .18f) / 1.18f), 7);
            color += new Color(.48f, .58f, .72f) * cool;
            color += new Color(.38f, .21f, .10f) * warm;
            color.a = 1;
            return color;
        }

        private void RefreshReflections()
        {
            if (!_reflectionPending || Time.unscaledTime < _reflectionAt) return;
            _reflectionPending = false;
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = _reflectionSky;
                probe.boxProjection = false;
                probe.intensity = .95f;
            }
        }

        private Light LocalLight(string name, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform);
            light.type = LightType.Point; light.range = range;
            light.shadows = LightShadows.None; light.enabled = false;
            return light;
        }

        private void RebindSoon()
        {
            _scanAt = Time.unscaledTime + .1f;
            _lastLives = -1;
            Array.Clear(_clouds, 0, _clouds.Length);
            Array.Clear(_sparks, 0, _sparks.Length);
        }

        private static bool Socket(Transform t, string prefix) =>
            (t.name == prefix || t.name.StartsWith(prefix + "_") || t.name.StartsWith(prefix + ".")) &&
            !t.TryGetComponent<MeshFilter>(out _);

        private void Rebind()
        {
            _scanAt = Time.unscaledTime + 5;
            _vents.Clear(); _heat.Clear(); _rotors.Clear(); _coreSurfaces.Clear(); _coreSocket = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (Socket(t, "Exhaust")) _vents.Add(new Vent { socket = t, clock = Random01() * 1.7f, phase = Random01() * 20 });
                else if (Socket(t, "DamageSmoke")) _vents.Add(new Vent { socket = t, clock = .1f, phase = Random01() * 20, damaged = true });
                else if (Socket(t, "Heat")) _heat.Add(t);
                else if (Socket(t, "CoreLight")) _coreSocket = t;
                else if (Socket(t, "FanRotor")) _rotors.Add(new Rotor { transform = t, speed = t.name.Contains("_R") ? -90 : 90 });
                else if (Socket(t, "ChargeRotor")) _rotors.Add(new Rotor { transform = t, speed = 25 });
            }
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] && materials[i].name.StartsWith("Energy_DefendedCore"))
                        _coreSurfaces.Add(new CoreSurface { renderer = renderer, slot = i });
            }
            if (_host.VerificationMode && !_reportedSockets)
            {
                _reportedSockets = true;
                Debug.Log($"Foundry atmosphere: {_vents.Count} vents, {_heat.Count} heat sockets, {_rotors.Count} rotors, {_coreSurfaces.Count} core surfaces.");
            }
        }

        private float Random01()
        {
            _seed = _seed * 1664525 + 1013904223;
            return (_seed & 65535) / 65535f;
        }

        private void Update()
        {
            if (!_initialized || !_host || !_volumeMaterial) return;
            RefreshReflections();
            if (Time.unscaledTime >= _scanAt) Rebind();
            bool focused = _host.HasFocus || _host.VerificationMode;
            float dt = focused ? Mathf.Min(Time.unscaledDeltaTime, .05f) : 0;
            _time += dt;
            if (_machinery) _machinery.volume = Mathf.MoveTowards(_machinery.volume, focused ? .055f : 0, Time.unscaledDeltaTime * .15f);
            foreach (var rotor in _rotors) if (rotor.transform) rotor.transform.Rotate(Vector3.up, rotor.speed * dt, Space.Self);
            UpdateCore(dt);
            foreach (var vent in _vents)
            {
                if (!vent.socket || (vent.damaged && _health > .55f)) continue;
                vent.clock -= dt;
                if (vent.clock > 0) continue;
                float rate = vent.damaged ? Mathf.Lerp(1.8f, .55f, 1 - _health) : 1.15f + .6f * Random01();
                vent.clock = rate;
                SpawnCloud(vent.socket.position, vent.phase, vent.damaged);
            }
            _sparkClock -= dt;
            if (_sparkClock <= 0 && _heat.Count > 0)
            {
                _sparkClock = .75f + Random01() * 1.9f;
                var socket = _heat[Mathf.Min(_heat.Count - 1, (int)(Random01() * _heat.Count))];
                if (socket) SpawnSpark(socket.position + Vector3.up * .12f, false);
            }
            DrawClouds(dt); DrawSparks(dt); UpdateHeatLights();
        }

        private void UpdateCore(float dt)
        {
            var session = _host.Session;
            float target = session == null || session.IsSandbox ? 1 : Mathf.Clamp01(session.Economy.Lives / (float)Mathf.Max(1, session.Economy.StartingLives));
            if (session != null)
            {
                if (_lastLives >= 0 && session.Economy.Lives < _lastLives && _coreSocket)
                    for (int i = 0; i < 6; i++) SpawnSpark(_coreSocket.position, true);
                _lastLives = session.Economy.Lives;
            }
            _health = Mathf.MoveTowards(_health, target, dt * .8f);
            var color = _health >= .55f ? Color.Lerp(new Color(1,.49f,.12f), new Color(.20f,.86f,.83f), Mathf.InverseLerp(.55f,1,_health))
                : Color.Lerp(new Color(.92f,.085f,.035f), new Color(1,.49f,.12f), _health/.55f);
            float pulse = _health > .55f ? 1 : .83f + .17f * Mathf.Sin(_time * Mathf.Lerp(7,2,_health));
            foreach (var surface in _coreSurfaces)
            {
                if (!surface.renderer) continue;
                surface.block.SetColor("_BaseColor", color);
                surface.block.SetColor("_EmissionColor", color * (1.2f * pulse));
                surface.renderer.SetPropertyBlock(surface.block, surface.slot);
            }
            if (_coreLight)
            {
                _coreLight.enabled = _coreSocket;
                if (_coreSocket) { _coreLight.transform.position = _coreSocket.position; _coreLight.color = color; _coreLight.intensity = .85f * pulse; }
            }
        }

        private void SpawnCloud(Vector3 position, float phase, bool damaged)
        {
            _clouds[_cloudCursor++ % CloudLimit] = new Particle
            {
                active = true, position = position + new Vector3(Random01()-.5f,.12f,Random01()-.5f)*.28f,
                velocity = new Vector3(.08f + Random01()*.10f, damaged ? .36f : .55f, -.06f + Random01()*.10f),
                lifetime = 4.6f + Random01()*1.9f, radius = damaged ? .36f : .46f,
                phase = phase + Random01()*5,
                color = damaged ? new Color(.28f,.33f,.37f,.32f) : new Color(.62f,.69f,.74f,.28f)
            };
        }

        private void SpawnSpark(Vector3 position, bool damage)
        {
            _sparks[_sparkCursor++ % SparkLimit] = new Particle
            {
                active = true, position = position,
                velocity = new Vector3(Random01()-.5f,.3f + Random01(),Random01()-.5f)*(damage ? 2.5f : 1.35f),
                lifetime = .42f + Random01()*.55f, radius = .016f + Random01()*.012f,
                color = damage ? new Color(2.6f,.48f,.12f,1) : new Color(2.3f,1.10f,.28f,.9f)
            };
        }

        private void DrawClouds(float dt)
        {
            int count = 0;
            for (int i = 0; i < _clouds.Length; i++)
            {
                ref var p = ref _clouds[i];
                var renderer = _cloudRenderers[i];
                if (!p.active) { renderer.enabled = false; continue; }
                p.age += dt;
                if (p.age >= p.lifetime) { p.active = false; renderer.enabled = false; continue; }
                p.position += (p.velocity + new Vector3(Mathf.Sin(_time*.7f+p.phase)*.07f,0,Mathf.Cos(_time*.6f+p.phase)*.055f))*dt;
                float age = p.age/p.lifetime;
                float radius = p.radius + p.age*.15f;
                float opacity = Mathf.SmoothStep(0,1,age*7) * Mathf.Pow(1-age,1.35f);
                renderer.transform.SetPositionAndRotation(p.position, Quaternion.Euler(0,p.phase*31+_time*6,0));
                renderer.transform.localScale = new Vector3(radius*2.1f,radius*1.35f,radius*1.7f);
                var color = p.color; color.a *= opacity;
                _cloudBlock.SetVector("_BaseColor", color); _cloudBlock.SetFloat("_Density", 2.1f);
                renderer.SetPropertyBlock(_cloudBlock); renderer.enabled = true;
                count++;
            }
            if (count >= 12 && _host.VerificationMode && !_reportedClouds)
            {
                _reportedClouds = true;
                Debug.Log($"Foundry atmosphere: {count} active closed steam volumes in the scene renderer list.");
            }
        }

        private void DrawSparks(float dt)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                ref var p = ref _sparks[i];
                var renderer = _sparkRenderers[i];
                if (!p.active) { renderer.enabled = false; continue; }
                p.age += dt;
                if (p.age >= p.lifetime) { p.active = false; renderer.enabled = false; continue; }
                p.velocity += Vector3.down*(2.2f*dt); p.position += p.velocity*dt;
                float fade = 1-p.age/p.lifetime;
                var rotation = p.velocity.sqrMagnitude>.001f ? Quaternion.LookRotation(p.velocity) : Quaternion.identity;
                renderer.transform.SetPositionAndRotation(p.position,rotation);
                renderer.transform.localScale = new Vector3(p.radius*2,p.radius*2,p.radius*5);
                var color=p.color; color.a *= fade;
                _sparkBlock.SetVector("_BaseColor",color); _sparkBlock.SetFloat("_Density",8);
                renderer.SetPropertyBlock(_sparkBlock); renderer.enabled=true;
            }
        }

        private void UpdateHeatLights()
        {
            var observer = _camera ? _camera.transform.position : Vector3.zero;
            Transform first = null, second = null;
            float firstDistance=float.MaxValue, secondDistance=float.MaxValue;
            foreach(var socket in _heat)
            {
                if(!socket) continue;
                float distance=(socket.position-observer).sqrMagnitude;
                if(distance<firstDistance) { second=first;secondDistance=firstDistance;first=socket;firstDistance=distance; }
                else if(distance<secondDistance) { second=socket;secondDistance=distance; }
            }
            for(int i=0;i<2;i++)
            {
                var socket=i==0?first:second;
                var light=_heatLights[i]; light.enabled=socket;
                if(!socket) continue;
                light.transform.position=socket.position+Vector3.up*.35f;
                light.color=new Color(1,.35f,.08f);
                light.intensity=.42f+.06f*Mathf.Sin(_time*7.3f+i*2.1f)+.035f*Mathf.Sin(_time*12.7f+i);
            }
        }

        private static Mesh VolumeMesh()
        {
            const int rings=10, sectors=20;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int r=0;r<=rings;r++)
            {
                float polar=r*Mathf.PI/rings;
                for(int s=0;s<=sectors;s++)
                {
                    float angle=s*Mathf.PI*2/sectors;
                    vertices.Add(new Vector3(Mathf.Sin(polar)*Mathf.Cos(angle),Mathf.Cos(polar),Mathf.Sin(polar)*Mathf.Sin(angle))*.5f);
                }
            }
            for(int r=0;r<rings;r++) for(int s=0;s<sectors;s++)
            {
                int a=r*(sectors+1)+s,b=a+sectors+1;
                if(r>0) { triangles.Add(a);triangles.Add(a+1);triangles.Add(b); }
                if(r<rings-1) { triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b); }
            }
            var mesh=new Mesh { name="Closed foundry scattering volume",vertices=vertices.ToArray(),triangles=triangles.ToArray() };
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static AudioClip MachineLoop()
        {
            const int rate=22050,duration=8;
            var samples=new float[rate*duration];uint seed=44281;float noise=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                seed=seed*1664525+1013904223;
                noise+=(((seed&65535)/32767.5f-1)-noise)*.025f;
                float rotation=.8f+.2f*Mathf.Sin(t*Mathf.PI*.5f);
                float hum=Mathf.Sin(t*Mathf.PI*2*45)*.24f+Mathf.Sin(t*Mathf.PI*2*90)*.06f;
                float edge=Mathf.Clamp01(Mathf.Min(t,duration-t)*24);
                samples[i]=(hum*rotation+noise*.26f)*edge;
            }
            var clip=AudioClip.Create("Foundry machinery loop",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        private void OnDestroy()
        {
            if(_host) _host.SessionChanged-=RebindSoon;
            if(_volumeMaterial) Destroy(_volumeMaterial);
            if(_skyMaterial) Destroy(_skyMaterial);
            if(_reflectionSky) Destroy(_reflectionSky);
            if(_volumeMesh) Destroy(_volumeMesh);
            if(_machineClip) Destroy(_machineClip);
            foreach(var surface in _coreSurfaces) if(surface.renderer) surface.renderer.SetPropertyBlock(null,surface.slot);
        }
    }
}
