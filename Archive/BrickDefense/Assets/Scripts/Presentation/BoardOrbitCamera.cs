// Orbit calculation adapted from Jermesa-Studio/JrsOrbitCamera.cs (main, read 2026-10-11).
// MIT License
// Copyright (c) 2023 Samborlang Pyrtuh
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using UnityEngine;

namespace BrickDefense.Presentation
{
    // Fixed board target; input and GUI capture belong to DemoUI.
    internal sealed class BoardOrbitCamera
    {
        private const float Radius=45,InitialYaw=-45,InitialPitch=55,MinimumPitch=45,MaximumPitch=75;
        private readonly Camera camera;
        private float yaw,pitch;
        public float ZoomFactor { get; private set; }=1;

        public BoardOrbitCamera(Camera camera)
        {
            this.camera=camera;
            Reset();
        }

        public void Rotate(float yawDegrees,float pitchDegrees)
        {
            yaw=Mathf.Repeat(yaw+yawDegrees+180,360)-180;
            pitch=Mathf.Clamp(pitch+pitchDegrees,MinimumPitch,MaximumPitch);
            Apply();
        }

        public void Zoom(float steps)
        { ZoomFactor=Mathf.Clamp(ZoomFactor*Mathf.Pow(1.15f,steps),0.65f,2.5f); Apply(); }

        public void Reset() { yaw=InitialYaw; pitch=InitialPitch; ZoomFactor=1; Apply(); }

        // 棋盘中心固定在原点；正交尺寸随窗口比例与缩放倍率调整。
        public void Apply()
        {
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
            camera.orthographic=true;
            camera.transform.SetPositionAndRotation(-(rotation*new Vector3(0,0,Radius)),rotation);
            // Fit at 100%; higher zoom intentionally shows a smaller part of the board.
            camera.orthographicSize=Mathf.Max(21f,16f/Mathf.Max(camera.aspect,0.1f))/ZoomFactor;
        }
    }
}
