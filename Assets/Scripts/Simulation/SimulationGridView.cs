using UnityEngine;

namespace Brick2Life.Simulation
{
    public class SimulationGridView : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineTemplate;
        [SerializeField] private float lineWidth = 0.04f;
        [SerializeField] private float borderWidth = 0.08f;

        private void Start()
        {
            if (lineTemplate == null)
            {
                Debug.LogError("请先指定网格线模板。", this);
                return;
            }

            // 生成前统一设置线宽，克隆出来的线条会继承这个值。
            lineTemplate.widthMultiplier = lineWidth;

            for (int i = 0; i <= 10; i++)
            {
                // 外框跨在底板边缘上，加粗后在缩小预览中仍能看清。
                float currentWidth = i == 0 || i == 10
                    ? Mathf.Max(lineWidth, borderWidth)
                    : lineWidth;

                // 沿 X 方向画线，每次改变 Z。
                LineRenderer xLine = Instantiate(lineTemplate, transform);
                xLine.name = $"LineX_{i:00}";
                xLine.widthMultiplier = currentWidth;
                xLine.SetPosition(0, new Vector3(0, 0.01f, i));
                xLine.SetPosition(1, new Vector3(10, 0.01f, i));

                // 沿 Z 方向画线，每次改变 X。
                LineRenderer zLine = Instantiate(lineTemplate, transform);
                zLine.name = $"LineZ_{i:00}";
                zLine.widthMultiplier = currentWidth;
                zLine.SetPosition(0, new Vector3(i, 0.01f, 0));
                zLine.SetPosition(1, new Vector3(i, 0.01f, 10));
            }

            // 完成复制后，隐藏原始模板，避免重复显示。
            lineTemplate.enabled = false;
        }
    }
}
