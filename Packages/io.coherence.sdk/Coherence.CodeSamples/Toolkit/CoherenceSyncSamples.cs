namespace Coherence.CodeSamples.Toolkit
{
    namespace Adopt
    {
        #region Adopt

        using UnityEngine;
        using Coherence.Toolkit;

        public class Example : MonoBehaviour
        {
            public CoherenceSync sync;

            void Update()
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    TryToAdopt();
                }
            }

            private void TryToAdopt()
            {
                if (!sync.EntityState.IsOrphaned)
                {
                    Debug.LogWarning("Can't adopt an entity that is not orphaned.");
                    return;
                }

                if (sync.Adopt())
                {
                    Debug.Log("Adoption requested");
                }
                else
                {
                    Debug.LogWarning("Adoption request failed");
                }
            }
        }

        #endregion
    }
}
