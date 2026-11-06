using UnityEngine;

namespace Lassu.Museum.Editor
{
    /// <summary>Temporary evidence of submissions for one camera; never saved in the scene.</summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    public sealed class MuseumOcclusionRenderProbe : MonoBehaviour
    {
        public Camera TargetCamera { get; set; }
        public bool Submitted { get; private set; }
        public void ResetSample() => Submitted = false;
        private void OnWillRenderObject()
        {
            if (Camera.current == TargetCamera) Submitted = true;
        }
    }
}
