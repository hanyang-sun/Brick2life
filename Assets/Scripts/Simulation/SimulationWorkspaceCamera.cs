using UnityEngine;

namespace Brick2Life.Simulation
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class SimulationWorkspaceCamera : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float padding = 1.1f;
        [SerializeField, Min(0.1f)] private float minimumSize = 7.1f;

        private Camera workspaceCamera;

        private void Awake()
        {
            workspaceCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            float halfWidth = 0;
            float halfHeight = 0;

            // 当前单层工作台的范围：X/Z 为 0～10，Y 为 0～1。
            for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = transform.InverseTransformPoint(new Vector3(x * 10, y, z * 10));
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(corner.x));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(corner.y));
                    }

            // 正交尺寸表示半高；把横向需求换算成半高，取更大的需求。
            float requiredSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(workspaceCamera.aspect, 0.01f));
            workspaceCamera.orthographicSize = Mathf.Max(minimumSize, requiredSize * padding);
        }
    }
}
