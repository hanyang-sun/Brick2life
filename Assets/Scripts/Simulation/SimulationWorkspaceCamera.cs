using Brick2Life.Core;
using UnityEngine;

namespace Brick2Life.Simulation
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class SimulationWorkspaceCamera : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float padding = 1.1f;
        [SerializeField, Min(0.1f)] private float minimumSize = 7.1f;
        [SerializeField] private SimulationBoardSession board;

        private Camera workspaceCamera;

        private void Awake()
        {
            workspaceCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            float halfWidth = 0;
            float halfHeight = 0;
            int top = 1;
            if (board != null && board.IsInitialized)
                foreach (BoardLayout.Brick brick in board.Bricks)
                    top = Mathf.Max(top, brick.Layer + 1);

            for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = transform.InverseTransformPoint(new Vector3(x * BoardLayout.Size, y * top, z * BoardLayout.Size));
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(corner.x));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(corner.y));
                    }

            // 正交尺寸表示半高；把横向需求换算成半高，取更大的需求。
            float requiredSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(workspaceCamera.aspect, 0.01f));
            workspaceCamera.orthographicSize = Mathf.Max(minimumSize, requiredSize * padding);
        }

        internal void Orbit(float horizontalPixels)
        {
            float center = BoardLayout.Size * 0.5f;
            transform.RotateAround(new Vector3(center, 0, center), Vector3.up, horizontalPixels * 0.35f);
        }
    }
}
