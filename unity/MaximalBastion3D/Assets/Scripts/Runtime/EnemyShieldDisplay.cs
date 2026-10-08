using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion3D
{
    public sealed class EnemyShieldDisplay : MonoBehaviour
    {
        private Renderer _surface;
        private MaterialPropertyBlock _properties;
        private static Material _material;
        private float _lastFlash = -1;

        public static EnemyShieldDisplay Create(Transform body, Bounds bounds)
        {
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Active shield envelope";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.SetParent(body, false);
            shell.transform.position = bounds.center;
            shell.transform.localScale = bounds.size / body.localScale.x * 1.10f;
            var display = shell.AddComponent<EnemyShieldDisplay>();
            display._surface = shell.GetComponent<Renderer>();
            display._properties = new MaterialPropertyBlock();
            if (!_material) _material = new Material(Shader.Find("Bastion/Shield")) { enableInstancing = true };
            display._surface.sharedMaterial = _material;
            display._surface.shadowCastingMode = ShadowCastingMode.Off;
            display._surface.receiveShadows = false;
            return display;
        }

        public void SetState(bool active, float flash)
        {
            _surface.enabled = active;
            if (!active || Mathf.Abs(flash - _lastFlash) < .01f) return;
            _lastFlash = flash;
            _properties.SetFloat("_Impact", flash);
            _surface.SetPropertyBlock(_properties);
        }
    }
}
