#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuseuVirtual
{
    // Run input scenarios on gameplay frames, rather than the Editor's separate input loop.
    // This component and its dependencies are excluded entirely from player builds.
    public sealed class MuseumPlayModeValidationDriver : MonoBehaviour
    {
        IEnumerator<int> steps;
        Action<Exception> completed;
        int nextFrame;
        float deadline;

        public void Run(IEnumerator<int> sequence, Action<Exception> onCompleted)
        {
            steps = sequence; completed = onCompleted;
            nextFrame = Time.frameCount + 3;
            deadline = Time.realtimeSinceStartup + 120;
            DontDestroyOnLoad(gameObject);
        }

        void LateUpdate()
        {
            if (steps == null) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Validação excedeu 120s");
                if (Time.frameCount < nextFrame) return;
                if (steps.MoveNext()) { nextFrame = Time.frameCount + steps.Current; return; }
                Complete(null);
            }
            catch (Exception error) { Complete(error); }
        }

        void Complete(Exception error)
        {
            steps.Dispose(); steps = null;
            completed(error);
            Destroy(gameObject);
        }
    }
}
#endif
