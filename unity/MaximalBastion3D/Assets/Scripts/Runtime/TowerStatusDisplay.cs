using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion3D
{
    public sealed class TowerStatusDisplay : MonoBehaviour
    {
        private GameObject _disrupt, _jam;
        private Camera _camera;
        private static Material _outline, _violet, _amber;

        public static TowerStatusDisplay Create(Transform tower, float height)
        {
            var go = new GameObject("Tower signal status");
            go.transform.SetParent(tower, false);
            go.transform.localPosition = Vector3.up * height;
            var display = go.AddComponent<TowerStatusDisplay>();
            display.Build();
            return display;
        }

        private void Build()
        {
            var cameraRig = FindFirstObjectByType<BattlefieldCamera>();
            _camera = cameraRig ? cameraRig.GetComponent<Camera>() : Camera.main;
            if (!_outline)
            {
                _outline = Material(new Color(.035f, .055f, .08f));
                _violet = Material(new Color(.72f, .42f, 1.4f));
                _amber = Material(new Color(1.2f, .63f, .13f));
            }
            _disrupt = new GameObject("Disrupted / pause bars");
            _disrupt.transform.SetParent(transform, false);
            Bar(_disrupt.transform, new Vector2(-.12f, 0), new Vector2(.11f, .36f), 0, _violet);
            Bar(_disrupt.transform, new Vector2(.12f, 0), new Vector2(.11f, .36f), 0, _violet);
            _jam = new GameObject("Suppressed / interrupted signal");
            _jam.transform.SetParent(transform, false);
            Bar(_jam.transform, new Vector2(0, -.04f), new Vector2(.09f, .30f), 0, _amber);
            Bar(_jam.transform, new Vector2(-.18f, .08f), new Vector2(.08f, .30f), -24, _amber);
            Bar(_jam.transform, new Vector2(.18f, .08f), new Vector2(.08f, .30f), 24, _amber);
            Bar(_jam.transform, Vector2.zero, new Vector2(.065f, .61f), -52, _amber);
            SetState(false, false);
        }

        private static Material Material(Color color)
        {
            var mat = new Material(Shader.Find("Bastion/Signal"));
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            return mat;
        }

        private static void Bar(Transform parent, Vector2 center, Vector2 size, float angle, Material color)
        {
            var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = "Signal border";
            Destroy(border.GetComponent<Collider>());
            border.transform.SetParent(parent, false);
            border.transform.localPosition = new Vector3(center.x, center.y, 0);
            border.transform.localRotation = Quaternion.Euler(0, 0, angle);
            border.transform.localScale = new Vector3(size.x + .065f, size.y + .065f, .035f);
            var renderer = border.GetComponent<Renderer>();
            renderer.sharedMaterial = _outline; renderer.shadowCastingMode = ShadowCastingMode.Off;
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Signal light";
            Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(parent, false);
            face.transform.localPosition = new Vector3(center.x, center.y, -.025f);
            face.transform.localRotation = Quaternion.Euler(0, 0, angle);
            face.transform.localScale = new Vector3(size.x, size.y, .025f);
            renderer = face.GetComponent<Renderer>();
            renderer.sharedMaterial = color; renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void SetState(bool disrupted, bool suppressed)
        {
            _disrupt.SetActive(disrupted); _jam.SetActive(suppressed);
            _disrupt.transform.localPosition = disrupted && suppressed ? Vector3.left * .38f : Vector3.zero;
            _jam.transform.localPosition = disrupted && suppressed ? Vector3.right * .38f : Vector3.zero;
            enabled = disrupted || suppressed;
        }

        private void LateUpdate()
        {
            if (_camera) transform.rotation = _camera.transform.rotation;
        }
    }
}
