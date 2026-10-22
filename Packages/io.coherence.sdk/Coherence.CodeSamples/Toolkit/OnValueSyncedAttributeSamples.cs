namespace Coherence.CodeSamples.Toolkit
{
    namespace OnValueSyncedAttribute_MethodAttribute
    {
        #region MethodAttribute

        using UnityEngine;
        using Coherence.Toolkit;
        using UnityEngine.Scripting;

        public class Example : MonoBehaviour
        {
            [Sync]
            public int myInt;

            [Preserve, OnValueSynced(nameof(myInt))]
            void OnMyIntSynced(int oldValue, int newValue)
            {
                // ...
            }
        }

        #endregion
    }

    namespace OnValueSyncedAttribute_FieldAttribute
    {
        #region FieldAttribute

        using UnityEngine;
        using Coherence.Toolkit;
        using UnityEngine.Scripting;

        public class Example : MonoBehaviour
        {
            [Sync]
            [OnValueSynced(nameof(OnBoxColliderSynced))]
            public BoxCollider boxCollider;

            [Preserve]
            void OnBoxColliderSynced(BoxCollider oldValue, BoxCollider newValue)
            {
                // ...
            }
        }

        #endregion
    }
}
