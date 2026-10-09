using UnityEngine;

namespace FishGame.Gameplay
{
    // Authority: none (local presentation). The scene camera and its AudioListener are enabled only while this peer has no
    // local player (PlayerNetState.Local == null). Attached to the SceneCamera object of each gameplay scene (contract 6.5).
    // Reads PlayerNetState.Local in OnEnable, then follows LocalPlayerChanged, so it never depends on having seen the event.
    public sealed class SceneCameraSwitch : MonoBehaviour
    {
        private UnityEngine.Camera _camera;
        private AudioListener _listener;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _listener = GetComponent<AudioListener>();
        }

        private void OnEnable()
        {
            PlayerNetState.LocalPlayerChanged += Apply;
            Apply(PlayerNetState.Local);
        }

        private void OnDisable()
        {
            PlayerNetState.LocalPlayerChanged -= Apply;
        }

        private void Apply(PlayerNetState local)
        {
            bool sceneActive = local == null;
            if (_camera != null)
            {
                _camera.enabled = sceneActive;
            }

            if (_listener != null)
            {
                _listener.enabled = sceneActive;
            }
        }
    }
}
